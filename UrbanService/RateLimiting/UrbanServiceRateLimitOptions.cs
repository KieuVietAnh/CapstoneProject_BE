namespace UrbanService.RateLimiting;

public sealed class UrbanServiceRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule GlobalAuthenticated { get; set; } = new(120, 1);
    public RateLimitRule GlobalAnonymous { get; set; } = new(60, 1);
    public RateLimitRule AuthAttempt { get; set; } = new(5, 1);
    public RateLimitRule Otp { get; set; } = new(3, 10);
    public RateLimitRule FeedbackSubmission { get; set; } = new(5, 10);
    public RateLimitRule UserWrite { get; set; } = new(30, 1);
    public RateLimitRule AiUsage { get; set; } = new(10, 1);
}

public sealed class RateLimitRule
{
    public RateLimitRule(int permitLimit, int windowMinutes)
    {
        PermitLimit = permitLimit;
        WindowMinutes = windowMinutes;
    }

    public int PermitLimit { get; set; }
    public int WindowMinutes { get; set; }
}
