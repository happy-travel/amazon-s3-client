using System;
using HappyTravel.AmazonS3Client.Options;
using HappyTravel.AmazonS3Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HappyTravel.AmazonS3Client.Extensions
{
    public static class AmazonS3ClientExtensions
    {
        public static IServiceCollection AddAmazonS3Client(this IServiceCollection services, Action<AmazonS3ClientOptions> options)
        {
            services.TryAddTransient<IAmazonS3ClientService, AmazonS3ClientService>();
            
            return services.Configure(options);
        }
    }
}