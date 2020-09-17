using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using HappyTravel.AmazonS3Client.Infrastructure.Logging;
using HappyTravel.AmazonS3Client.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HappyTravel.AmazonS3Client.Services
{
    public class AmazonS3ClientService : IAmazonS3ClientService
    {
        public AmazonS3ClientService(IOptions<AmazonS3ClientOptions> options, ILogger<AmazonS3ClientService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _s3Client = new Amazon.S3.AmazonS3Client(_options.AccessKeyId, _options.AccessKey, _options.AmazonS3Config);
        }


        /// <summary>
        /// Adds an object to the Amazon S3 bucket
        /// </summary>
        /// <param name="key">A name of the object in the bucket. Can include subfolders: folder/file1.jpg </param>
        /// <param name="stream"></param>
        /// <param name="acl">Access control lists https://docs.aws.amazon.com/AmazonS3/latest/dev/acl-overview.html#canned-acl</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Url to the object</returns>
        public async Task<Result<string>> Add(string key, Stream stream, S3CannedACL acl, CancellationToken cancellationToken = default)
        {
            var putObjectRequest = new PutObjectRequest
            {
                Key = key, InputStream = stream, BucketName = _options.BucketName, CannedACL = acl 
            };
            try
            {
                _logger.LogAddObjectToS3Request(GetLogMessage(key));
                
                var putObjectResponse = await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);

                _logger.LogAddObjectToS3Response(
                    $"{GetLogMessage(key)}, {nameof(putObjectResponse.ContentLength)}: {putObjectResponse.ContentLength}, {nameof(putObjectResponse.HttpStatusCode)}: {putObjectResponse.HttpStatusCode}");
                
                if (putObjectResponse.HttpStatusCode == HttpStatusCode.OK) return Result.Success(GetUrlPath(key));

                return Result.Failure<string>(
                    $"Failed to upload the object '{key}'. {nameof(putObjectResponse.HttpStatusCode)} is '{putObjectResponse.HttpStatusCode}'");
            }
            catch (Exception ex)
            {
                AddObjectKey(ex, key);
                _logger.LogS3RequestException(ex);

                return Result.Failure<string>(ex.ToString());
            }
        }


        public Task<Result<string>> Add(string key, Stream stream, CancellationToken cancellationToken = default)
            => Add(key, stream, S3CannedACL.PublicRead, cancellationToken);
        
        /// <summary>
        /// Adds multiple objects to the bucket. List object size limitation is 50. 
        /// </summary>
        /// <param name="objects">List of tuples where the first item is a key and the last item is an object's stream</param>
        /// <param name="cancellationToken"></param>
        /// <returns>URLs to the objects</returns>
        public async Task<List<Result<string>>> Add(List<(string key, Stream stream)> objects, CancellationToken cancellationToken = default)
        {
            if (objects.Count > MaxObjectsNumberToUpload)
                Result.Failure<List<string>>($"Can't upload more than {MaxObjectsNumberToUpload} objects at one time");

            var processedTasks = new List<Result<string>>(objects.Count);
            var nextTaskIndex = 0;
            var processingTasks = new List<Task<Result<string>>>(objects.Count < UploadConcurrencyNumber
                ? objects.Count
                : UploadConcurrencyNumber);
            
            for (; nextTaskIndex < UploadConcurrencyNumber && nextTaskIndex < objects.Count; nextTaskIndex++)
            {
                var objectToUpload = objects[nextTaskIndex];
                processingTasks.Add(Add(objectToUpload.key, objectToUpload.stream, cancellationToken));
            }

            while (processingTasks.Count > 0)
            {
                var task = await Task.WhenAny(processingTasks);
                processingTasks.Remove(task);

                processedTasks.Add(await task);

                if (nextTaskIndex < objects.Count)
                {
                    var objectToUpload = objects[nextTaskIndex++];
                    processingTasks.Add(Add(objectToUpload.key, objectToUpload.stream, cancellationToken));
                }
            }

            return processedTasks;
        }

        
        /// <summary>
        /// Gets an object from the bucket by a key 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Object from the bucket</returns>
        public async Task<Result<Stream>> Get(string key, CancellationToken cancellationToken = default)
        {
            var getObjectRequest = new GetObjectRequest {Key = key, BucketName = _options.BucketName};
            try
            {
                var putObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest, cancellationToken);

                return Result.Success(putObjectResponse.ResponseStream);
            }
            catch (Exception ex)
            {
                AddObjectKey(ex, key);
                _logger.LogS3RequestException(ex);
                
                return Result.Failure<Stream>(ex.ToString());
            }
        }

        
        /// <summary>
        /// Deletes an object from the bucket by a key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Result> Delete(string key, CancellationToken cancellationToken = default)
        {
            var deleteObjectRequest = new DeleteObjectRequest {Key = key, BucketName = _options.BucketName};
            try
            {
                _logger.LogDeleteObjectFromS3Request($"{GetLogMessage(key)}");

                var deleteObjectResponse = await _s3Client.DeleteObjectAsync(deleteObjectRequest, cancellationToken);
                
                _logger.LogDeleteObjectFromS3Response(
                    $"{GetLogMessage(key)}, {nameof(deleteObjectResponse.HttpStatusCode)}: {deleteObjectResponse.HttpStatusCode}");

                if (deleteObjectResponse.HttpStatusCode == HttpStatusCode.OK || deleteObjectResponse.HttpStatusCode == HttpStatusCode.NoContent)
                    return Result.Success();
                
                return Result.Failure(
                    $"Failed to delete the object '{key}'. {nameof(deleteObjectResponse.HttpStatusCode)} is '{deleteObjectResponse.HttpStatusCode}'");
            }
            catch (Exception ex)
            {
                AddObjectKey(ex, key);
                _logger.LogS3RequestException(ex);

                return Result.Failure<Stream>(ex.ToString());
            }
        }

        
        /// <summary>
        /// Deletes objects from the bucket using a list of keys
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Result> Delete(List<string> keys, CancellationToken cancellationToken = default)
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                Objects = keys.Select(key => new KeyVersion {Key = key}).ToList(), BucketName = _options.BucketName
            };
            var keysToLog = string.Join(" ", keys);
            try
            {
                _logger.LogDeleteObjectsFromS3Request(GetLogMessage(keysToLog));

                var deleteObjectsResponse = await _s3Client.DeleteObjectsAsync(deleteObjectsRequest, cancellationToken);

                _logger.LogDeleteObjectsFromS3Response(
                    $"{GetLogMessage(keysToLog)}, {nameof(deleteObjectsResponse.DeleteErrors)}: {string.Join(" ", deleteObjectsResponse.DeleteErrors)}, {nameof(deleteObjectsResponse.DeletedObjects)}: {string.Join(" ", deleteObjectsResponse.DeletedObjects.Select(obj => obj.Key))}, {nameof(deleteObjectsResponse.HttpStatusCode)}: {deleteObjectsResponse.HttpStatusCode}");

                if (deleteObjectsResponse.HttpStatusCode == HttpStatusCode.OK || deleteObjectsResponse.HttpStatusCode == HttpStatusCode.NoContent) 
                    return Result.Success();

                var errorMessage =
                    $"{nameof(deleteObjectsResponse.HttpStatusCode)} is '{deleteObjectsResponse.HttpStatusCode}'";

                var notDeletedKeys = keys.Except(deleteObjectsResponse.DeletedObjects.Select(obj => obj.Key)).ToList();
                if (notDeletedKeys.Any())
                    errorMessage =
                        $"Failed to delete objects with keys '{string.Join(", ", notDeletedKeys)}. {errorMessage}";

                return Result.Failure(errorMessage);
            }
            catch (Exception ex)
            {
                AddObjectKey(ex, keysToLog);
                _logger.LogS3RequestException(ex);

                return Result.Failure<Stream>(ex.ToString());
            }
        }

        
        /// <summary>
        /// Creates url path to the object
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Url path to the object</returns>
        public string GetUrlPath(string key) =>
            string.Format(PathString, _options.AmazonS3Config.RegionEndpoint.SystemName, _options.BucketName, key);
        
        
        private string GetLogMessage(string key) =>
            $"{nameof(_options.BucketName)}: {_options.BucketName}, {nameof(_options.AmazonS3Config.RegionEndpoint)}: {_options.AmazonS3Config.RegionEndpoint.SystemName}, {nameof(key)}: {key}";


        private void AddObjectKey(Exception exception, string key)
        {
            exception.Data.Add(nameof(key), key);
        }
        
        
        private readonly ILogger<AmazonS3ClientService> _logger;

        private const string PathString = "https://s3.{0}.amazonaws.com/{1}/{2}";
        private readonly Amazon.S3.AmazonS3Client _s3Client;
        private readonly AmazonS3ClientOptions _options;
        private const int MaxObjectsNumberToUpload = 50;
        private const int UploadConcurrencyNumber = 5;
    }
}