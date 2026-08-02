using System.Collections.Concurrent;
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
    private const long DuplicateClassificationLockNamespace = 0x4455504C00000000L;
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> AreaClassificationLocks = new();

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
        var initialIsMasterTicket = feedback.IsMasterTicket;
        var areaLock = AreaClassificationLocks.GetOrAdd(feedback.AreaId, _ => new SemaphoreSlim(1, 1));
        await areaLock.WaitAsync();
        var transactionStarted = false;

        try
        {
            _uow.BeginTransaction();
            transactionStarted = true;
            await _uow.AcquireTransactionAdvisoryLockAsync(GetAreaAdvisoryLockKey(feedback.AreaId));

            await ClassifyUnderAreaLockAsync(feedback);

            _uow.CommitTransaction();
            transactionStarted = false;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                try
                {
                    _uow.RollBack();
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "Failed to roll back duplicate classification transaction for feedback {FeedbackId}.",
                        feedback.FeedbackId);
                }
            }

            // Rollback clears EF tracking, but the worker still owns this object reference.
            // Keep it unresolved so the existing Submitted queue will retry classification.
            feedback.IsMasterTicket = initialIsMasterTicket;
            _logger.LogWarning(
                ex,
                "AI duplicate check failed for feedback {FeedbackId}. Feedback creation will continue.",
                feedback.FeedbackId);
        }
        finally
        {
            areaLock.Release();
        }
    }

    private async Task ClassifyUnderAreaLockAsync(Feedback feedback)
    {
        var currentState = await _uow.GetRepository<Feedback>().Entities
            .Where(current => current.FeedbackId == feedback.FeedbackId)
            .Select(current => new
            {
                current.IsMasterTicket,
                current.ParentTicketId
            })
            .FirstOrDefaultAsync();

        if (currentState is null ||
            currentState.IsMasterTicket ||
            currentState.ParentTicketId.HasValue)
        {
            return;
        }

        if (await HasOlderUnresolvedFeedbackAsync(feedback))
        {
            _logger.LogInformation(
                "Deferred duplicate classification for feedback {FeedbackId} because an older unresolved feedback exists in area {AreaId}.",
                feedback.FeedbackId,
                feedback.AreaId);
            return;
        }

        if (!feedback.Latitude.HasValue || !feedback.Longitude.HasValue)
        {
            await PromoteToMasterIfUnresolvedAsync(feedback);
            return;
        }

        var nearbyCandidates = await FindNearbyCandidatesAsync(feedback);

        if (nearbyCandidates.Count == 0)
        {
            await PromoteToMasterIfUnresolvedAsync(feedback);
            return;
        }

        var prompt = BuildDuplicatePrompt(feedback, nearbyCandidates);
        var rawResponse = await _aiClient.ChatAsync(prompt, jsonFormat: true);
        var result = ParseDuplicateResult(rawResponse);

        if (!result.IsDuplicate)
        {
            await PromoteToMasterIfUnresolvedAsync(feedback);
            return;
        }

        if (!result.ParentFeedbackId.HasValue)
        {
            _logger.LogWarning(
                "AI marked feedback {FeedbackId} as duplicate without a parentFeedbackId.",
                feedback.FeedbackId);
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
        var activeCandidate = await duplicateCandidateRepository.Entities
            .FirstOrDefaultAsync(candidate =>
                candidate.FeedbackId == feedback.FeedbackId &&
                (candidate.Status == "Pending" || candidate.Status == "Confirmed"));

        if (activeCandidate is not null)
        {
            // A feedback may have only one actionable duplicate relation. In particular,
            // a retry must not create a competing parent while staff is reviewing one.
            return;
        }

        var existingCandidate = await duplicateCandidateRepository.Entities
            .FirstOrDefaultAsync(candidate =>
                candidate.FeedbackId == feedback.FeedbackId &&
                candidate.PotentialParentFeedbackId == parentFeedback.FeedbackId);

        if (existingCandidate is not null)
        {
            // A staff rejection is authoritative. Do not silently reopen the same pair;
            // with no other active candidate, this feedback becomes its own master.
            if (existingCandidate.Status == "Rejected")
            {
                await PromoteToMasterIfUnresolvedAsync(feedback);
            }

            return;
        }

        if (!await IsValidCanonicalMasterAsync(feedback, parentFeedback.FeedbackId))
        {
            _logger.LogWarning(
                "AI duplicate parent {ParentFeedbackId} is no longer a valid canonical master for feedback {FeedbackId}.",
                parentFeedback.FeedbackId,
                feedback.FeedbackId);
            return;
        }

        var feedbackRepository = _uow.GetRepository<Feedback>();
        var childIsStillUnresolved = await feedbackRepository.Entities.AnyAsync(current =>
            current.FeedbackId == feedback.FeedbackId &&
            !current.IsMasterTicket &&
            current.ParentTicketId == null);

        if (!childIsStillUnresolved)
        {
            return;
        }

        var now = DateTime.UtcNow;
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

        await _uow.SaveAsync();
    }

    private static long GetAreaAdvisoryLockKey(int areaId)
    {
        return DuplicateClassificationLockNamespace | (uint)areaId;
    }

    private async Task<bool> HasOlderUnresolvedFeedbackAsync(Feedback feedback)
    {
        var excludedStatuses = new[]
        {
            FeedbackStatus.Closed,
            FeedbackStatus.Cancelled,
            FeedbackStatus.Rejected
        };

        var activeCandidateFeedbackIds = _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .Where(candidate => candidate.Status == "Pending" || candidate.Status == "Confirmed")
            .Select(candidate => candidate.FeedbackId);

        var unresolvedFeedbacks = await _uow.GetRepository<Feedback>().Entities
            .Where(candidate =>
                candidate.FeedbackId != feedback.FeedbackId &&
                candidate.AreaId == feedback.AreaId &&
                candidate.CreatedAt <= feedback.CreatedAt &&
                !candidate.IsMasterTicket &&
                candidate.ParentTicketId == null &&
                !excludedStatuses.Contains(candidate.Status) &&
                !activeCandidateFeedbackIds.Contains(candidate.FeedbackId))
            .ToListAsync();

        return unresolvedFeedbacks.Any(candidate => IsOlderThan(candidate, feedback));
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
                f.CreatedAt <= feedback.CreatedAt &&
                f.IsMasterTicket &&
                f.ParentTicketId == null &&
                f.Latitude.HasValue &&
                f.Longitude.HasValue &&
                !excludedStatuses.Contains(f.Status))
            .OrderByDescending(f => f.CreatedAt)
            .Take(100)
            .ToListAsync();

        return candidates
            .Where(candidate => IsOlderThan(candidate, feedback))
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

    private async Task PromoteToMasterIfUnresolvedAsync(Feedback feedback)
    {
        var feedbackRepository = _uow.GetRepository<Feedback>();
        var currentFeedback = await feedbackRepository.Entities
            .FirstOrDefaultAsync(current => current.FeedbackId == feedback.FeedbackId);

        if (currentFeedback is null ||
            currentFeedback.IsMasterTicket ||
            currentFeedback.ParentTicketId.HasValue)
        {
            return;
        }

        var hasActiveCandidate = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AnyAsync(candidate =>
                candidate.FeedbackId == feedback.FeedbackId &&
                (candidate.Status == "Pending" || candidate.Status == "Confirmed"));

        if (hasActiveCandidate)
        {
            return;
        }

        currentFeedback.IsMasterTicket = true;
        currentFeedback.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _uow.SaveAsync();
        }
        catch
        {
            currentFeedback.IsMasterTicket = false;
            throw;
        }
    }

    private async Task<bool> IsValidCanonicalMasterAsync(Feedback childFeedback, Guid parentFeedbackId)
    {
        var excludedStatuses = new[]
        {
            FeedbackStatus.Closed,
            FeedbackStatus.Cancelled,
            FeedbackStatus.Rejected
        };

        var parentFeedback = await _uow.GetRepository<Feedback>().Entities
            .FirstOrDefaultAsync(parent =>
                parent.FeedbackId == parentFeedbackId &&
                parent.FeedbackId != childFeedback.FeedbackId &&
                parent.AreaId == childFeedback.AreaId &&
                parent.IsMasterTicket &&
                parent.ParentTicketId == null &&
                parent.Latitude.HasValue &&
                parent.Longitude.HasValue &&
                !excludedStatuses.Contains(parent.Status));

        return parentFeedback is not null && IsOlderThan(parentFeedback, childFeedback);
    }

    private static bool IsOlderThan(Feedback candidate, Feedback feedback)
    {
        return candidate.CreatedAt < feedback.CreatedAt ||
            (candidate.CreatedAt == feedback.CreatedAt &&
             string.CompareOrdinal(
                 candidate.FeedbackId.ToString("D"),
                 feedback.FeedbackId.ToString("D")) < 0);
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
