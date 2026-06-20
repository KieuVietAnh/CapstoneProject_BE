namespace UrbanService.BLL.Interfaces;

public interface IAiClient
{
    string ModelName { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<string> ChatAsync(
        string prompt,
        IReadOnlyCollection<string>? base64Images = null,
        bool jsonFormat = false,
        CancellationToken cancellationToken = default);

    Task<string?> DownloadImageAsBase64Async(
        string imageUrl,
        CancellationToken cancellationToken = default);
}
