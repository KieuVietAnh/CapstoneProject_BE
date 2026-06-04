using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResultDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string folder,
        CancellationToken cancellationToken = default);
}
