using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task ForgotPasswordEndpoints_AreAnonymousAndReturnNoContent()
    {
        var authService = Substitute.For<IAuthService>();
        authService.RequestForgotPasswordOtpAsync(
                Arg.Any<ForgotPasswordRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        authService.ResetPasswordAsync(
                Arg.Any<ResetPasswordRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var controller = new AuthController(authService);

        var sendResult = await controller.SendForgotPasswordOtp(
            new ForgotPasswordRequest { Email = "citizen@urban.test" },
            default);
        var resetResult = await controller.ResetForgottenPassword(
            new ResetPasswordRequest
            {
                Email = "citizen@urban.test",
                Otp = "123456",
                NewPassword = "new-secret"
            },
            default);

        Assert.IsType<NoContentResult>(sendResult);
        Assert.IsType<NoContentResult>(resetResult);
        Assert.NotNull(typeof(AuthController)
            .GetMethod(nameof(AuthController.SendForgotPasswordOtp))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .SingleOrDefault());
        Assert.NotNull(typeof(AuthController)
            .GetMethod(nameof(AuthController.ResetForgottenPassword))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .SingleOrDefault());
    }

    [Fact]
    public async Task RequestForgotPasswordOtpAsync_ActiveUser_SendsOtpEmail()
    {
        using var context = new AuthTestContext();
        context.User("  Citizen@Urban.Test  ");

        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = " CITIZEN@urban.test " });

        var email = Assert.Single(context.SentEmails);
        Assert.Equal("Citizen@Urban.Test", Assert.Single(email.To));
        Assert.Contains("UrbanService", email.Subject);
        Assert.Matches(@"(?<!\d)\d{6}(?!\d)", email.Body);
    }

    [Fact]
    public async Task RequestForgotPasswordOtpAsync_UnknownOrInactive_DoesNotSendEmail()
    {
        using var context = new AuthTestContext();
        context.User("inactive@urban.test", isActive: false);

        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "unknown@urban.test" });
        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "inactive@urban.test" });

        Assert.Empty(context.SentEmails);
    }

    [Fact]
    public async Task RequestForgotPasswordOtpAsync_InvalidEmail_RejectsRequest()
    {
        using var context = new AuthTestContext();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            context.Service.RequestForgotPasswordOtpAsync(
                new ForgotPasswordRequest { Email = "not-an-email" }));

        Assert.Equal("Email không hợp lệ.", exception.Message);
        Assert.Empty(context.SentEmails);
    }

    [Fact]
    public async Task RequestForgotPasswordOtpAsync_DuringCooldown_SendsOnlyOnce()
    {
        using var context = new AuthTestContext();
        context.User("citizen@urban.test");

        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "citizen@urban.test" });
        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "citizen@urban.test" });

        Assert.Single(context.SentEmails);
    }

    [Fact]
    public async Task RequestForgotPasswordOtpAsync_EmailFailure_DoesNotKeepOtpOrCooldown()
    {
        using var context = new AuthTestContext();
        context.User("citizen@urban.test");
        context.EmailFailure = new InvalidOperationException("provider unavailable");

        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "citizen@urban.test" });
        await Assert.ThrowsAsync<Exception>(() => context.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = "citizen@urban.test",
                Otp = "123456",
                NewPassword = "new-secret"
            }));

        context.EmailFailure = null;
        await context.Service.RequestForgotPasswordOtpAsync(
            new ForgotPasswordRequest { Email = "citizen@urban.test" });

        Assert.Single(context.SentEmails);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_ChangesPasswordAndRevokesRefreshToken()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var otp = await context.RequestOtpAsync(user.Email);

        await context.Service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email,
            Otp = otp,
            NewPassword = "new-secret"
        });

        Assert.True(PasswordHasher.Verify("new-secret", user.PasswordHash));
        Assert.False(PasswordHasher.Verify("old-secret", user.PasswordHash));
        Assert.True(user.IsRefreshTokenRevoked);
        Assert.Null(user.RefreshToken);
        Assert.NotNull(user.UpdatedAt);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_UsedOtp_CannotBeUsedAgain()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var otp = await context.RequestOtpAsync(user.Email);
        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            Otp = otp,
            NewPassword = "new-secret"
        };

        await context.Service.ResetPasswordAsync(request);
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            context.Service.ResetPasswordAsync(request));

        Assert.Equal("OTP không hợp lệ hoặc đã hết hạn.", exception.Message);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_MissingOrDifferentEmail_UsesSameGenericError()
    {
        using var context = new AuthTestContext();
        var firstUser = context.User("first@urban.test");
        context.User("second@urban.test");
        var otp = await context.RequestOtpAsync(firstUser.Email);

        var missingException = await Assert.ThrowsAsync<Exception>(() =>
            context.Service.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = "missing@urban.test",
                Otp = "123456",
                NewPassword = "new-secret"
            }));
        var differentEmailException = await Assert.ThrowsAsync<Exception>(() =>
            context.Service.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = "second@urban.test",
                Otp = otp,
                NewPassword = "new-secret"
            }));

        Assert.Equal(missingException.Message, differentEmailException.Message);
        Assert.Equal("OTP không hợp lệ hoặc đã hết hạn.", missingException.Message);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_FifthWrongAttempt_InvalidatesOtp()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var otp = await context.RequestOtpAsync(user.Email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<Exception>(() => context.Service.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Email = user.Email,
                    Otp = "000000",
                    NewPassword = "new-secret"
                }));
        }

        await Assert.ThrowsAsync<Exception>(() => context.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = user.Email,
                Otp = otp,
                NewPassword = "new-secret"
            }));
        Assert.True(PasswordHasher.Verify("old-secret", user.PasswordHash));
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_DoesNotConsumeOtp()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var otp = await context.RequestOtpAsync(user.Email);

        await Assert.ThrowsAsync<Exception>(() => context.Service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = user.Email,
                Otp = otp,
                NewPassword = "short"
            }));
        await context.Service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email,
            Otp = otp,
            NewPassword = "long-enough"
        });

        Assert.True(PasswordHasher.Verify("long-enough", user.PasswordHash));
    }

    [Fact]
    public async Task ResetPasswordAsync_SaveFailure_RestoresUserAndKeepsOtp()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var originalHash = user.PasswordHash;
        var otp = await context.RequestOtpAsync(user.Email);
        context.UnitOfWork.SaveAsync().Returns(
            _ => Task.FromException(new InvalidOperationException("database unavailable")),
            _ => Task.CompletedTask);
        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            Otp = otp,
            NewPassword = "new-secret"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.ResetPasswordAsync(request));
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.False(user.IsRefreshTokenRevoked);
        Assert.NotNull(user.RefreshToken);

        await context.Service.ResetPasswordAsync(request);
        Assert.True(PasswordHasher.Verify("new-secret", user.PasswordHash));
    }

    [Fact]
    public async Task ResetPasswordAsync_ConcurrentUse_AllowsOnlyOneReset()
    {
        using var context = new AuthTestContext();
        var user = context.User("citizen@urban.test");
        var otp = await context.RequestOtpAsync(user.Email);
        var saveStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        context.UnitOfWork.SaveAsync().Returns(async _ =>
        {
            saveStarted.TrySetResult();
            await releaseSave.Task;
        });
        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            Otp = otp,
            NewPassword = "new-secret"
        };

        var firstReset = context.Service.ResetPasswordAsync(request);
        await saveStarted.Task;
        var secondException = await Assert.ThrowsAsync<Exception>(() =>
            context.Service.ResetPasswordAsync(request));
        releaseSave.TrySetResult();
        await firstReset;

        Assert.Equal("OTP không hợp lệ hoặc đã hết hạn.", secondException.Message);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    private sealed class AuthTestContext : IDisposable
    {
        public AuthTestContext()
        {
            UserRepository.Entities.Returns(_ => Users.AsAsyncQueryable());
            UnitOfWork.GetRepository<User>().Returns(UserRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
            EmailSender.SendAsync(
                    Arg.Any<EmailMessageDto>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    if (EmailFailure != null)
                    {
                        return Task.FromException(EmailFailure);
                    }

                    SentEmails.Add(callInfo.Arg<EmailMessageDto>());
                    return Task.CompletedTask;
                });

            Service = new AuthService(
                UnitOfWork,
                Substitute.For<IConfiguration>(),
                Substitute.For<IJwtTokenGenerator>(),
                EmailSender,
                Cache,
                Substitute.For<ILogger<AuthService>>());
        }

        public List<User> Users { get; } = [];

        public List<EmailMessageDto> SentEmails { get; } = [];

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IGenericRepository<User> UserRepository { get; } =
            Substitute.For<IGenericRepository<User>>();

        public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

        public IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

        public AuthService Service { get; }

        public Exception? EmailFailure { get; set; }

        public User User(string email, bool isActive = true)
        {
            var normalizedEmail = email.Trim();
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = 1,
                FullName = "Citizen Test",
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.Hash("old-secret"),
                IsActive = isActive,
                IsVerified = true,
                RefreshToken = "refresh-token-hash",
                IsRefreshTokenRevoked = false,
                CreatedAt = DateTime.UtcNow
            };
            Users.Add(user);
            return user;
        }

        public async Task<string> RequestOtpAsync(string email)
        {
            await Service.RequestForgotPasswordOtpAsync(
                new ForgotPasswordRequest { Email = email });
            var message = SentEmails.Last();
            return Regex.Match(message.Body, @"(?<!\d)\d{6}(?!\d)").Value;
        }

        public void Dispose()
        {
            Cache.Dispose();
        }
    }
}
