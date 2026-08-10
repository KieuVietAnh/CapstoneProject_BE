using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using System.Security.Cryptography;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class ZaloAccessTokenProviderTests
{
    [Fact]
    public async Task Bootstrap_EncryptsConfiguredAccessTokenBeforePersistence()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zalo:OaId"] = "oa-1",
                ["Zalo:AccessToken"] = "plain-access-token",
                ["Zalo:AccessTokenExpiresAtUtc"] = DateTime.UtcNow.AddHours(10).ToString("O"),
                ["Zalo:TokenEncryptionKey"] = key
            })
            .Build();
        var repository = Substitute.For<IGenericRepository<ZaloOauthCredential>>();
        repository.Entities.Returns(Array.Empty<ZaloOauthCredential>().AsAsyncQueryable());
        ZaloOauthCredential? storedCredential = null;
        repository.AddAsync(Arg.Any<ZaloOauthCredential>())
            .Returns(callInfo =>
            {
                storedCredential = callInfo.Arg<ZaloOauthCredential>();
                return Task.CompletedTask;
            });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.GetRepository<ZaloOauthCredential>().Returns(repository);
        var provider = new ZaloAccessTokenProvider(
            new HttpClient(),
            configuration,
            unitOfWork,
            new ZaloTokenRefreshLock(),
            NullLogger<ZaloAccessTokenProvider>.Instance);

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("plain-access-token", token);
        Assert.NotNull(storedCredential);
        Assert.NotEqual("plain-access-token", storedCredential.AccessTokenCiphertext);
        Assert.Null(storedCredential.RefreshTokenCiphertext);
        await unitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Bootstrap_WithRefreshToken_RotatesAndPersistsReturnedTokens()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zalo:OaId"] = "oa-1",
                ["Zalo:AppId"] = "app-1",
                ["Zalo:AppSecretKey"] = "secret-1",
                ["Zalo:RefreshToken"] = "initial-refresh-token",
                ["Zalo:TokenEncryptionKey"] = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32))
            })
            .Build();
        var repository = Substitute.For<IGenericRepository<ZaloOauthCredential>>();
        repository.Entities.Returns(Array.Empty<ZaloOauthCredential>().AsAsyncQueryable());
        ZaloOauthCredential? storedCredential = null;
        repository.AddAsync(Arg.Any<ZaloOauthCredential>())
            .Returns(callInfo =>
            {
                storedCredential = callInfo.Arg<ZaloOauthCredential>();
                return Task.CompletedTask;
            });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.GetRepository<ZaloOauthCredential>().Returns(repository);
        var handler = new RecordingOauthHandler();
        var provider = new ZaloAccessTokenProvider(
            new HttpClient(handler),
            configuration,
            unitOfWork,
            new ZaloTokenRefreshLock(),
            NullLogger<ZaloAccessTokenProvider>.Instance);

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("new-access-token", token);
        Assert.Equal("secret-1", handler.SecretKey);
        Assert.Contains("refresh_token=initial-refresh-token", handler.Body);
        Assert.Contains("grant_type=refresh_token", handler.Body);
        Assert.NotNull(storedCredential);
        Assert.NotEqual("new-access-token", storedCredential.AccessTokenCiphertext);
        Assert.NotEqual("new-refresh-token", storedCredential.RefreshTokenCiphertext);
    }

    private sealed class RecordingOauthHandler : HttpMessageHandler
    {
        public string? SecretKey { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SecretKey = request.Headers.TryGetValues("secret_key", out var values)
                ? values.Single()
                : null;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"new-access-token\",\"refresh_token\":\"new-refresh-token\",\"expires_in\":\"90000\"}")
            };
        }
    }
}
