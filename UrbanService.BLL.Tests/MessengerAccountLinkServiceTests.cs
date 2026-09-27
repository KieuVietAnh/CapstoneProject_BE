using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class MessengerAccountLinkServiceTests
{
    [Fact]
    public async Task Confirm_WithEligibleUser_CreatesActiveLinkAndConsumesToken()
    {
        const string rawToken = "valid-link-token";
        var context = CreateContext(rawToken, phoneNumber: "0900000000");
        var service = CreateService(context.UnitOfWork);

        var result = await service.ConfirmAsync(context.UserId, rawToken);

        var link = Assert.Single(context.Links);
        Assert.Equal(context.UserId, link.UserId);
        Assert.Equal("page-1", link.PageId);
        Assert.Equal("sender-1", link.SenderPsid);
        Assert.True(link.IsActive);
        Assert.NotNull(context.Token.UsedAt);
        Assert.True(result.IsActive);
        context.UnitOfWork.Received(1).CommitTransaction();
        context.UnitOfWork.DidNotReceive().RollBack();
    }

    [Fact]
    public async Task Confirm_WithoutPhoneNumber_IsRejectedAndDoesNotCreateLink()
    {
        const string rawToken = "valid-link-token";
        var context = CreateContext(rawToken, phoneNumber: null);
        var service = CreateService(context.UnitOfWork);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => service.ConfirmAsync(context.UserId, rawToken));

        Assert.Contains("số điện thoại", exception.Message);
        Assert.Empty(context.Links);
        Assert.Null(context.Token.UsedAt);
        context.UnitOfWork.Received(1).RollBack();
    }

    [Fact]
    public async Task CreateLinkToken_InvalidatesPreviousPendingToken()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var tokens = new List<MessengerLinkToken>
        {
            new()
            {
                LinkTokenId = Guid.NewGuid(),
                PageId = "page-1",
                SenderPsid = "sender-1",
                TokenHash = HashToken("old-token"),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTime.UtcNow.AddMinutes(9)
            }
        };
        var tokenRepository = Substitute.For<IGenericRepository<MessengerLinkToken>>();
        tokenRepository.Entities.Returns(_ => tokens.AsAsyncQueryable());
        tokenRepository.AddAsync(Arg.Any<MessengerLinkToken>())
            .Returns(call =>
            {
                tokens.Add(call.Arg<MessengerLinkToken>());
                return Task.CompletedTask;
            });
        unitOfWork.GetRepository<MessengerLinkToken>().Returns(tokenRepository);
        var service = CreateService(unitOfWork);

        var rawToken = await service.CreateLinkTokenAsync("page-1", "sender-1");

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.NotNull(tokens[0].InvalidatedAt);
        var createdToken = Assert.Single(tokens.Where(item => item.LinkTokenId != tokens[0].LinkTokenId));
        Assert.Equal(HashToken(rawToken), createdToken.TokenHash);
        Assert.Null(createdToken.InvalidatedAt);
        Assert.Null(createdToken.UsedAt);
    }

    [Fact]
    public async Task Revoke_ActiveOwnedLink_MarksItInactive()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var userId = Guid.NewGuid();
        var links = new List<MessengerAccountLink>
        {
            new()
            {
                LinkId = 15,
                PageId = "page-1",
                SenderPsid = "sender-1",
                UserId = userId,
                IsActive = true,
                LinkedAt = DateTime.UtcNow
            }
        };
        var linkRepository = Substitute.For<IGenericRepository<MessengerAccountLink>>();
        linkRepository.Entities.Returns(_ => links.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerAccountLink>().Returns(linkRepository);
        var service = CreateService(unitOfWork);

        await service.RevokeAsync(userId, 15);

        Assert.False(links[0].IsActive);
        Assert.NotNull(links[0].RevokedAt);
        await unitOfWork.Received(1).SaveAsync();
    }

    private static TestContext CreateContext(string rawToken, string? phoneNumber)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var userId = Guid.NewGuid();
        var token = new MessengerLinkToken
        {
            LinkTokenId = Guid.NewGuid(),
            PageId = "page-1",
            SenderPsid = "sender-1",
            TokenHash = HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        var tokens = new List<MessengerLinkToken> { token };
        var users = new List<User>
        {
            new()
            {
                UserId = userId,
                FullName = "Test User",
                Email = "user@example.test",
                PasswordHash = "test",
                PhoneNumber = phoneNumber,
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow,
                Role = new Role { RoleName = UserRole.SERVICEUSER }
            }
        };
        var links = new List<MessengerAccountLink>();

        var tokenRepository = Substitute.For<IGenericRepository<MessengerLinkToken>>();
        tokenRepository.Entities.Returns(_ => tokens.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerLinkToken>().Returns(tokenRepository);

        var userRepository = Substitute.For<IGenericRepository<User>>();
        userRepository.Entities.Returns(_ => users.AsAsyncQueryable());
        unitOfWork.GetRepository<User>().Returns(userRepository);

        var linkRepository = Substitute.For<IGenericRepository<MessengerAccountLink>>();
        linkRepository.Entities.Returns(_ => links.AsAsyncQueryable());
        linkRepository.AddAsync(Arg.Any<MessengerAccountLink>())
            .Returns(call =>
            {
                var link = call.Arg<MessengerAccountLink>();
                link.LinkId = links.Count + 1;
                links.Add(link);
                return Task.CompletedTask;
            });
        unitOfWork.GetRepository<MessengerAccountLink>().Returns(linkRepository);

        return new TestContext(unitOfWork, userId, token, links);
    }

    private static MessengerAccountLinkService CreateService(IUnitOfWork unitOfWork)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messenger:AccountLinkTokenMinutes"] = "10"
            })
            .Build();
        return new MessengerAccountLinkService(unitOfWork, configuration);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private sealed record TestContext(
        IUnitOfWork UnitOfWork,
        Guid UserId,
        MessengerLinkToken Token,
        List<MessengerAccountLink> Links);
}
