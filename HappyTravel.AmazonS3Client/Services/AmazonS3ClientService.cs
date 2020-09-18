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
        /// Adds an object to an Amazon S3 bucket
        /// </summary>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="key">Name of the object in the bucket. Can include subfolders: folder/file1.jpg </param>
        /// <param name="stream"></param>
        /// <param name="acl">Access control lists https://docs.aws.amazon.com/AmazonS3/latest/dev/acl-overview.html#canned-acl</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Url to the object</returns>
        public async Task<Result<string>> Add(string bucketName, string key, Stream stream, S3CannedACL acl, CancellationToken cancellationToken = default)
        {
            var putObjectRequest = new PutObjectRequest
            {
                Key = key, 
                InputStream = stream, 
                BucketName = bucketName, 
                CannedACL = acl 
            };
            try
            {
                _logger.LogAddObjectToS3Request(GetLogMessage(bucketName, key));
                
                var putObjectResponse = await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);

                _logger.LogAddObjectToS3Response(
                    $"{GetLogMessage(bucketName, key)}, {nameof(putObjectResponse.ContentLength)}: {putObjectResponse.ContentLength}, {nameof(putObjectResponse.HttpStatusCode)}: {putObjectResponse.HttpStatusCode}");
                
                if (putObjectResponse.HttpStatusCode == HttpStatusCode.OK) return Result.Success(GetUrlPath(bucketName, key));

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

        
        /// <summary>
        /// Adds an object to an Amazon S3 bucket. Default Acl is PublicRead
        /// </summary>
        /// <param name="key">Name of the object in the bucket. Can include subfolders: folder/file1.jpg </param>
        /// <param name="stream"></param>
        /// <param name="bucketName">Name of existed an S3 bucket</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Url to the object</returns>
        public Task<Result<string>> Add(string bucketName, string key, Stream stream, CancellationToken cancellationToken = default)
            => Add(bucketName, key, stream, S3CannedACL.PublicRead, cancellationToken);


        /// <summary>
        /// Adds multiple objects to a bucket. List object size limitation is 50. 
        /// </summary>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="objects">List of tuples where the first item is a key and the last item is an object's stream</param>
        /// <param name="acl">Access control lists https://docs.aws.amazon.com/AmazonS3/latest/dev/acl-overview.html#canned-acl</param>
        /// <param name="cancellationToken"></param>
        /// <returns>URLs to the objects</returns>
        public async Task<List<Result<string>>> Add(string bucketName, List<(string key, Stream stream)> objects, S3CannedACL acl, CancellationToken cancellationToken = default)
        {
            if (objects.Count > _options.MaxObjectsNumberToUpload)
                Result.Failure<List<string>>($"Can't upload more than {_options.MaxObjectsNumberToUpload} objects at one time");

            var result = new List<Result<string>>(objects.Count);
            
            var processingTasksListCapacity = objects.Count < _options.UploadConcurrencyNumber
                ? objects.Count
                : _options.UploadConcurrencyNumber;
            var processingTasks = new List<Task<Result<string>>>(processingTasksListCapacity);
            
            var nextTaskIndex = 0;
            while (nextTaskIndex < _options.UploadConcurrencyNumber && nextTaskIndex < objects.Count)
            {
                var objectToUpload = objects[nextTaskIndex++];
                var nextTask = Add(bucketName, objectToUpload.key, objectToUpload.stream, cancellationToken);
                processingTasks.Add(nextTask);
            }

            while (processingTasks.Count > 0)
            {
                var task = await Task.WhenAny(processingTasks);
                processingTasks.Remove(task);

                result.Add(await task);

                if (nextTaskIndex < objects.Count)
                {
                    var objectToUpload = objects[nextTaskIndex++];
                    var nextTask = Add(bucketName, objectToUpload.key, objectToUpload.stream, acl, cancellationToken);
                    processingTasks.Add(nextTask);
                }
            }

            return result;
        }


        /// <summary>
        /// Adds multiple objects to a bucket. List object size limitation is 50. Acl is PublicRead.
        /// </summary>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="objects">List of tuples where the first item is a key and the last item is an object's stream</param>
        /// <param name="cancellationToken"></param>
        /// <returns>URLs to the objects</returns>
        public Task<List<Result<string>>> Add(string bucketName, List<(string key, Stream stream)> objects, CancellationToken cancellationToken = default)
            => Add(bucketName, objects, S3CannedACL.PublicRead, cancellationToken);


        /// <summary>
        /// Gets an object from a bucket by a key 
        /// </summary>
        /// <param name="key">Object key</param>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Object from the bucket</returns>
        public async Task<Result<Stream>> Get(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            var getObjectRequest = new GetObjectRequest 
            {
                Key = key, 
                BucketName = bucketName
            };
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
        /// Deletes an object from a bucket by a key
        /// </summary>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="key">Object key</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Result> Delete(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            var deleteObjectRequest = new DeleteObjectRequest
            {
                Key = key, 
                BucketName = bucketName
            };
            try
            {
                _logger.LogDeleteObjectFromS3Request($"{GetLogMessage(bucketName, key)}");

                var deleteObjectResponse = await _s3Client.DeleteObjectAsync(deleteObjectRequest, cancellationToken);
                
                _logger.LogDeleteObjectFromS3Response(
                    $"{GetLogMessage(bucketName, key)}, {nameof(deleteObjectResponse.HttpStatusCode)}: {deleteObjectResponse.HttpStatusCode}");

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
        /// Deletes objects from a bucket using a list of keys
        /// </summary>
        /// <param name="keys">Object keys</param>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Result> Delete(string bucketName, List<string> keys, CancellationToken cancellationToken = default)
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                Objects = keys.Select(key => new KeyVersion {Key = key}).ToList(), BucketName = bucketName
            };
            var keysToLog = string.Join(" ", keys);
            try
            {
                _logger.LogDeleteObjectsFromS3Request(GetLogMessage(bucketName, keysToLog));

                var deleteObjectsResponse = await _s3Client.DeleteObjectsAsync(deleteObjectsRequest, cancellationToken);

                _logger.LogDeleteObjectsFromS3Response(
                    $"{GetLogMessage(bucketName, keysToLog)}, {nameof(deleteObjectsResponse.DeleteErrors)}: {string.Join(" ", deleteObjectsResponse.DeleteErrors)}, {nameof(deleteObjectsResponse.DeletedObjects)}: {string.Join(" ", deleteObjectsResponse.DeletedObjects.Select(obj => obj.Key))}, {nameof(deleteObjectsResponse.HttpStatusCode)}: {deleteObjectsResponse.HttpStatusCode}");

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
        /// Creates url path to an object
        /// </summary>
        /// <param name="key"></param>
        /// <param name="bucketName">Name of an existed S3 bucket</param>
        /// <returns>Url path to the object</returns>
        public string GetUrlPath(string bucketName, string key) =>
            string.Format(PathString, _options.AmazonS3Config.RegionEndpoint.SystemName, bucketName, key);
        
        
        private string GetLogMessage(string bucketName, string key) =>
            $"{nameof(bucketName)}: {bucketName}, {nameof(_options.AmazonS3Config.RegionEndpoint)}: {_options.AmazonS3Config.RegionEndpoint.SystemName}, {nameof(key)}: {key}";


        private void AddObjectKey(Exception exception, string key)
        {
            exception.Data.Add(nameof(key), key);
        }
        
        
        private readonly ILogger<AmazonS3ClientService> _logger;

        private const string PathString = "https://s3.{0}.amazonaws.com/{1}/{2}";
        private readonly Amazon.S3.AmazonS3Client _s3Client;
        private readonly AmazonS3ClientOptions _options;
    }
}