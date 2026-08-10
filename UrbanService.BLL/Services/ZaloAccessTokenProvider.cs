using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class ZaloAccessTokenProvider : IZaloAccessTokenProvider
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _uow;
    private readonly ZaloTokenRefreshLock _refreshLock;
    private readonly ILogger<ZaloAccessTokenProvider> _logger;

    public ZaloAccessTokenProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        IUnitOfWork uow,
        ZaloTokenRefreshLock refreshLock,
        ILogger<ZaloAccessTokenProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _uow = uow;
        _refreshLock = refreshLock;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var oaId = GetRequiredConfiguration("Zalo:OaId");
        var credential = await Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OaId == oaId, cancellationToken);

        if (credential != null && credential.AccessTokenExpiresAt > DateTime.UtcNow.Add(RefreshSkew))
        {
            return Decrypt(credential.AccessTokenCiphertext);
        }

        await _refreshLock.Gate.WaitAsync(cancellationToken);
        try
        {
            credential = await Credentials
                .FirstOrDefaultAsync(item => item.OaId == oaId, cancellationToken);

            if (credential != null && credential.AccessTokenExpiresAt > DateTime.UtcNow.Add(RefreshSkew))
            {
                return Decrypt(credential.AccessTokenCiphertext);
            }

            if (credential == null)
            {
                return await BootstrapCredentialAsync(oaId, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(credential.RefreshTokenCiphertext))
            {
                throw new InvalidOperationException(
                    "Zalo access token has expired and no refresh token is available.");
            }

            var refreshed = await RefreshAsync(
                Decrypt(credential.RefreshTokenCiphertext),
                cancellationToken);
            credential.AccessTokenCiphertext = Encrypt(refreshed.AccessToken);
            credential.RefreshTokenCiphertext = Encrypt(refreshed.RefreshToken);
            credential.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresInSeconds);
            credential.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();

            _logger.LogInformation("Refreshed the Zalo OA access token for OA {OaId}.", oaId);
            return refreshed.AccessToken;
        }
        finally
        {
            _refreshLock.Gate.Release();
        }
    }

    private IQueryable<ZaloOauthCredential> Credentials =>
        _uow.GetRepository<ZaloOauthCredential>().Entities;

    private async Task<string> BootstrapCredentialAsync(
        string oaId,
        CancellationToken cancellationToken)
    {
        var configuredRefreshToken = _configuration["Zalo:RefreshToken"];
        ZaloTokenResult tokenResult;

        if (!string.IsNullOrWhiteSpace(configuredRefreshToken))
        {
            tokenResult = await RefreshAsync(configuredRefreshToken, cancellationToken);
        }
        else
        {
            var accessToken = GetRequiredConfiguration("Zalo:AccessToken");
            var expiresAt = ParseConfiguredExpiry() ?? DateTime.UtcNow.AddHours(20);
            tokenResult = new ZaloTokenResult(accessToken, string.Empty, expiresAt.Subtract(DateTime.UtcNow).TotalSeconds);
        }

        var credential = new ZaloOauthCredential
        {
            OaId = oaId,
            AccessTokenCiphertext = Encrypt(tokenResult.AccessToken),
            RefreshTokenCiphertext = string.IsNullOrWhiteSpace(tokenResult.RefreshToken)
                ? null
                : Encrypt(tokenResult.RefreshToken),
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(
                Math.Max(60, tokenResult.ExpiresInSeconds)),
            UpdatedAt = DateTime.UtcNow
        };
        await _uow.GetRepository<ZaloOauthCredential>().AddAsync(credential);
        await _uow.SaveAsync();
        return tokenResult.AccessToken;
    }

    private async Task<ZaloTokenResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://oauth.zaloapp.com/v4/oa/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["app_id"] = GetRequiredConfiguration("Zalo:AppId"),
                ["grant_type"] = "refresh_token"
            })
        };
        request.Headers.TryAddWithoutValidation(
            "secret_key",
            GetRequiredConfiguration("Zalo:AppSecretKey"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Zalo OAuth API returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var accessTokenElement) ||
            !root.TryGetProperty("refresh_token", out var refreshTokenElement))
        {
            var error = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "Unknown Zalo OAuth response.";
            throw new InvalidOperationException($"Unable to refresh Zalo token: {error}");
        }

        var accessToken = accessTokenElement.GetString();
        var nextRefreshToken = refreshTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(nextRefreshToken))
        {
            throw new InvalidOperationException("Zalo OAuth response did not contain valid tokens.");
        }

        var expiresIn = 90000d;
        if (root.TryGetProperty("expires_in", out var expiresElement) &&
            double.TryParse(
                expiresElement.ToString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsedExpiresIn))
        {
            expiresIn = parsedExpiresIn;
        }

        return new ZaloTokenResult(accessToken, nextRefreshToken, Math.Max(60, expiresIn));
    }

    private DateTime? ParseConfiguredExpiry()
    {
        return DateTime.TryParse(
            _configuration["Zalo:AccessTokenExpiresAtUtc"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt)
            ? expiresAt
            : null;
    }

    private string Encrypt(string value)
    {
        var key = GetEncryptionKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    private string Decrypt(string value)
    {
        var payload = Convert.FromBase64String(value);
        if (payload.Length < 29)
        {
            throw new InvalidOperationException("Stored Zalo token is invalid.");
        }

        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var ciphertext = payload.AsSpan(28);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetEncryptionKey(), tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetEncryptionKey()
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(GetRequiredConfiguration("Zalo:TokenEncryptionKey"));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Zalo:TokenEncryptionKey must be a Base64-encoded 32-byte key.",
                exception);
        }

        return key.Length == 32
            ? key
            : throw new InvalidOperationException(
                "Zalo:TokenEncryptionKey must be a Base64-encoded 32-byte key.");
    }

    private string GetRequiredConfiguration(string key)
    {
        return _configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing configuration: {key}");
    }

    private sealed record ZaloTokenResult(
        string AccessToken,
        string RefreshToken,
        double ExpiresInSeconds);
}
