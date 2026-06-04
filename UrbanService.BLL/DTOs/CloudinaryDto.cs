namespace UrbanService.BLL.Dtos;

public class CloudinaryUploadResultDto
{
    public string FileUrl { get; set; } = null!;

    public string? PublicId { get; set; }

    public string? FileType { get; set; }

    public long Bytes { get; set; }
}
