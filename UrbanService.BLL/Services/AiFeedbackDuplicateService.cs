using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiFeedbackDuplicateService : IAiFeedbackDuplicateService
{
    private readonly IUnitOfWork _uow;
    private readonly IAiClient _aiClient;
    private readonly ILogger<AiFeedbackDuplicateService> _logger;
    private readonly double _nearbyRadiusMeters;
    private readonly int _maxCandidates;

    public AiFeedbackDuplicateService(
        IUnitOfWork uow,
        IAiClient aiClient,
        IConfiguration configuration,
        ILogger<AiFeedbackDuplicateService> logger)
    {
        _uow = uow;
        _aiClient = aiClient;
        _logger = logger;
        _nearbyRadiusMeters = double.TryParse(configuration["AI:DuplicateNearbyRadiusMeters"], out var radiusMeters)
            ? Math.Clamp(radiusMeters, 10d, 5000d)
            : 200d;
        _maxCandidates = int.TryParse(configuration["AI:DuplicateMaxCandidates"], out var maxCandidates)
            ? Math.Clamp(maxCandidates, 1, 10)
            : 5;
    }

    public async Task CheckAndLinkDuplicateAsync(Feedback feedback, Guid reviewedByUserId)
    {
        if (!feedback.Latitude.HasValue || !feedback.Longitude.HasValue)
        {
            return;
        }

        var nearbyCandidates = await FindNearbyCandidatesAsync(feedback);

        if (nearbyCandidates.Count == 0)
        {
            return;
        }

        try
        {
            var prompt = BuildDuplicatePrompt(feedback, nearbyCandidates);
            var rawResponse = await _aiClient.ChatAsync(prompt, jsonFormat: true);
            var result = ParseDuplicateResult(rawResponse);

            if (!result.IsDuplicate || !result.ParentFeedbackId.HasValue)
            {
                return;
            }

            var parentFeedbackId = result.ParentFeedbackId.Value;
            var parentFeedback = nearbyCandidates
                .Select(c => c.Feedback)
                .FirstOrDefault(f => f.FeedbackId == parentFeedbackId);

            if (parentFeedback is null)
            {
                _logger.LogWarning(
                    "AI duplicate result for feedback {FeedbackId} returned invalid parentFeedbackId {ParentFeedbackId}.",
                    feedback.FeedbackId,
                    parentFeedbackId);
                return;
            }

            var duplicateCandidateRepository = _uow.GetRepository<FeedbackDuplicateCandidate>();
            var existingCandidate = await duplicateCandidateRepository.Entities
                .FirstOrDefaultAsync(candidate =>
                    candidate.FeedbackId == feedback.FeedbackId &&
                    candidate.PotentialParentFeedbackId == parentFeedback.FeedbackId);

            var now = DateTime.UtcNow;
            if (existingCandidate is null)
            {
                await duplicateCandidateRepository.AddAsync(new FeedbackDuplicateCandidate
                {
                    FeedbackId = feedback.FeedbackId,
                    PotentialParentFeedbackId = parentFeedback.FeedbackId,
                    Status = "Pending",
                    ConfidenceScore = result.ConfidenceScore,
                    Reason = result.Reason,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else if (existingCandidate.Status == "Rejected")
            {
                existingCandidate.Status = "Pending";
                existingCandidate.ConfidenceScore = result.ConfidenceScore;
                existingCandidate.Reason = result.Reason;
                existingCandidate.ReviewedByUserId = null;
                existingCandidate.ReviewedAt = null;
                existingCandidate.UpdatedAt = now;
            }

            await _uow.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "AI duplicate check failed for feedback {FeedbackId}. Feedback creation will continue.",
                feedback.FeedbackId);
        }
    }

    private async Task<IReadOnlyCollection<NearbyFeedbackCandidate>> FindNearbyCandidatesAsync(Feedback feedback)
    {
        var excludedStatuses = new[]
        {
            FeedbackStatus.Closed,
            FeedbackStatus.Cancelled,
            FeedbackStatus.Rejected
        };

        var candidates = await _uow.GetRepository<Feedback>().Entities
            .Where(f =>
                f.FeedbackId != feedback.FeedbackId &&
                f.AreaId == feedback.AreaId &&
                f.Latitude.HasValue &&
                f.Longitude.HasValue &&
                !excludedStatuses.Contains(f.Status))
            .OrderByDescending(f => f.CreatedAt)
            .Take(100)
            .ToListAsync();

        return candidates
            .Select(candidate => new NearbyFeedbackCandidate(
                candidate,
                CalculateDistanceMeters(
                    (double)feedback.Latitude!.Value,
                    (double)feedback.Longitude!.Value,
                    (double)candidate.Latitude!.Value,
                    (double)candidate.Longitude!.Value)))
            .Where(candidate => candidate.DistanceMeters <= _nearbyRadiusMeters)
            .OrderBy(candidate => candidate.DistanceMeters)
            .Take(_maxCandidates)
            .ToList();
    }

    private static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusMeters = 6371000d;

        var latitudeDistance = ToRadians(latitude2 - latitude1);
        var longitudeDistance = ToRadians(longitude2 - longitude1);

        var a = Math.Sin(latitudeDistance / 2) * Math.Sin(latitudeDistance / 2)
            + Math.Cos(ToRadians(latitude1))
            * Math.Cos(ToRadians(latitude2))
            * Math.Sin(longitudeDistance / 2)
            * Math.Sin(longitudeDistance / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private string BuildDuplicatePrompt(
        Feedback newFeedback,
        IReadOnlyCollection<NearbyFeedbackCandidate> nearbyCandidates)
    {
        var candidateList = string.Join(
            Environment.NewLine,
            nearbyCandidates.Select(candidate =>
                $$"""
                Candidate:
                - feedbackId: {{candidate.Feedback.FeedbackId}}
                - title: {{candidate.Feedback.Title}}
                - description: {{candidate.Feedback.Description}}
                - locationText: {{candidate.Feedback.LocationText}}
                - status: {{candidate.Feedback.Status}}
                - distanceMeters: {{Math.Round(candidate.DistanceMeters, 2)}}
                - createdAt: {{candidate.Feedback.CreatedAt:O}}
                """));

        return $$"""
        Ban la he thong phat hien phan anh do thi bi trung lap cho UrbanService.
        He thong da loc truoc cac phan anh gan nhau theo toa do trong ban kinh {{_nearbyRadiusMeters}} met.
        Hay quyet dinh feedback moi co trung voi mot feedback cu nao khong.

        Quy tac:
        - Chi ket luan trung neu cung mot su co/van de thuc te tai cung khu vuc gan nhau.
        - Neu chi gan vi tri nhung noi dung khac nhau thi khong trung.
        - Neu noi dung mo ta cung van de nhung cach dien dat khac nhau thi co the la trung.
        - Neu trung, chon duy nhat mot feedback cu phu hop nhat lam parentFeedbackId.
        - parentFeedbackId bat buoc phai nam trong danh sach candidate.
        - Tat ca reason phai viet tieng Viet co dau.

        Feedback moi:
        - feedbackId: {{newFeedback.FeedbackId}}
        - title: {{newFeedback.Title}}
        - description: {{newFeedback.Description}}
        - locationText: {{newFeedback.LocationText}}
        - latitude: {{newFeedback.Latitude}}
        - longitude: {{newFeedback.Longitude}}
        - createdAt: {{newFeedback.CreatedAt:O}}

        Danh sach feedback gan:
        {{candidateList}}

        Tra ve dung JSON:
        {
          "isDuplicate": boolean,
          "parentFeedbackId": string | null,
          "confidenceScore": number,
          "reason": string
        }

        Khong duoc them giai thich ngoai JSON.
        """;
    }

    private static ParsedDuplicateResult ParseDuplicateResult(string rawResponse)
    {
        var json = ExtractJson(rawResponse);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ParsedDuplicateResult
        {
            IsDuplicate = root.TryGetProperty("isDuplicate", out var isDuplicate)
                && isDuplicate.ValueKind == JsonValueKind.True,
            ParentFeedbackId = GetGuid(root, "parentFeedbackId"),
            ConfidenceScore = GetDecimal(root, "confidenceScore"),
            Reason = GetString(root, "reason")
        };
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

    private static Guid? GetGuid(JsonElement root, string propertyName)
    {
        var value = GetString(root, propertyName);
        return Guid.TryParse(value, out var result) ? result : null;
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

    private sealed record NearbyFeedbackCandidate(Feedback Feedback, double DistanceMeters);

    private sealed class ParsedDuplicateResult
    {
        public bool IsDuplicate { get; set; }

        public Guid? ParentFeedbackId { get; set; }

        public decimal? ConfidenceScore { get; set; }

        public string? Reason { get; set; }
    }
}