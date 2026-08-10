using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class ZaloServiceTests
{
    [Fact]
    public void Signature_WithMatchingAppAndHash_IsAccepted()
    {
        const string appId = "app-123";
        const string secretKey = "oa-secret";
        const string timestamp = "1720000000000";
        const string payload =
            "{\"app_id\":\"app-123\",\"timestamp\":\"1720000000000\"}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{appId}{payload}{timestamp}{secretKey}"))).ToLowerInvariant();
        var service = CreateService(new Dictionary<string, string?>
        {
            ["Zalo:AppId"] = appId,
            ["Zalo:OaSecretKey"] = secretKey
        });

        Assert.True(service.IsSignatureValid(payload, signature));
        Assert.True(service.IsSignatureValid(payload, $"mac={signature}"));
        Assert.False(service.IsSignatureValid(payload + " ", signature));
        Assert.False(service.IsSignatureValid(payload, "invalid"));
    }

    [Fact]
    public async Task StartCommand_AdvancesDraftAndUsesZaloSendApi()
    {
        var conversation = Conversation(state: "Idle");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook("message-1", "Bắt đầu"));

        Assert.Equal("AwaitingTitle", conversation.State);
        Assert.Equal("message-1", conversation.LastMessageId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("zalo-access-token", request.AccessToken);
        Assert.Contains("tiêu đề", GetOutgoingText(request.Body), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateMessageId_DoesNotAdvanceConversationTwice()
    {
        var conversation = Conversation(state: "AwaitingTitle");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler));
        var payload = TextWebhook("same-message", "Đèn đường bị hỏng");

        await service.ProcessWebhookAsync(payload);
        await service.ProcessWebhookAsync(payload);

        Assert.Equal("AwaitingDescription", conversation.State);
        Assert.Equal("Đèn đường bị hỏng", conversation.Title);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RetriedConfirmation_AfterFeedbackWasCreated_DoesNotCreateAgain()
    {
        var feedbackId = Guid.NewGuid();
        var conversation = Conversation(state: "Completed");
        conversation.FeedbackId = feedbackId;
        conversation.LastMessageId = "confirm-1";
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var feedbackService = Substitute.For<IFeedbackService>();
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler),
            feedbackService);

        await service.ProcessWebhookAsync(TextWebhook("confirm-1", "Xác nhận"));

        await feedbackService.DidNotReceiveWithAnyArgs().CreateAsync(
            default,
            default!,
            default!);
        Assert.Contains(
            feedbackId.ToString(),
            GetOutgoingText(Assert.Single(handler.Requests).Body));
    }

    [Fact]
    public async Task SharedLocation_CapturesCoordinatesAndRequestsArea()
    {
        var conversation = Conversation(state: "AwaitingLocation");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var areaRepository = Substitute.For<IGenericRepository<OperatingArea>>();
        areaRepository.Entities.Returns(new[]
        {
            new OperatingArea { AreaId = 7, AreaName = "Phường 7", IsActive = true }
        }.AsAsyncQueryable());
        unitOfWork.GetRepository<OperatingArea>().Returns(areaRepository);
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(LocationWebhook("location-1", "10.7642473", "106.6564314"));

        Assert.Equal("AwaitingArea", conversation.State);
        Assert.Equal(10.7642473m, conversation.Latitude);
        Assert.Equal(106.6564314m, conversation.Longitude);
        Assert.Contains("7 - Phường 7", GetOutgoingText(Assert.Single(handler.Requests).Body));
    }

    [Fact]
    public async Task ImageDuringDraft_IsStoredForSubmission()
    {
        var conversation = Conversation(state: "AwaitingDescription");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var attachmentRepository = unitOfWork.GetRepository<ZaloFeedbackDraftAttachment>();
        IReadOnlyCollection<ZaloFeedbackDraftAttachment>? storedAttachments = null;
        attachmentRepository.AddRangeAsync(Arg.Any<IEnumerable<ZaloFeedbackDraftAttachment>>())
            .Returns(callInfo =>
            {
                storedAttachments = callInfo.Arg<IEnumerable<ZaloFeedbackDraftAttachment>>().ToList();
                return Task.CompletedTask;
            });
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(ImageWebhook(
            "image-1",
            "https://photo.talk.zdn.vn/example.jpg"));

        var attachment = Assert.Single(storedAttachments!);
        Assert.Equal(conversation.ConversationId, attachment.ConversationId);
        Assert.Equal("https://photo.talk.zdn.vn/example.jpg", attachment.SourceUrl);
        Assert.Contains("Đã thêm 1 ảnh", GetOutgoingText(Assert.Single(handler.Requests).Body));
    }

    [Fact]
    public async Task Confirmation_CreatesFeedbackWithZaloChannel()
    {
        var conversation = Conversation(state: "AwaitingConfirmation");
        conversation.ConversationId = 10;
        conversation.Title = "Đèn đường bị hỏng";
        conversation.Description = "Đèn tắt nhiều ngày";
        conversation.LocationText = "Đường số 1";
        conversation.AreaId = 7;
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var submissionRepository = Substitute.For<IGenericRepository<ZaloFeedbackSubmission>>();
        unitOfWork.GetRepository<ZaloFeedbackSubmission>().Returns(submissionRepository);
        var draftAttachment = new ZaloFeedbackDraftAttachment
        {
            DraftAttachmentId = 1,
            ConversationId = conversation.ConversationId,
            SourceUrl = "https://photo.talk.zdn.vn/evidence.jpg",
            FileType = "image",
            CreatedAt = DateTime.UtcNow
        };
        var attachmentRepository = unitOfWork.GetRepository<ZaloFeedbackDraftAttachment>();
        attachmentRepository.Entities.Returns(new[] { draftAttachment }.AsAsyncQueryable());

        var submissionUserId = Guid.NewGuid();
        var userRepository = Substitute.For<IGenericRepository<User>>();
        userRepository.Entities.Returns(new[]
        {
            new User
            {
                UserId = submissionUserId,
                IsActive = true,
                Role = new Role { RoleName = UserRole.SERVICEUSER }
            }
        }.AsAsyncQueryable());
        unitOfWork.GetRepository<User>().Returns(userRepository);

        var feedbackId = Guid.NewGuid();
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.CreateAsync(
                submissionUserId,
                Arg.Any<FeedbackCreateRequest>(),
                Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>())
            .Returns(new FeedbackDetailDto { FeedbackId = feedbackId });
        var cloudinaryService = Substitute.For<ICloudinaryService>();
        cloudinaryService.UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                "image/jpeg",
                "urban-service/zalo-feedbacks",
                Arg.Any<CancellationToken>())
            .Returns(new CloudinaryUploadResultDto
            {
                FileUrl = "https://cloudinary.example/evidence.jpg",
                FileType = "image/jpeg"
            });
        var handler = new RecordingZaloHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Zalo:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);

        await service.ProcessWebhookAsync(TextWebhook("confirm-1", "Xác nhận"));

        Assert.Equal("Completed", conversation.State);
        Assert.Equal(feedbackId, conversation.FeedbackId);
        await feedbackService.Received(1).CreateAsync(
            submissionUserId,
            Arg.Is<FeedbackCreateRequest>(request =>
                request.SubmissionChannel == FeedbackSubmissionChannel.Zalo &&
                request.GeoSource == "Zalo"),
            Arg.Is<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>(attachments =>
                attachments.Count == 1 &&
                attachments.Single().FileUrl == "https://cloudinary.example/evidence.jpg"));
        await submissionRepository.Received(1).AddAsync(
            Arg.Is<ZaloFeedbackSubmission>(item =>
                item.ConversationId == conversation.ConversationId &&
                item.FeedbackId == feedbackId));
        attachmentRepository.Received(1).DeleteRange(
            Arg.Is<IEnumerable<ZaloFeedbackDraftAttachment>>(items => items.Single() == draftAttachment));
    }

    [Fact]
    public async Task FeedbackHistory_OnlyReturnsCurrentZaloConversationSubmissions()
    {
        var conversation = Conversation(state: "Completed");
        conversation.ConversationId = 10;
        var ownFeedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            Title = "Phản ánh của tôi",
            Status = FeedbackStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        var anotherFeedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            Title = "Phản ánh của người khác",
            Status = FeedbackStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var submissionRepository = Substitute.For<IGenericRepository<ZaloFeedbackSubmission>>();
        submissionRepository.Entities.Returns(new[]
        {
            new ZaloFeedbackSubmission
            {
                ConversationId = 10,
                Feedback = ownFeedback,
                FeedbackId = ownFeedback.FeedbackId,
                CreatedAt = ownFeedback.CreatedAt
            },
            new ZaloFeedbackSubmission
            {
                ConversationId = 20,
                Feedback = anotherFeedback,
                FeedbackId = anotherFeedback.FeedbackId,
                CreatedAt = anotherFeedback.CreatedAt
            }
        }.AsAsyncQueryable());
        unitOfWork.GetRepository<ZaloFeedbackSubmission>().Returns(submissionRepository);
        var handler = new RecordingZaloHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook("history-1", "Xem phản ánh"));

        var text = GetOutgoingText(Assert.Single(handler.Requests).Body);
        Assert.Contains(ownFeedback.Title, text);
        Assert.DoesNotContain(anotherFeedback.Title, text);
    }

    private static ZaloService CreateService(
        Dictionary<string, string?> values,
        IUnitOfWork? unitOfWork = null,
        HttpClient? httpClient = null,
        IFeedbackService? feedbackService = null,
        ICloudinaryService? cloudinaryService = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var tokenProvider = Substitute.For<IZaloAccessTokenProvider>();
        tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns("zalo-access-token");

        return new ZaloService(
            httpClient ?? new HttpClient(new RecordingZaloHttpMessageHandler()),
            configuration,
            unitOfWork ?? Substitute.For<IUnitOfWork>(),
            feedbackService ?? Substitute.For<IFeedbackService>(),
            cloudinaryService ?? Substitute.For<ICloudinaryService>(),
            tokenProvider,
            NullLogger<ZaloService>.Instance);
    }

    private static IUnitOfWork UnitOfWorkWithConversation(ZaloFeedbackConversation conversation)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var repository = Substitute.For<IGenericRepository<ZaloFeedbackConversation>>();
        repository.Entities.Returns(new[] { conversation }.AsAsyncQueryable());
        unitOfWork.GetRepository<ZaloFeedbackConversation>().Returns(repository);
        var attachmentRepository = Substitute.For<IGenericRepository<ZaloFeedbackDraftAttachment>>();
        attachmentRepository.Entities.Returns(
            Array.Empty<ZaloFeedbackDraftAttachment>().AsAsyncQueryable());
        unitOfWork.GetRepository<ZaloFeedbackDraftAttachment>().Returns(attachmentRepository);
        return unitOfWork;
    }

    private static ZaloFeedbackConversation Conversation(string state)
    {
        return new ZaloFeedbackConversation
        {
            ConversationId = 1,
            OaId = "oa-1",
            SenderUserId = "sender-1",
            State = state,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Dictionary<string, string?> BaseConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["Zalo:OaId"] = "oa-1"
        };
    }

    private static string TextWebhook(string messageId, string text)
    {
        return $$"""
            {
              "app_id": "app-1",
              "sender": { "id": "sender-1" },
              "recipient": { "id": "oa-1" },
              "event_name": "user_send_text",
              "message": { "text": "{{text}}", "msg_id": "{{messageId}}" },
              "timestamp": "1720000000000"
            }
            """;
    }

    private static string LocationWebhook(string messageId, string latitude, string longitude)
    {
        return $$"""
            {
              "app_id": "app-1",
              "sender": { "id": "sender-1" },
              "recipient": { "id": "oa-1" },
              "event_name": "user_send_location",
              "message": {
                "msg_id": "{{messageId}}",
                "attachments": [{
                  "type": "location",
                  "payload": {
                    "coordinates": {
                      "latitude": "{{latitude}}",
                      "longitude": "{{longitude}}"
                    }
                  }
                }]
              },
              "timestamp": "1720000000000"
            }
            """;
    }

    private static string ImageWebhook(string messageId, string imageUrl)
    {
        return $$"""
            {
              "app_id": "app-1",
              "sender": { "id": "sender-1" },
              "recipient": { "id": "oa-1" },
              "event_name": "user_send_image",
              "message": {
                "msg_id": "{{messageId}}",
                "attachments": [{
                  "type": "image",
                  "payload": { "url": "{{imageUrl}}" }
                }]
              },
              "timestamp": "1720000000000"
            }
            """;
    }

    private static string GetOutgoingText(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("message").GetProperty("text").GetString()!;
    }

    private sealed class RecordingZaloHttpMessageHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var imageContent = new ByteArrayContent([1, 2, 3, 4]);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "image/jpeg");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = imageContent };
            }

            Requests.Add(new RecordedRequest(
                request.Headers.TryGetValues("access_token", out var values)
                    ? values.Single()
                    : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"error\":0,\"message\":\"Success\"}")
            };
        }
    }

    private sealed record RecordedRequest(string? AccessToken, string Body);
}
