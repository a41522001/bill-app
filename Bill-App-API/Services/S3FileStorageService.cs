using Bill_App_API.Interfaces;

namespace Bill_App_API.Services;

public class S3FileStorageService : IFileStorageService
{
    public Task DeleteAsync(string path)
    {
        throw new NotImplementedException();
    }

    public Task<(string originalPath, string thumbPath)> UploadAvatarAsync(IFormFile file)
    {
        throw new NotImplementedException();
    }
}
