using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class InteractionMessageService : IInteractionMessageService
{
    private const int MaxMessageLength = 4000;

    private readonly IUnitOfWork _uow;

    public InteractionMessageService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyCollection<InteractionMessageDto>> GetTicketMessagesAsync(
        Guid currentUserId,
        Guid feedbackId,
        bool includeInternal = false)
    {
        var currentUser = await GetActiveUserAsync(currentUserId);
        var feedback = await GetFeedbackAsync(feedbackId);

        var isResidentOwner = feedback.UserId == currentUserId;
        var canViewInternal = IsManagementActor(currentUser);

        if (canViewInternal)
        {
            await ManagementAccessRules.EnsureFeedbackReadAccessAsync(
                _uow,
                feedbackId,
                currentUserId);
        }

        if (!isResidentOwner && !canViewInternal)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem trao đổi của ticket này.");
        }

        var showInternal = includeInternal && canViewInternal;

        return await _uow.GetRepository<InteractionMessage>().Entities
            .AsNoTracking()
            .Include(m => m.User)
                .ThenInclude(u => u.Role)
            .Where(m => m.FeedbackId == feedbackId && (showInternal || !m.IsInternal))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.InteractionMessageId)
            .Select(m => ToDto(m))
            .ToListAsync();
    }

    public async Task<InteractionMessageDto> SendMessageAsync(
        Guid currentUserId,
        Guid feedbackId,
        InteractionMessageCreateRequest request)
    {
        ValidateMessage(request.MessageText);

        var currentUser = await GetActiveUserAsync(currentUserId);
        var feedback = await GetFeedbackAsync(feedbackId);

        var isResidentOwner = feedback.UserId == currentUserId;
        var isManagementActor = IsWorkflowActor(currentUser);

        if (isManagementActor)
        {
            await ManagementAccessRules.EnsureFeedbackReadAccessAsync(
                _uow,
                feedbackId,
                currentUserId);
        }

        if (!isResidentOwner && !isManagementActor)
        {
            throw new ForbiddenAccessException("Bạn không có quyền gửi trao đổi cho ticket này.");
        }

        if (request.IsInternal && !isManagementActor)
        {
            throw new ForbiddenAccessException("Chỉ Staff/Manager trong phạm vi phụ trách được gửi ghi chú nội bộ.");
        }

        var message = new InteractionMessage
        {
            FeedbackId = feedbackId,
            UserId = currentUserId,
            SenderType = ResolveSenderType(currentUser),
            MessageText = request.MessageText.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<InteractionMessage>().AddAsync(message);
        await _uow.SaveAsync();

        return await GetMessageDtoAsync(message.InteractionMessageId);
    }

    public async Task<InteractionMessageDto> AddSystemMessageAsync(
        Guid currentUserId,
        Guid feedbackId,
        SystemInteractionMessageCreateRequest request)
    {
        ValidateMessage(request.MessageText);

        var currentUser = await GetActiveUserAsync(currentUserId);

        if (!IsSystemActor(currentUser))
        {
            throw new ForbiddenAccessException("Bạn không có quyền tạo system message.");
        }

        _ = await GetFeedbackAsync(feedbackId);
        await ManagementAccessRules.EnsureFeedbackReadAccessAsync(
            _uow,
            feedbackId,
            currentUserId);

        var message = new InteractionMessage
        {
            FeedbackId = feedbackId,
            UserId = currentUserId,
            SenderType = "System",
            MessageText = request.MessageText.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<InteractionMessage>().AddAsync(message);
        await _uow.SaveAsync();

        return await GetMessageDtoAsync(message.InteractionMessageId);
    }

    private async Task<User> GetActiveUserAsync(Guid userId)
    {
        var user = await _uow.GetRepository<User>().Entities
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        return user ?? throw new UnauthorizedAccessException("Không tìm thấy người dùng hoặc tài khoản đã bị khóa.");
    }

    private async Task<Feedback> GetFeedbackAsync(Guid feedbackId)
    {
        var feedback = await _uow.GetRepository<Feedback>().Entities
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId);

        return feedback ?? throw new Exception("Không tìm thấy ticket.");
    }

    private async Task<InteractionMessageDto> GetMessageDtoAsync(int interactionMessageId)
    {
        var message = await _uow.GetRepository<InteractionMessage>().Entities
            .AsNoTracking()
            .Include(m => m.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(m => m.InteractionMessageId == interactionMessageId);

        return message is null
            ? throw new Exception("Không tìm thấy message vừa tạo.")
            : ToDto(message);
    }

    private static InteractionMessageDto ToDto(InteractionMessage message)
    {
        return new InteractionMessageDto
        {
            InteractionMessageId = message.InteractionMessageId,
            FeedbackId = message.FeedbackId,
            UserId = message.UserId,
            UserFullName = message.User?.FullName,
            UserEmail = message.User?.Email,
            UserRole = message.User?.Role?.RoleName,
            SenderType = message.SenderType,
            MessageText = message.MessageText,
            IsInternal = message.IsInternal,
            CreatedAt = message.CreatedAt
        };
    }

    private static void ValidateMessage(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new Exception("Message là bắt buộc.");
        }

        if (messageText.Trim().Length > MaxMessageLength)
        {
            throw new Exception($"Message không được vượt quá {MaxMessageLength} ký tự.");
        }
    }

    private static bool IsManagementActor(User user)
    {
        var role = user.Role?.RoleName?.ToUpperInvariant();
        return role is UserRole.SYSTEMADMIN
            or UserRole.SYSTEMSTAFF
            or UserRole.INTERACTIONMANAGER;
    }

    private static bool IsSystemActor(User user)
    {
        var role = user.Role?.RoleName?.ToUpperInvariant();
        return role is UserRole.INTERACTIONMANAGER;
    }

    private static bool IsWorkflowActor(User user)
    {
        var role = user.Role?.RoleName?.ToUpperInvariant();
        return role is UserRole.SYSTEMSTAFF or UserRole.INTERACTIONMANAGER;
    }

    private static string ResolveSenderType(User user)
    {
        return user.Role?.RoleName?.ToUpperInvariant() switch
        {
            UserRole.SERVICEUSER => "Resident",
            UserRole.SYSTEMSTAFF => "Staff",
            UserRole.SERVICEOPERATORSTAFF => "OperatorStaff",
            UserRole.INTERACTIONMANAGER => "Manager",
            UserRole.SYSTEMADMIN => "System",
            _ => "User"
        };
    }
}
