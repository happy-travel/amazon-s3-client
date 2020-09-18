using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using CSharpFunctionalExtensions;

namespace HappyTravel.AmazonS3Client.Services
{
    public interface IAmazonS3ClientService
    {
        Task<Result<string>> Add(string bucketName, string key, Stream stream,  CancellationToken cancellationToken = default);
        
        Task<Result<string>> Add(string bucketName, string key, Stream stream, S3CannedACL acl, CancellationToken cancellationToken = default);
        
        Task<List<Result<string>>> Add(string bucketName, List<(string key, Stream stream)> objects, CancellationToken cancellationToken = default);

        Task<List<Result<string>>> Add(string bucketName, List<(string key, Stream stream)> objects, S3CannedACL acl, CancellationToken cancellationToken = default);
        
        Task<Result<Stream>> Get(string bucketName, string key, CancellationToken cancellationToken = default);
        
        Task<Result> Delete(string bucketName, string key, CancellationToken cancellationToken = default);

        Task<Result> Delete(string bucketName, List<string> keys, CancellationToken cancellationToken = default);

        string GetUrlPath(string bucketName, string key);
    }
}