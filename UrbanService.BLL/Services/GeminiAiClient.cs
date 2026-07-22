using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class GeminiAiClient : IAiClient
{
    private const string VietnameseSystemInstruction =
        "Luôn trả lời bằng tiếng Việt có dấu. Nếu response là JSON, giữ nguyên tên field/schema được yêu cầu, nhưng mọi giá trị dạng text do AI sinh ra phải bằng tiếng Việt.";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiClient> _logger;
    private readonly long _maxImageBytes;
    private readonly double _temperature;

    public GeminiAiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        ModelName = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        _maxImageBytes = int.TryParse(configuration["AI:MaxImageBytes"], out var maxImageBytes)
            ? maxImageBytes
            : 2 * 1024 * 1024;
        _temperature = double.TryParse(configuration["AI:Temperature"], out var temperature)
            ? temperature
            : 0.1d;
    }

    public string ModelName { get; }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = GetApiKey();
            var requestUrl = $"models/{ModelName}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = "ping" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.0,
                    maxOutputTokens = 8
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini health check failed.");
            return false;
        }
    }

    public async Task<string> ChatAsync(
        string prompt,
        IReadOnlyCollection<string>? base64Images = null,
        bool jsonFormat = false,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        var requestUrl = $"models/{ModelName}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var userParts = new List<object>
        {
            new
            {
                text = jsonFormat
                    ? $"{prompt}\n\nChỉ trả về JSON hợp lệ, không bọc markdown, không thêm giải thích ngoài JSON."
                    : prompt
            }
        };

        if (base64Images is { Count: > 0 })
        {
            foreach (var image in base64Images.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                userParts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = image
                    }
                });
            }
        }

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = VietnameseSystemInstruction }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = userParts
                }
            },
            generationConfig = new
            {
                temperature = _temperature,
                responseMimeType = jsonFormat ? "application/json" : "text/plain"
            }
        };

        HttpResponseMessage response;
        string body;

        try
        {
            response = await _httpClient.PostAsJsonAsync(requestUrl, payload, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Gemini chat request timed out.");
            throw new Exception("Gemini API timeout. Kiem tra ket noi toi Google AI Studio API.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini chat request could not connect.");
            throw new Exception("Khong ket noi duoc Gemini API. Kiem tra internet, firewall va Gemini:BaseUrl.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini chat request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                throw new Exception($"Gemini API khong phan hoi thanh cong ({(int)response.StatusCode} {response.StatusCode}): {Truncate(body, 500)}");
            }

            try
            {
                using var document = JsonDocument.Parse(body);

                if (!document.RootElement.TryGetProperty("candidates", out var candidatesElement) ||
                    candidatesElement.ValueKind != JsonValueKind.Array ||
                    candidatesElement.GetArrayLength() == 0)
                {
                    throw new Exception("Gemini API tra ve response khong co candidates.");
                }

                var firstCandidate = candidatesElement[0];
                if (!firstCandidate.TryGetProperty("content", out var contentElement) ||
                    !contentElement.TryGetProperty("parts", out var partsElement) ||
                    partsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new Exception("Gemini API tra ve response khong hop le.");
                }

                var texts = partsElement
                    .EnumerateArray()
                    .Where(part => part.TryGetProperty("text", out _))
                    .Select(part => part.GetProperty("text").GetString())
                    .Where(text => !string.IsNullOrWhiteSpace(text));

                return string.Join("\n", texts).Trim();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Gemini response: {Body}", body);
                throw new Exception("Gemini API tra ve JSON khong hop le.");
            }
        }
    }

    public async Task<string?> DownloadImageAsBase64Async(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        var optimizedImageUrl = OptimizeCloudinaryImageUrl(imageUrl);
        if (!Uri.TryCreate(optimizedImageUrl, UriKind.Absolute, out var uri) ||
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

    private string GetApiKey()
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("Chua cau hinh Gemini:ApiKey.");
        }

        return apiKey;
    }

    private static string OptimizeCloudinaryImageUrl(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Contains("/image/upload/", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        const string marker = "/image/upload/";
        const string transform = "f_jpg,q_auto:eco,w_768,c_limit/";
        var absolutePath = uri.AbsolutePath;
        var markerIndex = absolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            return imageUrl;
        }

        var insertIndex = markerIndex + marker.Length;
        var newPath = absolutePath.Insert(insertIndex, transform);
        var builder = new UriBuilder(uri)
        {
            Path = newPath
        };

        return builder.Uri.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}