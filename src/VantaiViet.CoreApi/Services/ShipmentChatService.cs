using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

/// <summary>Messages between a shipment's owner and one carrier who asked for it. Anyone else gets "not found".</summary>
public sealed class ShipmentChatService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentChatService
{
    private const string PendingStatus = "Pending";
    private const string AcceptedStatus = "Accepted";

    private sealed record Conversation(Guid OwnerUserId, Guid CarrierUserId, bool Open, bool CarrierAccepted);

    /// <summary>Finds the conversation the caller may see. The owner names the carrier; a carrier always talks to the owner.</summary>
    private async Task<ShipmentResult<Conversation>> ResolveAsync(Guid shipmentId, Guid? carrierUserId, CancellationToken cancellationToken)
    {
        var shipment = await db.Shipments.AsNoTracking().Where(x => x.Id == shipmentId)
            .Select(x => new { x.OwnerUserId, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (shipment is null)
        {
            return ShipmentResult<Conversation>.Fail("not_found");
        }

        var isOwner = shipment.OwnerUserId == actor.UserId;
        if (isOwner && carrierUserId is null)
        {
            return ShipmentResult<Conversation>.Fail("carrier_required");
        }

        var carrier = isOwner ? carrierUserId!.Value : actor.UserId;
        var request = await db.TransportRequests.AsNoTracking().Where(x => x.ShipmentId == shipmentId && x.DriverUserId == carrier)
            .Select(x => x.Status).SingleOrDefaultAsync(cancellationToken);
        var offer = await db.Set<ShipmentBroker>().AsNoTracking().Where(x => x.ShipmentId == shipmentId && x.BrokerUserId == carrier)
            .Select(x => x.Status).SingleOrDefaultAsync(cancellationToken);
        if (carrier == shipment.OwnerUserId || (request is null && offer is null))
        {
            return ShipmentResult<Conversation>.Fail("not_found");
        }

        var active = request is PendingStatus or AcceptedStatus || offer is PendingStatus or AcceptedStatus;
        var open = active && shipment.Status is ShipmentStatuses.Published or ShipmentStatuses.Booked;
        return new(new Conversation(shipment.OwnerUserId, carrier, open, request == AcceptedStatus || offer == AcceptedStatus));
    }

    public async Task<ShipmentResult<ChatResponse>> ListAsync(Guid shipmentId, Guid? carrierUserId, long afterId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(shipmentId, carrierUserId, cancellationToken);
        if (resolved.Data is not { } conversation)
        {
            return ShipmentResult<ChatResponse>.Fail(resolved.ErrorCode!);
        }

        var messages = await db.ShipmentMessages.AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId && x.CarrierUserId == conversation.CarrierUserId && x.Id > afterId)
            .OrderBy(x => x.Id).Take(ChatLimits.PageSize)
            .Select(x => new ChatMessageResponse(x.Id, x.SenderUserId, x.Body, x.SentAt, x.SenderUserId == actor.UserId)).ToListAsync(cancellationToken);

        // Numbers are shared once the two are connected (the carrier's request or offer was accepted), in both directions.
        var partnerId = conversation.OwnerUserId == actor.UserId ? conversation.CarrierUserId : conversation.OwnerUserId;
        var partner = await db.Users.AsNoTracking().Where(x => x.Id == partnerId).Select(x => new { x.DisplayName, x.PhoneNumber }).SingleAsync(cancellationToken);
        var showPhone = conversation.CarrierAccepted;
        return new(new ChatResponse(messages, new ChatPartner(partnerId, partner.DisplayName, showPhone ? partner.PhoneNumber : null), conversation.Open));
    }

    public async Task<ShipmentResult<ChatMessageResponse>> SendAsync(Guid shipmentId, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > ChatLimits.MaxBodyLength)
        {
            return ShipmentResult<ChatMessageResponse>.Fail("invalid_message");
        }

        var resolved = await ResolveAsync(shipmentId, request.CarrierUserId, cancellationToken);
        if (resolved.Data is not { } conversation)
        {
            return ShipmentResult<ChatMessageResponse>.Fail(resolved.ErrorCode!);
        }

        if (!conversation.Open)
        {
            return ShipmentResult<ChatMessageResponse>.Fail("chat_closed");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Clients read "everything after the last id I have". A sequence hands out ids when rows are inserted, not when
        // they commit, so two writers at once could commit 6 before 5 and a reader holding 6 would never see 5.
        // Writers of one conversation therefore take turns: an id is only drawn once the previous message is committed.
        var lockKey = $"chat:{shipmentId:N}:{conversation.CarrierUserId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var message = new ShipmentMessage { ShipmentId = shipmentId, CarrierUserId = conversation.CarrierUserId, SenderUserId = actor.UserId, Body = body, SentAt = clock.GetUtcNow() };
        db.ShipmentMessages.Add(message);
        var recipient = actor.UserId == conversation.OwnerUserId ? conversation.CarrierUserId : conversation.OwnerUserId;
        await NotificationEvents.RaiseAsync(db, clock, NotificationTypes.ChatMessage, recipient, shipmentId, actor.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new ChatMessageResponse(message.Id, message.SenderUserId, message.Body, message.SentAt, true));
    }
}
