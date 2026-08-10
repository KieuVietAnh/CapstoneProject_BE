using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class ZaloWebhookInbox : IZaloWebhookInbox
{
    private const int RecoveryBatchSize = 500;
    private const int MaximumAttempts = 3;
    private readonly IUnitOfWork _uow;

    public ZaloWebhookInbox(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<long?> StoreAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        var eventKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var existingId = await Events
            .AsNoTracking()
            .Where(item => item.EventKey == eventKey)
            .Select(item => (long?)item.WebhookEventId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
        {
            return null;
        }

        var webhookEvent = new ZaloWebhookEvent
        {
            EventKey = eventKey,
            Payload = payload,
            Status = ZaloWebhookStatus.Pending,
            AttemptCount = 0,
            ReceivedAt = DateTime.UtcNow
        };
        await _uow.GetRepository<ZaloWebhookEvent>().AddAsync(webhookEvent);

        try
        {
            await _uow.SaveAsync();
            return webhookEvent.WebhookEventId;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            // The unique event key closes the small race between the lookup and insert.
            return null;
        }
    }

    public async Task<IReadOnlyCollection<long>> GetRecoverableEventIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await Events
            .AsNoTracking()
            .Where(item =>
                item.Status == ZaloWebhookStatus.Processing ||
                ((item.Status == ZaloWebhookStatus.Pending ||
                  item.Status == ZaloWebhookStatus.Failed) &&
                 item.AttemptCount < MaximumAttempts))
            .OrderBy(item => item.ReceivedAt)
            .Take(RecoveryBatchSize)
            .Select(item => item.WebhookEventId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ZaloWebhookEvent?> TryBeginProcessingAsync(
        long webhookEventId,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await Events.FirstOrDefaultAsync(
            item => item.WebhookEventId == webhookEventId,
            cancellationToken);
        if (webhookEvent == null || webhookEvent.Status == ZaloWebhookStatus.Completed)
        {
            return null;
        }

        webhookEvent.Status = ZaloWebhookStatus.Processing;
        webhookEvent.AttemptCount++;
        webhookEvent.LastError = null;
        await _uow.SaveAsync();
        return webhookEvent;
    }

    public async Task MarkCompletedAsync(
        long webhookEventId,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await Events.FirstOrDefaultAsync(
            item => item.WebhookEventId == webhookEventId,
            cancellationToken);
        if (webhookEvent == null)
        {
            return;
        }

        webhookEvent.Status = ZaloWebhookStatus.Completed;
        webhookEvent.ProcessedAt = DateTime.UtcNow;
        webhookEvent.LastError = null;
        webhookEvent.Payload = "{}";
        await _uow.SaveAsync();
    }

    public async Task MarkFailedAsync(
        long webhookEventId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await Events.FirstOrDefaultAsync(
            item => item.WebhookEventId == webhookEventId,
            cancellationToken);
        if (webhookEvent == null)
        {
            return;
        }

        webhookEvent.Status = ZaloWebhookStatus.Failed;
        webhookEvent.LastError = error.Length <= 2000 ? error : error[..2000];
        webhookEvent.ProcessedAt = null;
        await _uow.SaveAsync();
    }

    private IQueryable<ZaloWebhookEvent> Events =>
        _uow.GetRepository<ZaloWebhookEvent>().Entities;
}
