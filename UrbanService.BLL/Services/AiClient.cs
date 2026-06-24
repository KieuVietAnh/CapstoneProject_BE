using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiClient> _logger;
    private readonly long _maxImageBytes;

    public AiClient(HttpClient httpClient, IConfiguration configuration, ILogger<AiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        ModelName = configuration["AI:Model"] ?? "qwen2.5vl:3b";
        _maxImageBytes = int.TryParse(configuration["AI:MaxImageBytes"], out var maxImageBytes)
            ? maxImageBytes
            : 2 * 1024 * 1024;
    }

    public string ModelName { get; }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI health check failed.");
            return false;
        }
    }

    public async Task<string> ChatAsync(
        string prompt,
        IReadOnlyCollection<string>? base64Images = null,
        bool jsonFormat = false,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null)
        {
            throw new Exception("Chua cau hinh AI:BaseUrl cho AI server.");
        }

        var message = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = prompt
        };

        if (base64Images is { Count: > 0 })
        {
            message["images"] = base64Images;
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = ModelName,
            ["stream"] = false,
            ["messages"] = new[] { message }
        };

        if (jsonFormat)
        {
            payload["format"] = "json";
        }

        HttpResponseMessage response;
        string body;

        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI chat request timed out.");
            throw new Exception($"AI server timeout. Kiem tra Ollama tai {_httpClient.BaseAddress} va firewall port 11434.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AI chat request could not connect.");
            throw new Exception($"Khong ket noi duoc AI server tai {_httpClient.BaseAddress}. Kiem tra Ollama dang chay, OLLAMA_HOST va firewall port 11434.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI chat request failed with status {StatusCode}: {Body}", response.StatusCode, body);
            throw new Exception($"AI server khong phan hoi thanh cong ({(int)response.StatusCode} {response.StatusCode}): {Truncate(body, 500)}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                throw new Exception("AI server tra ve response khong hop le.");
            }

            return contentElement.GetString() ?? string.Empty;
        }
        finally
        {
            response.Dispose();
        }
    }

    public async Task<string?> DownloadImageAsBase64Async(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength > _maxImageBytes)
            {
                _logger.LogWarning("Skipped AI image because it is larger than {MaxImageBytes} bytes.", _maxImageBytes);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            var totalBytes = 0;

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                if (totalBytes > _maxImageBytes)
                {
                    _logger.LogWarning("Skipped AI image because downloaded bytes exceeded {MaxImageBytes}.", _maxImageBytes);
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return Convert.ToBase64String(memory.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download AI analysis image.");
            return null;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
