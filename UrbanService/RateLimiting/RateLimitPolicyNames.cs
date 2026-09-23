namespace UrbanService.RateLimiting;

public static class RateLimitPolicyNames
{
    public const string AuthAttempt = "auth-attempt";
    public const string Otp = "otp";
    public const string FeedbackSubmission = "feedback-submission";
    public const string UserWrite = "user-write";
    public const string AiUsage = "ai-usage";
}
