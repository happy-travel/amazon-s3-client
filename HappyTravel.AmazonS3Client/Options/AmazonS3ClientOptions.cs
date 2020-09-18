using Amazon;
using Amazon.S3;

namespace HappyTravel.AmazonS3Client.Options
{
    public class AmazonS3ClientOptions
    {
        /// <summary>
        /// Amazon S3 Access key ID
        /// </summary>
        public string AccessKeyId { get; set; } = string.Empty;
        
        /// <summary>
        /// Amazon S3 Secret access key
        /// </summary>
        public string AccessKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Maximum number of objects to upload into a bucket at one time
        /// </summary>
        public int MaxObjectsNumberToUpload { get; set; } = 50;
        
        //Number of simultaneous uploads to a bucket
        public int UploadConcurrencyNumber { get; set; } = 5;
        
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