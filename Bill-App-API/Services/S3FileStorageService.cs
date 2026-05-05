using Amazon.S3;
using Amazon.S3.Model;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
namespace Bill_App_API.Services;

public class S3FileStorageService(IAmazonS3 client, IOptions<S3Options> s3Options) : IFileStorageService
{
    private readonly S3Options _s3Options = s3Options.Value;
    public async Task<(string originalPath, string thumbPath)> UploadAvatarAsync(IFormFile file)
    {
        var guid = Guid.NewGuid();
        var originalName = $"{_s3Options.AvatarFolder}/{guid}_original.webp";
        var thumbName = $"{_s3Options.AvatarFolder}/{guid}_thumb.webp";
        using var image = await Image.LoadAsync(file.OpenReadStream());

        // Original (400x400)
        using (var ms = new MemoryStream())
        {
            image.Mutate(x => x.Resize(400, 400));
            await image.SaveAsWebpAsync(ms);
            ms.Position = 0;
            var originalRequest = new PutObjectRequest
            {
                BucketName = _s3Options.BucketName,
                Key = originalName,
                InputStream = ms,
                ContentType = "image/webp",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            };
            await client.PutObjectAsync(originalRequest);
        }

        // Thumbnail (100x100)
        using (var ms = new MemoryStream())
        {
            image.Mutate(x => x.Resize(100, 100));
            await image.SaveAsWebpAsync(ms);
            ms.Position = 0;
            var thumbRequest = new PutObjectRequest
            {
                BucketName = _s3Options.BucketName,
                Key = thumbName,
                InputStream = ms,
                ContentType = "image/webp",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            };
            await client.PutObjectAsync(thumbRequest);
        }

        return ($"{_s3Options.CloudFrontUrl}/{originalName}", $"{_s3Options.CloudFrontUrl}/{thumbName}");
    }
    public async Task DeleteAsync(string path)
    {
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _s3Options.BucketName,
            Key = path.Replace($"{_s3Options.CloudFrontUrl}/", "")
        });
    }
}
