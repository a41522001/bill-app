using Bill_App_API.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Bill_App_API.Services;

public class LocalFileStorageService(IWebHostEnvironment env) : IFileStorageService
{
    public async Task<(string originalPath, string thumbPath)> UploadAvatarAsync(IFormFile file)
    {
        var guid = Guid.NewGuid();
        var originalName = $"{guid}_original.webp";
        var thumbName = $"{guid}_thumb.webp";
        var folder = Path.Combine(env.WebRootPath, "avatars");

        Directory.CreateDirectory(folder);

        using var image = await Image.LoadAsync(file.OpenReadStream());

        // Original (400x400)
        image.Mutate(x => x.Resize(400, 400));
        await image.SaveAsWebpAsync(Path.Combine(folder, originalName));

        // Thumbnail (100x100)
        image.Mutate(x => x.Resize(100, 100));
        await image.SaveAsWebpAsync(Path.Combine(folder, thumbName));

        return ($"avatars/{originalName}", $"avatars/{thumbName}");
    }

    public Task DeleteAsync(string path)
    {
        var fullPath = Path.Combine(env.WebRootPath, path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }
}
