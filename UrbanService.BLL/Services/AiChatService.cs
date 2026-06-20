using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiChatService : IAiChatService
{
    private readonly IUnitOfWork _uow;
    private readonly IAiClient _aiClient;

    public AiChatService(IUnitOfWork uow, IAiClient aiClient)
    {
        _uow = uow;
        _aiClient = aiClient;
    }

    public async Task<AiChatResponse> SendAsync(
        Guid userId,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new Exception("Message la bat buoc.");
        }

        Feedback? feedback = null;
        if (request.FeedbackId.HasValue)
        {
            feedback = await _uow.GetRepository<Feedback>().Entities
                .AsNoTracking()
                .Include(f => f.Category)
                .FirstOrDefaultAsync(
                    f => f.FeedbackId == request.FeedbackId.Value && f.UserId == userId,
                    cancellationToken)
                ?? throw new Exception("Khong tim thay feedback cua nguoi dung.");
        }

        var conversation = await GetOrCreateConversationAsync(userId, request, cancellationToken);
        var now = DateTime.UtcNow;

        await _uow.GetRepository<AiMessage>().AddAsync(new AiMessage
        {
            AiConversationId = conversation.AiConversationId,
            SenderType = "User",
            MessageText = request.Message.Trim(),
            CreatedAt = now
        });

        var knowledge = await BuildKnowledgeContextAsync(feedback, request.Message, cancellationToken);
        var prompt = BuildChatPrompt(request.Message.Trim(), knowledge, feedback);
        var aiMessage = await _aiClient.ChatAsync(prompt, jsonFormat: false, cancellationToken: cancellationToken);

        var savedAiMessage = new AiMessage
        {
            AiConversationId = conversation.AiConversationId,
            SenderType = "AI",
            MessageText = aiMessage,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<AiMessage>().AddAsync(savedAiMessage);
        await _uow.SaveAsync();

        return new AiChatResponse
        {
            ConversationId = conversation.AiConversationId,
            Message = savedAiMessage.MessageText,
            CreatedAt = savedAiMessage.CreatedAt
        };
    }

    private async Task<AiConversation> GetOrCreateConversationAsync(
        Guid userId,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId is > 0)
        {
            return await _uow.GetRepository<AiConversation>().Entities
                .FirstOrDefaultAsync(
                    c => c.AiConversationId == request.ConversationId.Value && c.UserId == userId,
                    cancellationToken)
                ?? throw new Exception("Khong tim thay conversation cua nguoi dung.");
        }

        var title = request.Message.Trim();
        if (title.Length > 80)
        {
            title = title[..80];
        }

        var conversation = new AiConversation
        {
            UserId = userId,
            FeedbackId = request.FeedbackId,
            Title = title,
            StartedAt = DateTime.UtcNow,
            Status = "Active"
        };

        await _uow.GetRepository<AiConversation>().AddAsync(conversation);
        await _uow.SaveAsync();
        return conversation;
    }

    private async Task<string> BuildKnowledgeContextAsync(
        Feedback? feedback,
        string message,
        CancellationToken cancellationToken)
    {
        var query = _uow.GetRepository<AiKnowledgeSource>().Entities
            .AsNoTracking()
            .Where(k => k.IsActive && !string.IsNullOrWhiteSpace(k.Content));

        if (feedback != null)
        {
            query = query.Where(k => k.CategoryId == null || k.CategoryId == feedback.CategoryId);
        }
        else
        {
            query = query.Where(k => k.CategoryId == null);
        }

        var sources = await query.Take(20).ToListAsync(cancellationToken);
        var terms = message.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3)
            .Distinct()
            .ToArray();

        var selected = sources
            .Select(source => new
            {
                Source = source,
                Score = terms.Count(term =>
                    source.Title.ToLower().Contains(term) ||
                    (source.Content?.ToLower().Contains(term) ?? false))
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Source.CategoryId.HasValue)
            .Take(5)
            .Select(x => $"- {x.Source.Title}: {Truncate(x.Source.Content, 1000)}");

        return string.Join(Environment.NewLine, selected);
    }

    private static string BuildChatPrompt(string message, string knowledge, Feedback? feedback)
    {
        var feedbackContext = feedback == null
            ? "Khong co feedback cu the."
            : $"""
              Feedback lien quan:
              - Ma feedback: {feedback.FeedbackId}
              - Tieu de: {feedback.Title}
              - Mo ta: {feedback.Description}
              - Dia diem: {feedback.LocationText}
              - Trang thai: {feedback.Status}
              - Category: {feedback.Category.CategoryName}
              """;

        return $"""
        Ban la tro ly UrbanService cho nguoi dan.
        Chi tra loi dua tren knowledge duoc cung cap va thong tin feedback neu co.
        Neu khong du thong tin, hay noi ro la chua du thong tin va de xuat nguoi dan lien he nhan vien ho tro.
        Khong tu y hua thoi gian xu ly neu knowledge khong co.
        Tra loi bang tieng Viet, ngan gon, lich su.

        Knowledge:
        {knowledge}

        {feedbackContext}

        Cau hoi nguoi dan:
        {message}
        """;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
