using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class MessengerAccountLinkService : IMessengerAccountLinkService
{
    private const int DefaultTokenLifetimeMinutes = 10;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _configuration;

    public MessengerAccountLinkService(IUnitOfWork uow, IConfiguration configuration)
    {
        _uow = uow;
        _configuration = configuration;
    }

    public Task<Guid?> GetLinkedUserIdAsync(
        string pageId,
        string senderPsid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(senderPsid))
        {
            return Task.FromResult<Guid?>(null);
        }

        return _uow.GetRepository<MessengerAccountLink>().Entities
            .AsNoTracking()
            .Where(link =>
                link.PageId == pageId &&
                link.SenderPsid == senderPsid &&
                link.IsActive &&
                link.User.IsActive &&
                link.User.IsVerified &&
                link.User.PhoneNumber != null &&
                link.User.PhoneNumber != string.Empty &&
                link.User.Role.RoleName == UserRole.SERVICEUSER)
            .Select(link => (Guid?)link.UserId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<string> CreateLinkTokenAsync(
        string pageId,
        string senderPsid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(senderPsid))
        {
            throw new Exception("Không xác định được tài khoản Messenger cần liên kết.");
        }

        var now = DateTime.UtcNow;
        var tokenRepository = _uow.GetRepository<MessengerLinkToken>();
        var pendingTokens = await tokenRepository.Entities
            .Where(item =>
                item.PageId == pageId &&
                item.SenderPsid == senderPsid &&
                item.UsedAt == null &&
                item.InvalidatedAt == null &&
                item.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var pendingToken in pendingTokens)
        {
            pendingToken.InvalidatedAt = now;
        }

        var rawToken = CreateRawToken();
        await tokenRepository.AddAsync(new MessengerLinkToken
        {
            LinkTokenId = Guid.NewGuid(),
            PageId = pageId,
            SenderPsid = senderPsid,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddMinutes(GetTokenLifetimeMinutes()),
            CreatedAt = now
        });
        await _uow.SaveAsync();
        return rawToken;
    }

    public async Task<MessengerAccountLinkDto> ConfirmAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("Liên kết Messenger không hợp lệ hoặc đã hết hạn.");
        }

        _uow.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var linkToken = await _uow.GetRepository<MessengerLinkToken>().Entities
                .FirstOrDefaultAsync(
                    item =>
                        item.TokenHash == HashToken(token.Trim()) &&
                        item.UsedAt == null &&
                        item.InvalidatedAt == null &&
                        item.ExpiresAt > now,
                    cancellationToken)
                ?? throw new Exception("Liên kết Messenger không hợp lệ hoặc đã hết hạn.");

            var user = await _uow.GetRepository<User>().Entities
                .Where(candidate => candidate.UserId == userId)
                .Select(candidate => new
                {
                    candidate.IsActive,
                    candidate.IsVerified,
                    candidate.PhoneNumber,
                    candidate.Role.RoleName
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException();

            if (!user.IsActive ||
                !user.IsVerified ||
                !string.Equals(user.RoleName, UserRole.SERVICEUSER, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenAccessException(
                    "Chỉ tài khoản người dân đã xác thực mới có thể liên kết Messenger.");
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                throw new Exception(
                    "Vui lòng cập nhật số điện thoại trước khi liên kết Messenger.");
            }

            var activeLinks = await _uow.GetRepository<MessengerAccountLink>().Entities
                .Where(link =>
                    link.IsActive &&
                    link.PageId == linkToken.PageId &&
                    (link.SenderPsid == linkToken.SenderPsid || link.UserId == userId))
                .ToListAsync(cancellationToken);

            var matchingLink = activeLinks.FirstOrDefault(link =>
                link.SenderPsid == linkToken.SenderPsid && link.UserId == userId);
            if (matchingLink != null)
            {
                linkToken.UsedAt = now;
                await _uow.SaveAsync();
                _uow.CommitTransaction();
                return Map(matchingLink);
            }

            if (activeLinks.Any(link => link.SenderPsid == linkToken.SenderPsid))
            {
                throw new ConflictException(
                    "Tài khoản Messenger này đã được liên kết với một tài khoản khác.");
            }

            if (activeLinks.Any(link => link.UserId == userId))
            {
                throw new ConflictException(
                    "Tài khoản của bạn đã liên kết với một Messenger khác trên trang này.");
            }

            var link = new MessengerAccountLink
            {
                PageId = linkToken.PageId,
                SenderPsid = linkToken.SenderPsid,
                UserId = userId,
                IsActive = true,
                LinkedAt = now
            };
            await _uow.GetRepository<MessengerAccountLink>().AddAsync(link);
            linkToken.UsedAt = now;
            await _uow.SaveAsync();
            _uow.CommitTransaction();
            return Map(link);
        }
        catch
        {
            _uow.RollBack();
            throw;
        }
    }

    public async Task<IReadOnlyCollection<MessengerAccountLinkDto>> GetMyLinksAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _uow.GetRepository<MessengerAccountLink>().Entities
            .AsNoTracking()
            .Where(link => link.UserId == userId)
            .OrderByDescending(link => link.LinkedAt)
            .Select(link => new MessengerAccountLinkDto
            {
                LinkId = link.LinkId,
                PageId = link.PageId,
                IsActive = link.IsActive,
                LinkedAt = link.LinkedAt,
                RevokedAt = link.RevokedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeAsync(
        Guid userId,
        long linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _uow.GetRepository<MessengerAccountLink>().Entities
            .FirstOrDefaultAsync(
                item => item.LinkId == linkId && item.UserId == userId && item.IsActive,
                cancellationToken)
            ?? throw new Exception("Không tìm thấy liên kết Messenger đang hoạt động.");

        link.IsActive = false;
        link.RevokedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
    }

    private int GetTokenLifetimeMinutes()
    {
        return int.TryParse(
                _configuration["Messenger:AccountLinkTokenMinutes"],
                out var configuredMinutes) &&
            configuredMinutes > 0
                ? configuredMinutes
                : DefaultTokenLifetimeMinutes;
    }

    private static string CreateRawToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static MessengerAccountLinkDto Map(MessengerAccountLink link)
    {
        return new MessengerAccountLinkDto
        {
            LinkId = link.LinkId,
            PageId = link.PageId,
            IsActive = link.IsActive,
            LinkedAt = link.LinkedAt,
            RevokedAt = link.RevokedAt
        };
    }
}
