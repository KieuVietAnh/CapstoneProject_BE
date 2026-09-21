using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class AiChatServiceTests
{
    [Fact]
    public async Task DeleteConversationAsync_OwnConversation_DeletesAndSavesOnce()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation(7, userId);
        var context = new TestContext(conversation);
        var service = CreateService(context.UnitOfWork);

        await service.DeleteConversationAsync(userId, conversation.AiConversationId);

        context.ConversationRepository.Received(1).Delete(conversation);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task DeleteConversationAsync_DifferentOwner_DoesNotDeleteOrSave()
    {
        var conversation = Conversation(7, Guid.NewGuid());
        var context = new TestContext(conversation);
        var service = CreateService(context.UnitOfWork);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.DeleteConversationAsync(Guid.NewGuid(), conversation.AiConversationId));

        Assert.Equal("Khong tim thay conversation cua nguoi dung.", exception.Message);
        context.ConversationRepository.DidNotReceive().Delete(Arg.Any<AiConversation>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task DeleteConversation_ActionReturnsNoContentAndUsesCurrentUser()
    {
        var userId = Guid.NewGuid();
        var aiChatService = Substitute.For<IAiChatService>();
        var controller = CreateController(aiChatService, userId);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await controller.DeleteConversation(15, cancellationTokenSource.Token);

        Assert.IsType<NoContentResult>(result);
        await aiChatService.Received(1).DeleteConversationAsync(
            userId,
            15,
            cancellationTokenSource.Token);
    }

    [Fact]
    public void DeleteConversation_ActionRequiresServiceUserAndIntRoute()
    {
        var action = typeof(AiController)
            .GetMethod(nameof(AiController.DeleteConversation))!;

        var authorize = Assert.Single(action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(UserRole.SERVICEUSER, authorize.Roles);

        var httpDelete = Assert.Single(action
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true)
            .Cast<HttpDeleteAttribute>());
        Assert.Equal("conversations/{conversationId:int}", httpDelete.Template);

        var responseTypes = action
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>();
        Assert.Contains(responseTypes, response => response.StatusCode == StatusCodes.Status204NoContent);
    }

    [Fact]
    public void AiConversationMessageRelationship_UsesCascadeDelete()
    {
        var options = new DbContextOptionsBuilder<UrbanServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=urbanservice_model_test")
            .Options;
        using var dbContext = new UrbanServiceDbContext(options);

        var messageEntity = dbContext.Model.FindEntityType(typeof(AiMessage))!;
        var conversationForeignKey = Assert.Single(messageEntity.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, conversationForeignKey.DeleteBehavior);
    }

    private static AiChatService CreateService(IUnitOfWork unitOfWork)
    {
        var configuration = new ConfigurationBuilder().Build();
        var aiClient = new OpenRouterAiClient(
            new HttpClient(),
            configuration,
            NullLogger<OpenRouterAiClient>.Instance);

        return new AiChatService(unitOfWork, aiClient);
    }

    private static AiController CreateController(IAiChatService aiChatService, Guid userId)
    {
        var controller = new AiController(
            Substitute.For<IAiClient>(),
            aiChatService,
            Substitute.For<IAiFeedbackDraftService>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IAiFeedbackReviewQueue>(),
            new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"))
            }
        };

        return controller;
    }

    private static AiConversation Conversation(int conversationId, Guid userId)
        => new()
        {
            AiConversationId = conversationId,
            UserId = userId,
            Status = "Active",
            StartedAt = DateTime.UtcNow
        };

    private sealed class TestContext
    {
        public TestContext(params AiConversation[] conversations)
        {
            ConversationRepository.Entities.Returns(_ => conversations.AsAsyncQueryable());
            UnitOfWork.GetRepository<AiConversation>().Returns(ConversationRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IGenericRepository<AiConversation> ConversationRepository { get; }
            = Substitute.For<IGenericRepository<AiConversation>>();
    }
}
