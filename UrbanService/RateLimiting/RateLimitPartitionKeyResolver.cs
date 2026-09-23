using System.Security.Claims;

namespace UrbanService.RateLimiting;

public static class RateLimitPartitionKeyResolver
{
    public static string Resolve(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
