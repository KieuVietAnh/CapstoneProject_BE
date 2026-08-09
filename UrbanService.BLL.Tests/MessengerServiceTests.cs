using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
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

    [Fact]
    public async Task StartQuickReply_UsesPayloadAndSendsDraftControls()
    {
        var conversation = new MessengerFeedbackConversation
        {
            ConversationId = 10,
            PageId = "page-1",
            SenderPsid = "sender-1",
            State = "Idle",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var conversationRepository = Substitute.For<IGenericRepository<MessengerFeedbackConversation>>();
        conversationRepository.Entities.Returns(new[] { conversation }.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerFeedbackConversation>().Returns(conversationRepository);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["Messenger:PageAccessToken"] = "page-token"
            },
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(
            """
            {
              "object": "page",
              "entry": [{
                "id": "page-1",
                "messaging": [{
                  "sender": { "id": "sender-1" },
                  "timestamp": 1,
                  "message": {
                    "mid": "message-1",
                    "text": "Gửi phản ánh",
                    "quick_reply": { "payload": "START_FEEDBACK" }
                  }
                }]
              }]
            }
            """);

        Assert.Equal("AwaitingTitle", conversation.State);
        var body = Assert.Single(handler.RequestBodies);
        using var json = JsonDocument.Parse(body);
        var quickReplies = json.RootElement
            .GetProperty("message")
            .GetProperty("quick_replies");
        Assert.Equal("CANCEL", quickReplies[0].GetProperty("payload").GetString());
    }

    [Fact]
    public async Task FeedbackHistory_OnlyReturnsCurrentConversationSubmissions()
    {
        var conversation = new MessengerFeedbackConversation
        {
            ConversationId = 10,
            PageId = "page-1",
            SenderPsid = "sender-1",
            State = "Completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ownFeedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            Title = "Đèn đường bị hỏng",
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow
        };
        var anotherFeedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            Title = "Phản ánh của người khác",
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow
        };
        var submissions = new[]
        {
            new MessengerFeedbackSubmission
            {
                SubmissionId = 1,
                ConversationId = 10,
                FeedbackId = ownFeedback.FeedbackId,
                Feedback = ownFeedback,
                CreatedAt = ownFeedback.CreatedAt
            },
            new MessengerFeedbackSubmission
            {
                SubmissionId = 2,
                ConversationId = 20,
                FeedbackId = anotherFeedback.FeedbackId,
                Feedback = anotherFeedback,
                CreatedAt = anotherFeedback.CreatedAt
            }
        };
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var conversationRepository = Substitute.For<IGenericRepository<MessengerFeedbackConversation>>();
        conversationRepository.Entities.Returns(new[] { conversation }.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerFeedbackConversation>().Returns(conversationRepository);
        var submissionRepository = Substitute.For<IGenericRepository<MessengerFeedbackSubmission>>();
        submissionRepository.Entities.Returns(submissions.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerFeedbackSubmission>().Returns(submissionRepository);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["Messenger:PageAccessToken"] = "page-token"
            },
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(
            """
            {
              "object": "page",
              "entry": [{
                "id": "page-1",
                "messaging": [{
                  "sender": { "id": "sender-1" },
                  "timestamp": 2,
                  "message": {
                    "mid": "message-2",
                    "text": "Phản ánh đã gửi",
                    "quick_reply": { "payload": "VIEW_FEEDBACKS:1" }
                  }
                }]
              }]
            }
            """);

        var body = Assert.Single(handler.RequestBodies);
        using var json = JsonDocument.Parse(body);
        var messageText = json.RootElement
            .GetProperty("message")
            .GetProperty("text")
            .GetString();
        Assert.Contains(ownFeedback.Title, messageText);
        Assert.DoesNotContain(anotherFeedback.Title, messageText);
    }

    private static MessengerService CreateService(
        Dictionary<string, string?> values,
        IUnitOfWork? unitOfWork = null,
        HttpClient? httpClient = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new MessengerService(
            httpClient ?? new HttpClient(),
            configuration,
            unitOfWork ?? Substitute.For<IUnitOfWork>(),
            Substitute.For<IFeedbackService>(),
            NullLogger<MessengerService>.Instance);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
