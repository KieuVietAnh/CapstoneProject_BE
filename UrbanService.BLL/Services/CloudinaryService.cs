using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException("Missing Cloudinary configuration.");
        }

        _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret))
        {
            Api = { Secure = true }
        };
    }

    public async Task<CloudinaryUploadResultDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (fileStream.Length == 0)
        {
            throw new Exception("File upload không được rỗng.");
        }

        var uploadFolder = string.IsNullOrWhiteSpace(folder) ? "urban-service" : folder.Trim('/');
        UploadResult result;

        if (contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            result = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = uploadFolder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            });
        }
        else if (contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
        {
            result = await _cloudinary.UploadAsync(new VideoUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = uploadFolder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            });
        }
        else
        {
            result = await _cloudinary.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = uploadFolder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (result.Error != null)
        {
            throw new Exception(result.Error.Message);
        }

        return new CloudinaryUploadResultDto
        {
            FileUrl = result.SecureUrl?.ToString() ?? result.Url?.ToString() ?? throw new Exception("Cloudinary không trả về URL."),
            PublicId = result.PublicId,
            FileType = contentType,
            Bytes = result.Bytes
        };
    }
}
