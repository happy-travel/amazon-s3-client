using System;
using Microsoft.Extensions.Logging;

namespace HappyTravel.AmazonS3Client.Infrastructure.Logging
{
    internal static class LoggerExtensions
    {
        static LoggerExtensions()
        {
            S3RequestExceptionOccured = LoggerMessage.Define(LogLevel.Error,
                new EventId(3001, "S3RequestException"), $"ERROR | AmazonS3ClientService: ");
            
            AddObjectToS3RequestOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3002, "AddObjectToS3RequestOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
            
            AddObjectToS3ResponseOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3003, "AddObjectToS3ResponseOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
            
            DeleteObjectFromS3RequestOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3004, "DeleteObjectFromS3RequestOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
            
            DeleteObjectFromS3ResponseOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3005, "DeleteObjectFromS3ResponseOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
            
            DeleteObjectsFromS3RequestOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3006, "DeleteObjectsFromS3RequestOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
            
            DeleteObjectsFromS3ResponseOccured = LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3007, "DeleteObjectsFromS3ResponseOccured"), $"INFORMATION | AmazonS3ClientService: {{message}}");
        }
        
        
        internal static void LogS3RequestException(this ILogger logger, Exception exception)
            => S3RequestExceptionOccured(logger, exception);
        
        internal static void LogAddObjectToS3Request(this ILogger logger, string message)
            => AddObjectToS3RequestOccured(logger, message, null!);
        
        internal static void LogAddObjectToS3Response(this ILogger logger, string message)
            => AddObjectToS3ResponseOccured(logger, message, null!);
        
        internal static void LogDeleteObjectFromS3Request(this ILogger logger, string message)
            => DeleteObjectFromS3RequestOccured(logger, message, null!);
        
        internal static void LogDeleteObjectFromS3Response(this ILogger logger, string message)
            => DeleteObjectFromS3ResponseOccured(logger, message, null!);
        
        internal static void LogDeleteObjectsFromS3Request(this ILogger logger, string message)
            => DeleteObjectsFromS3RequestOccured(logger, message, null!);
        
        internal static void LogDeleteObjectsFromS3Response(this ILogger logger, string message)
            => DeleteObjectsFromS3ResponseOccured(logger, message, null!);
        
        
        private static readonly Action<ILogger, Exception> S3RequestExceptionOccured;
        
        private static readonly Action<ILogger, string, Exception> AddObjectToS3RequestOccured;
        
        private static readonly Action<ILogger, string, Exception> AddObjectToS3ResponseOccured;
        
        private static readonly Action<ILogger, string, Exception> DeleteObjectFromS3RequestOccured;
        
        private static readonly Action<ILogger, string, Exception> DeleteObjectFromS3ResponseOccured;
        
        private static readonly Action<ILogger, string, Exception> DeleteObjectsFromS3RequestOccured;
        
        private static readonly Action<ILogger, string, Exception> DeleteObjectsFromS3ResponseOccured;
    }
}