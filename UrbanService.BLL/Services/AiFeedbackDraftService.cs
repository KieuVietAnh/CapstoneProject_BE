using System.Text.Json;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiFeedbackDraftService : IAiFeedbackDraftService
{
    private const int MaxReflectionChars = 700;
    private const int MaxLocationChars = 180;

    private readonly IAiClient _aiClient;

    public AiFeedbackDraftService(IAiClient aiClient)
    {
        _aiClient = aiClient;
    }

    public async Task<AiFeedbackDraftResponse> CreateDraftAsync(
        Guid userId,
        AiFeedbackDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = userId;

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reflection))
        {
            throw new ArgumentException("Reflection is required.", nameof(request));
        }

        var prompt = BuildPrompt(request);

        // Do not send base64 image payloads for draft generation.
        // Small local VLM models commonly have a 4096-token context window, and even one image
        // can push an otherwise short draft prompt over that limit. The draft only needs the
        // citizen-provided text/location plus whether evidence exists; the uploaded image URLs
        // are preserved on the returned draft for submission.
        var rawResponse = await _aiClient.ChatAsync(
            prompt,
            jsonFormat: true,
            cancellationToken: cancellationToken);

        var draft = ParseDraft(rawResponse, request);

        draft.Location ??= request.Location;
        draft.Latitude ??= request.Latitude;
        draft.Longitude ??= request.Longitude;
        draft.ImageUrls = request.ImageUrls;
        draft.ConfirmationMessage = string.IsNullOrWhiteSpace(draft.ConfirmationMessage)
            ? "Tôi đã tạo bản nháp phản ánh từ thông tin bạn cung cấp. Vui lòng kiểm tra lại trước khi gửi."
            : draft.ConfirmationMessage;

        return draft;
    }

    private static string BuildPrompt(AiFeedbackDraftRequest request)
    {
        var locationText = string.IsNullOrWhiteSpace(request.Location)
            ? "Chưa cung cấp"
            : Truncate(request.Location.Trim(), MaxLocationChars);

        var coordinateText = request.Latitude.HasValue && request.Longitude.HasValue
            ? $"{request.Latitude}, {request.Longitude}"
            : "Chưa cung cấp";

        var hasImages = request.ImageUrls.Count > 0 || request.Base64Images.Count > 0;

        var reflectionText = Truncate(request.Reflection.Trim(), MaxReflectionChars);

        return $$"""
Tạo bản nháp phản ánh đô thị bằng tiếng Việt. Chỉ trả JSON hợp lệ, không markdown.

Đầu vào:
- Mô tả: {{reflectionText}}
- Vị trí: {{locationText}}
- Tọa độ: {{coordinateText}}
- Có ảnh minh chứng: {{(hasImages ? "Có" : "Không")}}

Quy tắc:
- Không bịa thông tin thiếu.
- missingFields liệt kê thông tin còn thiếu nếu cần.
- urgencyLevel chỉ là Low, Medium, High hoặc Urgent.

JSON:
{
  "title": "",
  "description": "",
  "location": null,
  "latitude": null,
  "longitude": null,
  "suggestedCategory": null,
  "urgencyLevel": null,
  "summary": null,
  "missingFields": [],
  "confirmationMessage": ""
}
""";
    }

    private static AiFeedbackDraftResponse ParseDraft(string rawResponse, AiFeedbackDraftRequest request)
    {
        var json = ExtractJson(rawResponse);

        var draft = JsonSerializer.Deserialize<AiFeedbackDraftResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (draft is null)
        {
            throw new InvalidOperationException("AI did not return a valid feedback draft.");
        }

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            draft.Title = CreateFallbackTitle(request.Reflection);
        }

        if (string.IsNullOrWhiteSpace(draft.Description))
        {
            draft.Description = request.Reflection.Trim();
        }

        return draft;
    }

    private static string ExtractJson(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new InvalidOperationException("AI returned an empty response.");
        }

        var trimmed = rawResponse.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        return trimmed;
    }

    private static string CreateFallbackTitle(string reflection)
    {
        var normalized = reflection.Trim();

        if (normalized.Length <= 80)
        {
            return normalized;
        }

        return normalized[..77] + "...";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
