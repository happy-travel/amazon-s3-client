using Amazon;
using Amazon.S3;

namespace HappyTravel.AmazonS3Client.Options
{
    public class AmazonS3ClientOptions
    {
        public string AccessKeyId { get; set; } = string.Empty;
        
        public string AccessKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of an existed bucket
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Config of the Amazon S3 client.
        /// RegionEndpoint is a required option.
        /// </summary>
        public AmazonS3Config AmazonS3Config { get; set; } = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.EUWest1
        };
    }
}