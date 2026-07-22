using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiFeedbackAnalysisService : IAiFeedbackAnalysisService
{
    private readonly IUnitOfWork _uow;
    private readonly IAiClient _aiClient;
    private readonly ILogger<AiFeedbackAnalysisService> _logger;
    private readonly int _maxImagesPerFeedback;

    public AiFeedbackAnalysisService(
        IUnitOfWork uow,
        IAiClient aiClient,
        IConfiguration configuration,
        ILogger<AiFeedbackAnalysisService> logger)
    {
        _uow = uow;
        _aiClient = aiClient;
        _logger = logger;
        _maxImagesPerFeedback = int.TryParse(configuration["AI:MaxImagesPerFeedback"], out var maxImages)
            ? Math.Clamp(maxImages, 0, 3)
            : 0;
    }

    public async Task<AiAnalysisResponseDto> AnalyzeFeedbackAsync(
        Guid feedbackId,
        Guid reviewedByUserId,
        CancellationToken cancellationToken = default)
    {
        var feedback = await _uow.GetRepository<Feedback>().Entities
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.AnalysisResults)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId, cancellationToken)
            ?? throw new Exception("Khong tim thay feedback.");

        if (!string.Equals(feedback.Status, FeedbackStatus.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Only Submitted feedback can be reviewed by AI.");
        }

        var images = new List<string>();
        foreach (var attachment in feedback.FeedbackAttachments.Where(IsImageAttachment).Take(_maxImagesPerFeedback))
        {
            var base64 = await _aiClient.DownloadImageAsBase64Async(attachment.FileUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(base64))
            {
                images.Add(base64);
            }
        }

        var activeCategories = await GetActiveCategoriesAsync(cancellationToken);
        var prompt = BuildAnalysisPrompt(feedback, activeCategories, images.Count > 0);
        var rawResponse = await ChatWithFallbackAsync(feedback.FeedbackId, prompt, images, cancellationToken);
        var parsed = ParseAnalysis(rawResponse);
        var detectedCategory = FindDetectedCategory(parsed.DetectedCategoryName, activeCategories);

        _uow.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var analysisResult = new AnalysisResult
            {
                FeedbackId = feedback.FeedbackId,
                ModelName = _aiClient.ModelName,
                DetectedCategoryId = detectedCategory?.CategoryId,
                Sentiment = parsed.Sentiment,
                UrgencyLevel = parsed.UrgencyLevel,
                Summary = Truncate(parsed.Summary, 500),
                Keywords = Truncate(JsonSerializer.Serialize(parsed.Keywords ?? []), 500),
                ConfidenceScore = parsed.ConfidenceScore,
                RawResponse = NormalizeJsonForJsonb(rawResponse),
                CreatedAt = now
            };

            await _uow.GetRepository<AnalysisResult>().AddAsync(analysisResult);

            feedback.CategoryId = detectedCategory?.CategoryId
                ?? throw new Exception("AI review khong xac dinh duoc category hop le cho feedback.");

            feedback.Priority = NormalizeUrgencyAsPriority(parsed.UrgencyLevel)
                ?? throw new Exception("AI review khong xac dinh duoc priority hop le cho feedback.");

            if (!string.Equals(feedback.Status, FeedbackStatus.AiReviewed, StringComparison.OrdinalIgnoreCase))
            {
                var oldStatus = feedback.Status;
                feedback.Status = FeedbackStatus.AiReviewed;
                feedback.UpdatedAt = now;

                await _uow.GetRepository<FeedbackStatusHistory>().AddAsync(new FeedbackStatusHistory
                {
                    FeedbackId = feedback.FeedbackId,
                    ChangedByUserId = reviewedByUserId,
                    OldStatus = oldStatus,
                    NewStatus = FeedbackStatus.AiReviewed,
                    Note = $"Reviewed by AI using {_aiClient.ModelName}",
                    ChangedAt = now
                });
            }

            await _uow.SaveAsync();
            _uow.CommitTransaction();

            return new AiAnalysisResponseDto
            {
                AnalysisResultId = analysisResult.AnalysisResultId,
                FeedbackId = analysisResult.FeedbackId,
                ModelName = analysisResult.ModelName,
                DetectedCategoryId = analysisResult.DetectedCategoryId,
                DetectedCategoryName = detectedCategory?.CategoryName,
                Sentiment = analysisResult.Sentiment,
                UrgencyLevel = analysisResult.UrgencyLevel,
                Summary = analysisResult.Summary,
                Keywords = parsed.Keywords ?? [],
                ConfidenceScore = analysisResult.ConfidenceScore,
                RawResponse = analysisResult.RawResponse,
                CreatedAt = analysisResult.CreatedAt
            };
        }
        catch
        {
            _uow.RollBack();
            throw;
        }
    }

    private async Task<IReadOnlyCollection<UrbanServiceCategory>> GetActiveCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    private static UrbanServiceCategory? FindDetectedCategory(
        string? categoryName,
        IReadOnlyCollection<UrbanServiceCategory> activeCategories)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var normalized = NormalizeForMatching(categoryName);
        return activeCategories.FirstOrDefault(c =>
            NormalizeForMatching(c.CategoryName) == normalized);
    }

    private static bool IsImageAttachment(FeedbackAttachment attachment)
    {
        if (attachment.FileType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var url = attachment.FileUrl.ToLower();
        return url.EndsWith(".jpg") || url.EndsWith(".jpeg") || url.EndsWith(".png") || url.EndsWith(".webp");
    }

    private async Task<string> ChatWithFallbackAsync(
        Guid feedbackId,
        string prompt,
        IReadOnlyCollection<string> images,
        CancellationToken cancellationToken)
    {
        if (images.Count == 0)
        {
            return await _aiClient.ChatAsync(prompt, jsonFormat: true, cancellationToken: cancellationToken);
        }

        try
        {
            return await _aiClient.ChatAsync(prompt, images, jsonFormat: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "AI vision review failed for feedback {FeedbackId}. Retrying with text-only prompt.",
                feedbackId);

            return await _aiClient.ChatAsync(
                prompt,
                jsonFormat: true,
                cancellationToken: cancellationToken);
        }
    }

    private static string BuildAnalysisPrompt(
        Feedback feedback,
        IReadOnlyCollection<UrbanServiceCategory> activeCategories,
        bool hasImages)
    {
        var categoryList = activeCategories.Count == 0
            ? "- Khong co category active trong he thong"
            : string.Join(
                Environment.NewLine,
                activeCategories.Select(c =>
                    $"- {c.CategoryName}{(string.IsNullOrWhiteSpace(c.Description) ? string.Empty : $": {c.Description}")}"));

        return $$"""
        Ban la he thong phan tich phan anh do thi cho UrbanService.
        Hay phan tich feedback cua nguoi dan dua tren text{{(hasImages ? " va anh dinh kem" : "")}}.
        Tat ca noi dung text do AI sinh ra phai bang tieng Viet co dau.
        Nhiem vu bat buoc:
        1. Chon dung 1 category phu hop nhat tu danh sach category active ben duoi.
        2. Gan muc uu tien/priority dua tren muc do khan cap cua feedback.
        3. detectedCategoryName phai trung khop chinh xac voi mot CategoryName trong danh sach.
        4. urgencyLevel phai la mot trong cac gia tri: Low, Medium, High, Critical.

        Danh sach category active:
        {{categoryList}}

        Quy tac priority:
        - Low: van de nho, it anh huong, khong can xu ly gap.
        - Medium: anh huong binh thuong, can xu ly theo lich.
        - High: anh huong nhieu nguoi/khu vuc, can uu tien xu ly som.
        - Critical: nguy hiem, mat an toan, su co nghiem trong, can xu ly khan cap.

        Feedback:
        - Tieu de: {{feedback.Title}}
        - Mo ta: {{feedback.Description}}
        - Dia diem: {{feedback.LocationText}}
        - Muc uu tien hien tai: {{feedback.Priority ?? "Chua co"}}
        - Category hien tai: {{feedback.Category?.CategoryName ?? "Chua co"}}

        Tra ve dung JSON:
        {
          "detectedCategoryName": string,
          "sentiment": "Positive" | "Neutral" | "Negative",
          "urgencyLevel": "Low" | "Medium" | "High" | "Critical",
          "summary": string,
          "keywords": string[],
          "confidenceScore": number,
          "riskNotes": string[]
        }

        Khong duoc them giai thich ngoai JSON.
        Bat buoc detectedCategoryName khong duoc null neu danh sach category active co du lieu.
        Cac field summary, keywords va riskNotes phai viet bang tieng Viet.
        {{(hasImages ? "Neu anh khong ro hoac khong lien quan, ghi ro trong riskNotes." : "Khong co anh dinh kem trong request nay, chi phan tich dua tren text.")}}
        """;
    }

    private static string NormalizeForMatching(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeUrgencyAsPriority(string? urgencyLevel)
    {
        if (string.IsNullOrWhiteSpace(urgencyLevel))
        {
            return null;
        }

        return urgencyLevel.Trim() switch
        {
            var value when string.Equals(value, "Low", StringComparison.OrdinalIgnoreCase) => "Low",
            var value when string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
            var value when string.Equals(value, "High", StringComparison.OrdinalIgnoreCase) => "High",
            var value when string.Equals(value, "Critical", StringComparison.OrdinalIgnoreCase) => "Critical",
            _ => null
        };
    }

    private static ParsedAnalysis ParseAnalysis(string rawResponse)
    {
        var json = ExtractJson(rawResponse);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ParsedAnalysis
        {
            DetectedCategoryName = GetString(root, "detectedCategoryName"),
            Sentiment = GetString(root, "sentiment"),
            UrgencyLevel = GetString(root, "urgencyLevel"),
            Summary = GetString(root, "summary"),
            Keywords = root.TryGetProperty("keywords", out var keywords) && keywords.ValueKind == JsonValueKind.Array
                ? keywords.EnumerateArray()
                    .Select(k => k.GetString())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => k!)
                    .ToList()
                : [],
            ConfidenceScore = GetDecimal(root, "confidenceScore")
        };
    }

    private static string NormalizeJsonForJsonb(string rawResponse)
    {
        var json = ExtractJson(rawResponse);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetRawText();
    }

    private static string ExtractJson(string value)
    {
        var trimmed = value.Trim();
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

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var result)
            ? Math.Clamp(result, 0m, 1m)
            : null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private sealed class ParsedAnalysis
    {
        public string? DetectedCategoryName { get; set; }

        public string? Sentiment { get; set; }

        public string? UrgencyLevel { get; set; }

        public string? Summary { get; set; }

        public List<string>? Keywords { get; set; }

        public decimal? ConfidenceScore { get; set; }
    }
}
