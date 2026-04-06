namespace Bill_App_API.Interfaces;

public interface IFileStorageService
{
    Task<(string originalPath, string thumbPath)> UploadAvatarAsync(IFormFile file);
    Task DeleteAsync(string path);
}
