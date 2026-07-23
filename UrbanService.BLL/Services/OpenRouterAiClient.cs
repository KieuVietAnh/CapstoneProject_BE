using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class OpenRouterAiClient : IAiClient
{
    private const string VietnameseSystemInstruction =
        "Luôn trả lời bằng tiếng Việt có dấu. Nếu response là JSON, giữ nguyên tên field/schema được yêu cầu, nhưng mọi giá trị dạng text do AI sinh ra phải bằng tiếng Việt.";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterAiClient> _logger;
    private readonly long _maxImageBytes;
    private readonly double _temperature;

    public OpenRouterAiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouterAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        ModelName = configuration["OpenRouter:Model"] ?? "openai/gpt-4o-mini";
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
            ApplyOpenRouterHeaders();

            var payload = new
            {
                model = ModelName,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "ping"
                    }
                },
                temperature = 0.0,
                max_tokens = 8
            };

            using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter health check failed.");
            return false;
        }
    }

    public async Task<string> ChatAsync(
        string prompt,
        IReadOnlyCollection<string>? base64Images = null,
        bool jsonFormat = false,
        CancellationToken cancellationToken = default)
    {
        ApplyOpenRouterHeaders();

        var userPrompt = jsonFormat
            ? $"{prompt}\n\nChỉ trả về JSON hợp lệ, không bọc markdown, không thêm giải thích ngoài JSON."
            : prompt;

        object userContent = base64Images is { Count: > 0 }
            ? BuildMultimodalContent(userPrompt, base64Images)
            : userPrompt;

        var payload = new
        {
            model = ModelName,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = VietnameseSystemInstruction
                },
                new
                {
                    role = "user",
                    content = userContent
                }
            },
            temperature = _temperature,
            response_format = jsonFormat ? new { type = "json_object" } : null
        };

        HttpResponseMessage response;
        string body;

        try
        {
            response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "OpenRouter chat request timed out.");
            throw new Exception("OpenRouter API timeout. Kiem tra ket noi toi OpenRouter API.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenRouter chat request could not connect.");
            throw new Exception("Khong ket noi duoc OpenRouter API. Kiem tra internet, firewall va OpenRouter:BaseUrl.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouter chat request failed with status {StatusCode}: {Body}", response.StatusCode, body);
                throw new Exception($"OpenRouter API khong phan hoi thanh cong ({(int)response.StatusCode} {response.StatusCode}): {Truncate(body, 500)}");
            }

            try
            {
                using var document = JsonDocument.Parse(body);

                if (!document.RootElement.TryGetProperty("choices", out var choicesElement) ||
                    choicesElement.ValueKind != JsonValueKind.Array ||
                    choicesElement.GetArrayLength() == 0)
                {
                    throw new Exception("OpenRouter API tra ve response khong co choices.");
                }

                var firstChoice = choicesElement[0];
                if (!firstChoice.TryGetProperty("message", out var messageElement) ||
                    !messageElement.TryGetProperty("content", out var contentElement))
                {
                    throw new Exception("OpenRouter API tra ve response khong hop le.");
                }

                return contentElement.GetString()?.Trim()
                    ?? throw new Exception("OpenRouter API tra ve content rong.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenRouter response: {Body}", body);
                throw new Exception("OpenRouter API tra ve JSON khong hop le.");
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

    private void ApplyOpenRouterHeaders()
    {
        var apiKey = _configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("Chua cau hinh OpenRouter:ApiKey.");
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var referer = _configuration["OpenRouter:HttpReferer"];
        if (!string.IsNullOrWhiteSpace(referer))
        {
            _httpClient.DefaultRequestHeaders.Remove("HTTP-Referer");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", referer);
        }

        var title = _configuration["OpenRouter:XTitle"] ?? "UrbanService";
        _httpClient.DefaultRequestHeaders.Remove("X-Title");
        _httpClient.DefaultRequestHeaders.Add("X-Title", title);
    }

    private static object[] BuildMultimodalContent(string prompt, IReadOnlyCollection<string> base64Images)
    {
        var content = new List<object>
        {
            new
            {
                type = "text",
                text = prompt
            }
        };

        content.AddRange(base64Images.Select(base64Image => new
        {
            type = "image_url",
            image_url = new
            {
                url = $"data:image/jpeg;base64,{base64Image}"
            }
        }));

        return content.ToArray();
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