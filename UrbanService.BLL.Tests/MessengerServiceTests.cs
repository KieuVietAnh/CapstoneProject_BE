using System.Net;
using System.Net.Http.Headers;
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
        var conversation = Conversation("Idle");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook(
            "message-1",
            "Gửi phản ánh",
            "START_FEEDBACK"));

        Assert.Equal("AwaitingTitle", conversation.State);
        var quickReplies = GetQuickReplies(Assert.Single(handler.RequestBodies));
        Assert.Equal("CANCEL", quickReplies[0].GetProperty("payload").GetString());
    }

    [Fact]
    public async Task AreaSelection_AdvancesToEvidenceStepWithStableControls()
    {
        var conversation = Conversation("AwaitingArea");
        conversation.Title = "Đèn đường bị hỏng";
        conversation.Description = "Đèn tắt nhiều ngày";
        conversation.LocationText = "Đường số 1";
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        ConfigureArea(unitOfWork, 7, "Phường 7");
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook("area-1", "7"));

        Assert.Equal("AwaitingEvidence", conversation.State);
        Assert.Equal(7, conversation.AreaId);
        var quickReplies = GetQuickReplies(Assert.Single(handler.RequestBodies));
        Assert.Equal(
            new string?[] { "EVIDENCE_DONE", "EVIDENCE_SKIP", "CANCEL" },
            quickReplies.Select(item => item.GetProperty("payload").GetString()).ToArray());
    }

    [Fact]
    public async Task ImagesDuringEvidence_UseRemainingSlotsAndDuplicateMidIsIdempotent()
    {
        var conversation = Conversation("AwaitingEvidence");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/existing.jpg", "old-mid", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:MaxImagesPerFeedback"] = "2";
        var service = CreateService(configuration, unitOfWork, new HttpClient(handler));
        var webhook = ImageWebhook(
            "image-1",
            "https://scontent.fbcdn.net/new.jpg",
            "https://scontent.fbcdn.net/new.jpg",
            "https://lookaside.fbsbx.com/no-slot.jpg");

        await service.ProcessWebhookAsync(webhook);
        await service.ProcessWebhookAsync(webhook);

        Assert.Equal("AwaitingEvidence", conversation.State);
        Assert.Equal(2, drafts.Count);
        var added = Assert.Single(drafts.Where(item => item.SourceMessageId == "image-1"));
        Assert.Equal(0, added.SourceOrdinal);
        Assert.Equal("https://scontent.fbcdn.net/new.jpg", added.SourceUrl);
        Assert.Contains("2/2 ảnh", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task ImageOutsideEvidence_DoesNotChangeDraftOrStoreAttachment()
    {
        var conversation = Conversation("AwaitingDescription");
        conversation.Title = "Tiêu đề đang nhập";
        var drafts = new List<MessengerFeedbackDraftAttachment>();
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(ImageWebhook(
            "image-wrong-step",
            "https://scontent.fbcdn.net/evidence.jpg"));

        Assert.Equal("AwaitingDescription", conversation.State);
        Assert.Equal("Tiêu đề đang nhập", conversation.Title);
        Assert.Empty(drafts);
        Assert.Contains("sau khi bạn chọn khu vực", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task NullAttachmentCollection_IsHandledAsAnEmptyMessage()
    {
        var conversation = Conversation("Idle");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));
        const string webhook =
            """
            {
              "object": "page",
              "entry": [{
                "id": "page-1",
                "messaging": [{
                  "sender": { "id": "sender-1" },
                  "timestamp": 1,
                  "message": { "mid": "null-attachments", "attachments": null }
                }]
              }]
            }
            """;

        await service.ProcessWebhookAsync(webhook);

        Assert.Equal("Idle", conversation.State);
        Assert.Contains("nội dung chữ và ảnh", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Theory]
    [InlineData("http://scontent.fbcdn.net/evidence.jpg")]
    [InlineData("https://fbcdn.net.attacker.example/evidence.jpg")]
    public async Task UntrustedImageUrl_IsRejectedAndDraftIsRetained(string imageUrl)
    {
        var conversation = Conversation("AwaitingEvidence");
        var drafts = new List<MessengerFeedbackDraftAttachment>();
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(ImageWebhook(
            "image-untrusted",
            imageUrl));

        Assert.Equal("AwaitingEvidence", conversation.State);
        Assert.Empty(drafts);
        Assert.Contains("Không đọc được ảnh hợp lệ", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Theory]
    [InlineData("EVIDENCE_DONE", 1)]
    [InlineData("EVIDENCE_SKIP", 0)]
    public async Task EvidenceControls_ShowConfirmationWithAttachmentCount(
        string payload,
        int attachmentCount)
    {
        var conversation = CompleteDraftConversation("AwaitingEvidence");
        List<MessengerFeedbackDraftAttachment> drafts = attachmentCount == 0
            ? []
            :
            [
                DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
            ];
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        ConfigureArea(unitOfWork, conversation.AreaId!.Value, "Phường 7");
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook("evidence-done", "Xong", payload));

        Assert.Equal("AwaitingConfirmation", conversation.State);
        Assert.Contains(
            $"Ảnh minh chứng: {attachmentCount}",
            GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task EvidenceCompletionWithInactiveArea_ResetsAndDeletesAttachments()
    {
        var conversation = CompleteDraftConversation("AwaitingEvidence");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        ConfigureArea(unitOfWork, conversation.AreaId!.Value, "Phường 7", isActive: false);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook(
            "inactive-area",
            "Xong",
            "EVIDENCE_DONE"));

        Assert.Equal("AwaitingTitle", conversation.State);
        Assert.Null(conversation.Title);
        Assert.Null(conversation.AreaId);
        Assert.Empty(drafts);
        Assert.Contains("Khu vực không còn hợp lệ", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task Confirmation_CreatesMessengerFeedbackWithUploadedAttachmentsAndCleansDraft()
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var submissionRepository = unitOfWork.GetRepository<MessengerFeedbackSubmission>();
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
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
                "urban-service/messenger-feedbacks",
                Arg.Any<CancellationToken>())
            .Returns(new CloudinaryUploadResultDto
            {
                FileUrl = "https://cloudinary.example/evidence.jpg",
                FileType = "image/jpeg"
            });
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);

        await service.ProcessWebhookAsync(TextWebhook("confirm-1", "Xác nhận", "XAC NHAN"));

        Assert.Equal("Completed", conversation.State);
        Assert.Equal(feedbackId, conversation.FeedbackId);
        Assert.Empty(drafts);
        await feedbackService.Received(1).CreateAsync(
            submissionUserId,
            Arg.Is<FeedbackCreateRequest>(request =>
                request.SubmissionChannel == FeedbackSubmissionChannel.Messenger &&
                request.GeoSource == "Messenger"),
            Arg.Is<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>(attachments =>
                attachments.Count == 1 &&
                attachments.Single().FileUrl == "https://cloudinary.example/evidence.jpg"));
        await submissionRepository.Received(1).AddAsync(
            Arg.Is<MessengerFeedbackSubmission>(item =>
                item.ConversationId == conversation.ConversationId &&
                item.FeedbackId == feedbackId));
    }

    [Fact]
    public async Task Confirmation_WithoutImages_DoesNotDownloadOrUpload()
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.CreateAsync(
                submissionUserId,
                Arg.Any<FeedbackCreateRequest>(),
                Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>())
            .Returns(new FeedbackDetailDto { FeedbackId = Guid.NewGuid() });
        var cloudinaryService = Substitute.For<ICloudinaryService>();
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);

        await service.ProcessWebhookAsync(TextWebhook("confirm-empty", "Xác nhận", "XAC NHAN"));

        Assert.Equal("Completed", conversation.State);
        Assert.Equal(0, handler.GetRequestCount);
        await cloudinaryService.DidNotReceiveWithAnyArgs().UploadAsync(
            default!,
            default!,
            default,
            default!,
            default);
        await feedbackService.Received(1).CreateAsync(
            submissionUserId,
            Arg.Any<FeedbackCreateRequest>(),
            Arg.Is<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>(items => items.Count == 0));
    }

    [Theory]
    [InlineData("http-status")]
    [InlineData("mime")]
    [InlineData("content-length")]
    [InlineData("stream-size")]
    [InlineData("redirect-host")]
    public async Task InvalidDownloadedImage_IsNotUploadedOrSubmitted(string failureMode)
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
        var feedbackService = Substitute.For<IFeedbackService>();
        var cloudinaryService = Substitute.For<ICloudinaryService>();
        var handler = new RecordingHttpMessageHandler
        {
            GetResponseFactory = _ => InvalidImageResponse(failureMode)
        };
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        configuration["Messenger:MaxImageBytes"] = "3";
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);

        await service.ProcessWebhookAsync(TextWebhook(
            $"confirm-{failureMode}",
            "Xác nhận",
            "XAC NHAN"));

        Assert.Equal("AwaitingConfirmation", conversation.State);
        Assert.Null(conversation.LastMessageId);
        Assert.Single(drafts);
        await cloudinaryService.DidNotReceiveWithAnyArgs().UploadAsync(
            default!,
            default!,
            default,
            default!,
            default);
        await feedbackService.DidNotReceiveWithAnyArgs().CreateAsync(
            default,
            default!,
            default!);
        Assert.Contains("Ảnh nháp vẫn được giữ", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task UploadFailure_RestoresConfirmationAndSameMidCanRetry()
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
        var feedbackId = Guid.NewGuid();
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.CreateAsync(
                submissionUserId,
                Arg.Any<FeedbackCreateRequest>(),
                Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>())
            .Returns(new FeedbackDetailDto { FeedbackId = feedbackId });
        var shouldFail = true;
        var cloudinaryService = Substitute.For<ICloudinaryService>();
        cloudinaryService.UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (shouldFail)
                {
                    throw new InvalidOperationException("simulated upload failure");
                }

                return new CloudinaryUploadResultDto
                {
                    FileUrl = "https://cloudinary.example/evidence.jpg",
                    FileType = "image/jpeg"
                };
            });
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);
        var webhook = TextWebhook("confirm-retry", "Xác nhận", "XAC NHAN");

        await service.ProcessWebhookAsync(webhook);
        Assert.Equal("AwaitingConfirmation", conversation.State);
        Assert.Single(drafts);

        shouldFail = false;
        await service.ProcessWebhookAsync(webhook);

        Assert.Equal("Completed", conversation.State);
        Assert.Equal(feedbackId, conversation.FeedbackId);
        Assert.Empty(drafts);
        await feedbackService.Received(1).CreateAsync(
            submissionUserId,
            Arg.Any<FeedbackCreateRequest>(),
            Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>());
    }

    [Fact]
    public async Task FeedbackCreationFailure_RestoresConfirmationAndRetainsDraft()
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.CreateAsync(
                submissionUserId,
                Arg.Any<FeedbackCreateRequest>(),
                Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>())
            .Returns<Task<FeedbackDetailDto>>(_ => throw new InvalidOperationException(
                "simulated feedback failure"));
        var cloudinaryService = Substitute.For<ICloudinaryService>();
        cloudinaryService.UploadAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new CloudinaryUploadResultDto
            {
                FileUrl = "https://cloudinary.example/evidence.jpg",
                FileType = "image/jpeg"
            });
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService,
            cloudinaryService);

        await service.ProcessWebhookAsync(TextWebhook(
            "confirm-create-failure",
            "Xác nhận",
            "XAC NHAN"));

        Assert.Equal("AwaitingConfirmation", conversation.State);
        Assert.Null(conversation.LastMessageId);
        Assert.Single(drafts);
        Assert.Contains("Ảnh nháp vẫn được giữ", GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task RetriedConfirmation_AfterFeedbackWasCreated_DoesNotCreateAgain()
    {
        var feedbackId = Guid.NewGuid();
        var conversation = CompleteDraftConversation("Completed");
        conversation.FeedbackId = feedbackId;
        conversation.LastMessageId = "confirm-duplicate";
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var feedbackService = Substitute.For<IFeedbackService>();
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler),
            feedbackService);

        await service.ProcessWebhookAsync(TextWebhook(
            "confirm-duplicate",
            "Xác nhận",
            "XAC NHAN"));

        await feedbackService.DidNotReceiveWithAnyArgs().CreateAsync(
            default,
            default!,
            default!);
        Assert.Contains(feedbackId.ToString(), GetOutgoingText(Assert.Single(handler.RequestBodies)));
    }

    [Fact]
    public async Task DuplicateMidInPersistedSubmittingState_ResumesExactlyOnce()
    {
        var conversation = CompleteDraftConversation("Submitting");
        conversation.LastMessageId = "confirm-submitting";
        var unitOfWork = UnitOfWorkWithConversation(conversation);
        var submissionUserId = ConfigureSubmissionUser(unitOfWork);
        var feedbackId = Guid.NewGuid();
        var feedbackService = Substitute.For<IFeedbackService>();
        feedbackService.CreateAsync(
                submissionUserId,
                Arg.Any<FeedbackCreateRequest>(),
                Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>())
            .Returns(new FeedbackDetailDto { FeedbackId = feedbackId });
        var handler = new RecordingHttpMessageHandler();
        var configuration = BaseConfiguration();
        configuration["Messenger:SubmissionUserId"] = submissionUserId.ToString();
        var service = CreateService(
            configuration,
            unitOfWork,
            new HttpClient(handler),
            feedbackService);

        await service.ProcessWebhookAsync(TextWebhook(
            "confirm-submitting",
            "Xác nhận",
            "XAC NHAN"));

        Assert.Equal("Completed", conversation.State);
        Assert.Equal(feedbackId, conversation.FeedbackId);
        await feedbackService.Received(1).CreateAsync(
            submissionUserId,
            Arg.Any<FeedbackCreateRequest>(),
            Arg.Any<IReadOnlyCollection<UploadedFeedbackAttachmentDto>>());
    }

    [Theory]
    [InlineData("START_FEEDBACK", "AwaitingTitle")]
    [InlineData("MENU", "Idle")]
    [InlineData("CANCEL", "Idle")]
    public async Task ResetCommands_DeleteDraftAttachments(string payload, string expectedState)
    {
        var conversation = CompleteDraftConversation("AwaitingEvidence");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook("reset-1", payload, payload));

        Assert.Equal(expectedState, conversation.State);
        Assert.Empty(drafts);
        Assert.Null(conversation.Title);
        Assert.Null(conversation.AreaId);
    }

    [Fact]
    public async Task ApiReset_DeletesDraftAttachments()
    {
        var conversation = CompleteDraftConversation("AwaitingEvidence");
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var service = CreateService(BaseConfiguration(), unitOfWork);

        var result = await service.ResetConversationAsync(conversation.SenderPsid);

        Assert.Equal("AwaitingTitle", result.State);
        Assert.Empty(drafts);
    }

    [Fact]
    public async Task ConfirmationWithIncompleteDraft_ResetsAndDeletesAttachments()
    {
        var conversation = CompleteDraftConversation("AwaitingConfirmation");
        conversation.Description = null;
        var drafts = new List<MessengerFeedbackDraftAttachment>
        {
            DraftAttachment(conversation, "https://scontent.fbcdn.net/evidence.jpg", "image-1", 0)
        };
        var unitOfWork = UnitOfWorkWithConversation(conversation, drafts);
        var feedbackService = Substitute.For<IFeedbackService>();
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(
            BaseConfiguration(),
            unitOfWork,
            new HttpClient(handler),
            feedbackService);

        await service.ProcessWebhookAsync(TextWebhook("confirm-incomplete", "Xác nhận", "XAC NHAN"));

        Assert.Equal("AwaitingTitle", conversation.State);
        Assert.Empty(drafts);
        await feedbackService.DidNotReceiveWithAnyArgs().CreateAsync(
            default,
            default!,
            default!);
    }

    [Fact]
    public async Task FeedbackHistory_OnlyReturnsCurrentConversationSubmissions()
    {
        var conversation = Conversation("Completed");
        var ownFeedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            Title = "Đèn đường bị hỏng",
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
        var submissionRepository = unitOfWork.GetRepository<MessengerFeedbackSubmission>();
        submissionRepository.Entities.Returns(new[]
        {
            new MessengerFeedbackSubmission
            {
                ConversationId = conversation.ConversationId,
                FeedbackId = ownFeedback.FeedbackId,
                Feedback = ownFeedback,
                CreatedAt = ownFeedback.CreatedAt
            },
            new MessengerFeedbackSubmission
            {
                ConversationId = 20,
                FeedbackId = anotherFeedback.FeedbackId,
                Feedback = anotherFeedback,
                CreatedAt = anotherFeedback.CreatedAt
            }
        }.AsAsyncQueryable());
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(BaseConfiguration(), unitOfWork, new HttpClient(handler));

        await service.ProcessWebhookAsync(TextWebhook(
            "history-1",
            "Phản ánh đã gửi",
            "VIEW_FEEDBACKS:1"));

        var messageText = GetOutgoingText(Assert.Single(handler.RequestBodies));
        Assert.Contains(ownFeedback.Title, messageText);
        Assert.DoesNotContain(anotherFeedback.Title, messageText);
    }

    private static MessengerService CreateService(
        Dictionary<string, string?> values,
        IUnitOfWork? unitOfWork = null,
        HttpClient? httpClient = null,
        IFeedbackService? feedbackService = null,
        ICloudinaryService? cloudinaryService = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new MessengerService(
            httpClient ?? new HttpClient(new RecordingHttpMessageHandler()),
            configuration,
            unitOfWork ?? Substitute.For<IUnitOfWork>(),
            feedbackService ?? Substitute.For<IFeedbackService>(),
            cloudinaryService ?? Substitute.For<ICloudinaryService>(),
            NullLogger<MessengerService>.Instance);
    }

    private static IUnitOfWork UnitOfWorkWithConversation(
        MessengerFeedbackConversation conversation,
        List<MessengerFeedbackDraftAttachment>? drafts = null)
    {
        drafts ??= [];
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var conversationRepository = Substitute.For<IGenericRepository<MessengerFeedbackConversation>>();
        conversationRepository.Entities.Returns(new[] { conversation }.AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerFeedbackConversation>().Returns(conversationRepository);

        var attachmentRepository = Substitute.For<IGenericRepository<MessengerFeedbackDraftAttachment>>();
        attachmentRepository.Entities.Returns(_ => drafts.AsAsyncQueryable());
        attachmentRepository.AddRangeAsync(Arg.Any<IEnumerable<MessengerFeedbackDraftAttachment>>())
            .Returns(callInfo =>
            {
                drafts.AddRange(callInfo.Arg<IEnumerable<MessengerFeedbackDraftAttachment>>());
                return Task.CompletedTask;
            });
        attachmentRepository
            .When(repository => repository.DeleteRange(
                Arg.Any<IEnumerable<MessengerFeedbackDraftAttachment>>()))
            .Do(callInfo =>
            {
                foreach (var item in callInfo.Arg<IEnumerable<MessengerFeedbackDraftAttachment>>().ToList())
                {
                    drafts.Remove(item);
                }
            });
        unitOfWork.GetRepository<MessengerFeedbackDraftAttachment>().Returns(attachmentRepository);

        var submissionRepository = Substitute.For<IGenericRepository<MessengerFeedbackSubmission>>();
        submissionRepository.Entities.Returns(
            Array.Empty<MessengerFeedbackSubmission>().AsAsyncQueryable());
        unitOfWork.GetRepository<MessengerFeedbackSubmission>().Returns(submissionRepository);

        var areaRepository = Substitute.For<IGenericRepository<OperatingArea>>();
        areaRepository.Entities.Returns(Array.Empty<OperatingArea>().AsAsyncQueryable());
        unitOfWork.GetRepository<OperatingArea>().Returns(areaRepository);

        var userRepository = Substitute.For<IGenericRepository<User>>();
        userRepository.Entities.Returns(Array.Empty<User>().AsAsyncQueryable());
        unitOfWork.GetRepository<User>().Returns(userRepository);
        return unitOfWork;
    }

    private static void ConfigureArea(
        IUnitOfWork unitOfWork,
        int areaId,
        string areaName,
        bool isActive = true)
    {
        unitOfWork.GetRepository<OperatingArea>().Entities.Returns(new[]
        {
            new OperatingArea { AreaId = areaId, AreaName = areaName, IsActive = isActive }
        }.AsAsyncQueryable());
    }

    private static Guid ConfigureSubmissionUser(IUnitOfWork unitOfWork)
    {
        var submissionUserId = Guid.NewGuid();
        unitOfWork.GetRepository<User>().Entities.Returns(new[]
        {
            new User
            {
                UserId = submissionUserId,
                IsActive = true,
                Role = new Role { RoleName = UserRole.SERVICEUSER }
            }
        }.AsAsyncQueryable());
        return submissionUserId;
    }

    private static MessengerFeedbackConversation Conversation(string state)
    {
        return new MessengerFeedbackConversation
        {
            ConversationId = 10,
            PageId = "page-1",
            SenderPsid = "sender-1",
            State = state,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static MessengerFeedbackConversation CompleteDraftConversation(string state)
    {
        var conversation = Conversation(state);
        conversation.Title = "Đèn đường bị hỏng";
        conversation.Description = "Đèn tắt nhiều ngày";
        conversation.LocationText = "Đường số 1";
        conversation.AreaId = 7;
        return conversation;
    }

    private static MessengerFeedbackDraftAttachment DraftAttachment(
        MessengerFeedbackConversation conversation,
        string sourceUrl,
        string sourceMessageId,
        int sourceOrdinal)
    {
        return new MessengerFeedbackDraftAttachment
        {
            DraftAttachmentId = sourceOrdinal + 1,
            ConversationId = conversation.ConversationId,
            SourceUrl = sourceUrl,
            FileType = "image",
            SourceMessageId = sourceMessageId,
            SourceOrdinal = sourceOrdinal,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Dictionary<string, string?> BaseConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["Messenger:PageAccessToken"] = "page-token",
            ["Messenger:AllowedMediaHostSuffixes"] = "fbcdn.net,fbsbx.com"
        };
    }

    private static string TextWebhook(string messageId, string text, string? quickReplyPayload = null)
    {
        return JsonSerializer.Serialize(new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page-1",
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender-1" },
                            timestamp = 1,
                            message = new
                            {
                                mid = messageId,
                                text,
                                quick_reply = quickReplyPayload == null
                                    ? null
                                    : new { payload = quickReplyPayload }
                            }
                        }
                    }
                }
            }
        });
    }

    private static string ImageWebhook(string messageId, params string[] imageUrls)
    {
        return JsonSerializer.Serialize(new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page-1",
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender-1" },
                            timestamp = 1,
                            message = new
                            {
                                mid = messageId,
                                attachments = imageUrls.Select(url => new
                                {
                                    type = "image",
                                    payload = new { url }
                                })
                            }
                        }
                    }
                }
            }
        });
    }

    private static IReadOnlyList<JsonElement> GetQuickReplies(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement
            .GetProperty("message")
            .GetProperty("quick_replies")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToList();
    }

    private static string GetOutgoingText(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("message").GetProperty("text").GetString()!;
    }

    private static HttpResponseMessage InvalidImageResponse(string failureMode)
    {
        if (failureMode == "http-status")
        {
            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        }

        HttpContent content;
        if (failureMode == "mime")
        {
            content = new StringContent("not an image");
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        }
        else if (failureMode == "stream-size")
        {
            content = new StreamContent(new NonSeekableMemoryStream([1, 2, 3, 4]));
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        }
        else
        {
            content = new ByteArrayContent([1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
            RequestMessage = failureMode == "redirect-host"
                ? new HttpRequestMessage(HttpMethod.Get, "https://attacker.example/evidence.jpg")
                : null
        };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        public int GetRequestCount { get; private set; }

        public Func<HttpRequestMessage, HttpResponseMessage>? GetResponseFactory { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                GetRequestCount++;
                var response = GetResponseFactory?.Invoke(request) ?? CreateImageResponse();
                response.RequestMessage ??= request;
                return response;
            }

            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"recipient_id\":\"sender-1\"}"),
                RequestMessage = request
            };
        }

        private static HttpResponseMessage CreateImageResponse()
        {
            var content = new ByteArrayContent([1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class NonSeekableMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
    }
}
