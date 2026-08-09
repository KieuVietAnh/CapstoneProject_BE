using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class MessengerServiceTests
{
    [Fact]
    public void VerificationRequest_WithMatchingToken_IsAccepted()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["Messenger:VerifyToken"] = "verify-me"
        });

        Assert.True(service.IsVerificationRequestValid("subscribe", "verify-me"));
        Assert.False(service.IsVerificationRequestValid("subscribe", "wrong"));
        Assert.False(service.IsVerificationRequestValid("unsubscribe", "verify-me"));
    }

    [Fact]
    public void Signature_WithValidHmac_IsAccepted()
    {
        const string appSecret = "test-app-secret";
        const string payload = "{\"object\":\"page\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var signature = $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";
        var service = CreateService(new Dictionary<string, string?>
        {
            ["Messenger:AppSecret"] = appSecret
        });

        Assert.True(service.IsSignatureValid(payload, signature));
        Assert.False(service.IsSignatureValid(payload + " ", signature));
        Assert.False(service.IsSignatureValid(payload, "sha256=invalid"));
    }

    private static MessengerService CreateService(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new MessengerService(
            new HttpClient(),
            configuration,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IFeedbackService>(),
            NullLogger<MessengerService>.Instance);
    }
}
