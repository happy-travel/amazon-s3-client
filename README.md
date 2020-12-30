# Amazon S3 Client

The library that allows to manipulate with objects in an Amazon S3 bucket using AWSSDK.


## Usage
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAmazonS3Client(new AmazonS3ClientOptions
        {
            AccessKeyId = "Access-Key-Id";
            AccessKey = "Access-Key";
            MaxObjectsNumberToUpload = 50;
            UploadConcurrencyNumber = 5;
            AmazonS3Config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.EUWest1
            };
        });
        
        ...
    }
}


public class Class1
{
    public Class1(IAmazonS3ClientService client)
    {
        _client = client;
    }
}
```


## Automation

New package version is automatically published to [github packages](https://github.com/features/packages) after changes in the master branch.


## Dependencies

The project depends on following packages: 
* `AWSSDK.S3`
