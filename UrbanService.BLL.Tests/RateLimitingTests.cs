using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UrbanService.RateLimiting;
using Xunit;

namespace UrbanService.BLL.Tests;

public class RateLimitingTests
{
    [Fact]
    public void PartitionKey_UsesAuthenticatedUserId()
    {
        var context = CreateContext("user-123", IPAddress.Parse("10.0.0.1"));

        var key = RateLimitPartitionKeyResolver.Resolve(context);

        Assert.Equal("user:user-123", key);
    }

    [Fact]
    public void PartitionKey_UsesIpForAnonymousRequest()
    {
        var context = CreateContext(null, IPAddress.Parse("10.0.0.2"));

        var key = RateLimitPartitionKeyResolver.Resolve(context);

        Assert.Equal("ip:10.0.0.2", key);
    }

    [Fact]
    public async Task GlobalLimiter_IsolatesAuthenticatedUsers()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["RateLimiting:GlobalAuthenticated:PermitLimit"] = "2",
            ["RateLimiting:GlobalAuthenticated:WindowMinutes"] = "1"
        });
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>()
            .Value.GlobalLimiter!;
        var firstUser = CreateContext("user-1", IPAddress.Loopback);
        var secondUser = CreateContext("user-2", IPAddress.Loopback);

        using var firstLease = await limiter.AcquireAsync(firstUser);
        using var secondLease = await limiter.AcquireAsync(firstUser);
        using var rejectedLease = await limiter.AcquireAsync(firstUser);
        using var otherUserLease = await limiter.AcquireAsync(secondUser);

        Assert.True(firstLease.IsAcquired);
        Assert.True(secondLease.IsAcquired);
        Assert.False(rejectedLease.IsAcquired);
        Assert.True(otherUserLease.IsAcquired);
    }

    [Fact]
    public void InvalidRule_IsRejectedByOptionsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["RateLimiting:AiUsage:PermitLimit"] = "0"
        });

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<UrbanServiceRateLimitOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUrbanServiceRateLimiting(configuration);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(string? userId, IPAddress address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;

        if (userId != null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "Test"));
        }

        return context;
    }
}
