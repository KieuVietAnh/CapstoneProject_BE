using System.Text.Json;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiFeedbackDraftService : IAiFeedbackDraftService
{
    private const int MaxReflectionChars = 1200;
    private const int MaxLocationChars = 300;

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
        var images = request.Base64Images
            .Where(image => !string.IsNullOrWhiteSpace(image))
            .ToArray();

        var rawResponse = await _aiClient.ChatAsync(
            prompt,
            images.Length > 0 ? images : null,
            jsonFormat: true,
            cancellationToken);

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
Bạn là trợ lý AI của hệ thống UrbanService.
Nhiệm vụ: tạo một bản nháp phản ánh đô thị từ thông tin người dân cung cấp.

Thông tin đầu vào:
- Nội dung người dân mô tả: {{reflectionText}}
- Vị trí dạng chữ: {{locationText}}
- Tọa độ: {{coordinateText}}
- Có ảnh minh chứng: {{(hasImages ? "Có" : "Không")}}

Yêu cầu:
1. Chỉ trả về JSON hợp lệ, không markdown, không giải thích ngoài JSON.
2. Không bịa đặt vị trí, tọa độ hoặc thông tin không có trong đầu vào.
3. Nếu thiếu thông tin quan trọng, liệt kê trong missingFields.
4. title ngắn gọn, rõ vấn đề.
5. description viết lại phản ánh lịch sự, đầy đủ, dùng tiếng Việt.
6. suggestedCategory là nhóm vấn đề đô thị phù hợp nếu suy luận được.
7. urgencyLevel chỉ dùng một trong các giá trị: Low, Medium, High, Urgent.
8. Nếu ảnh giúp nhận diện vấn đề, dùng ảnh để bổ sung mô tả nhưng không khẳng định quá mức.

Schema JSON:
{
  "title": "string",
  "description": "string",
  "location": "string|null",
  "latitude": number|null,
  "longitude": number|null,
  "suggestedCategory": "string|null",
  "urgencyLevel": "Low|Medium|High|Urgent|null",
  "summary": "string|null",
  "missingFields": ["string"],
  "confirmationMessage": "string"
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
