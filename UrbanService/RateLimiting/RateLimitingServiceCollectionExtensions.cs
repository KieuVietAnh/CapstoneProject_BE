using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common;

namespace UrbanService.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddUrbanServiceRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<UrbanServiceRateLimitOptions>()
            .Bind(configuration.GetSection(UrbanServiceRateLimitOptions.SectionName))
            .Validate(HasValidRules, "Mỗi cấu hình rate limit phải có PermitLimit và WindowMinutes lớn hơn 0.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            var limits = configuration
                .GetSection(UrbanServiceRateLimitOptions.SectionName)
                .Get<UrbanServiceRateLimitOptions>() ?? new UrbanServiceRateLimitOptions();

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/swagger"))
                {
                    return RateLimitPartition.GetNoLimiter("swagger");
                }

                var rule = context.User.Identity?.IsAuthenticated == true
                    ? limits.GlobalAuthenticated
                    : limits.GlobalAnonymous;

                return CreatePartition("global", context, rule);
            });

            AddPolicy(options, RateLimitPolicyNames.AuthAttempt, limits.AuthAttempt);
            AddPolicy(options, RateLimitPolicyNames.Otp, limits.Otp);
            AddPolicy(options, RateLimitPolicyNames.FeedbackSubmission, limits.FeedbackSubmission);
            AddPolicy(options, RateLimitPolicyNames.UserWrite, limits.UserWrite);
            AddPolicy(options, RateLimitPolicyNames.AiUsage, limits.AiUsage);

            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("UrbanService.RateLimiting");
                logger.LogWarning(
                    "Rate limit exceeded for {Method} {Path}. Authenticated: {Authenticated}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    context.HttpContext.User.Identity?.IsAuthenticated == true);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                var payload = new ApiResponse<object?>
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Msg = "Bạn thao tác quá nhanh. Vui lòng thử lại sau.",
                    Data = null
                };

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    cancellationToken);
            };
        });

        return services;
    }

    private static void AddPolicy(
        RateLimiterOptions options,
        string policyName,
        RateLimitRule rule)
    {
        options.AddPolicy(policyName, context => CreatePartition(policyName, context, rule));
    }

    private static RateLimitPartition<string> CreatePartition(
        string policyName,
        HttpContext context,
        RateLimitRule rule)
    {
        var partitionKey = $"{policyName}:{RateLimitPartitionKeyResolver.Resolve(context)}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.PermitLimit,
                Window = TimeSpan.FromMinutes(rule.WindowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static bool HasValidRules(UrbanServiceRateLimitOptions options)
    {
        var rules = new[]
        {
            options.GlobalAuthenticated,
            options.GlobalAnonymous,
            options.AuthAttempt,
            options.Otp,
            options.FeedbackSubmission,
            options.UserWrite,
            options.AiUsage
        };

        return rules.All(rule => rule.PermitLimit > 0 && rule.WindowMinutes > 0);
    }
}
