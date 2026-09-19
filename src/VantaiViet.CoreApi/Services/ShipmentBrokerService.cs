using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Services;

public sealed class ShipmentBrokerService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentBrokerService
{
    private Task<bool> HasRoleAsync(Guid id,string role,CancellationToken ct) =>
        (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
         where u.Id == id && (u.AccountStatus == "Active" || u.AccountStatus == "PendingKyc") && r.NormalizedName == role
         select u.Id).AnyAsync(ct);

    public async Task<ShipmentResult<ShipmentBrokerResponse>> AssignAsync(Guid shipmentId,AssignBrokerRequest request,CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var shipment = await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={shipmentId} AND "OwnerUserId"={actor.UserId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        if (shipment is null) { return ShipmentResult<ShipmentBrokerResponse>.Fail("not_found"); }
        if (shipment.IsExternalOrder || !await HasRoleAsync(actor.UserId,"SHIPPER",cancellationToken)) { return ShipmentResult<ShipmentBrokerResponse>.Fail("forbidden"); }
        if (shipment.Status != "Published" || shipment.Version != request.Version || shipment.PickupAt <= clock.GetUtcNow()) { return ShipmentResult<ShipmentBrokerResponse>.Fail("conflict"); }
        if (request.BrokerUserId == actor.UserId || !await HasRoleAsync(request.BrokerUserId,"BROKER",cancellationToken)) { return ShipmentResult<ShipmentBrokerResponse>.Fail("invalid_broker"); }
        var assignment = await db.Set<ShipmentBroker>().SingleOrDefaultAsync(x => x.ShipmentId == shipmentId,cancellationToken);
        if (assignment is null)
        {
            assignment = new ShipmentBroker { ShipmentId = shipmentId, BrokerUserId = request.BrokerUserId, Status = "Pending" };
            db.Add(assignment);
        }
        else
        {
            assignment.BrokerUserId = request.BrokerUserId;
            assignment.Status = "Pending";
        }
        var response = await SaveAsync(shipment,assignment,"assigned",cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }

    public async Task<ShipmentResult<ShipmentBrokerResponse>> DecideAsync(Guid shipmentId,string action,BrokerDecisionRequest request,CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var shipment = await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={shipmentId} FOR UPDATE""").SingleOrDefaultAsync(cancellationToken);
        var assignment = await db.Set<ShipmentBroker>().SingleOrDefaultAsync(x => x.ShipmentId == shipmentId,cancellationToken);
        if (shipment is null || assignment is null || (action == "revoke" ? shipment.OwnerUserId != actor.UserId : assignment.BrokerUserId != actor.UserId))
        { return ShipmentResult<ShipmentBrokerResponse>.Fail("not_found"); }
        if (!await HasRoleAsync(actor.UserId,action == "revoke" ? "SHIPPER" : "BROKER",cancellationToken)) { return ShipmentResult<ShipmentBrokerResponse>.Fail("forbidden"); }
        if (shipment.Status != "Published" || shipment.Version != request.Version || shipment.PickupAt <= clock.GetUtcNow()
            || (action == "revoke" ? assignment.Status is not ("Pending" or "Accepted") : assignment.Status != "Pending"))
        { return ShipmentResult<ShipmentBrokerResponse>.Fail("conflict"); }
        var status = action switch { "accept" => "Accepted", "reject" => "Rejected", "revoke" => "Revoked", _ => null };
        if (status is null) { return ShipmentResult<ShipmentBrokerResponse>.Fail("invalid_action"); }
        assignment.Status = status;
        var response = await SaveAsync(shipment,assignment,action,cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response);
    }

    public async Task<ShipmentResult<IReadOnlyList<ShipmentBrokerResponse>>> ListAsync(int page,CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100000) { return ShipmentResult<IReadOnlyList<ShipmentBrokerResponse>>.Fail("invalid_pagination"); }
        if (!await HasRoleAsync(actor.UserId,"BROKER",cancellationToken) && !await HasRoleAsync(actor.UserId,"SHIPPER",cancellationToken))
        { return ShipmentResult<IReadOnlyList<ShipmentBrokerResponse>>.Fail("forbidden"); }
        return new(await (from a in db.Set<ShipmentBroker>() join s in db.Shipments on a.ShipmentId equals s.Id
            where a.BrokerUserId == actor.UserId || s.OwnerUserId == actor.UserId
            orderby a.UpdatedAt descending,a.ShipmentId
            select new ShipmentBrokerResponse(s.Id,s.Title,a.BrokerUserId,a.Status,s.Version))
            .Skip((page-1)*20).Take(20).ToListAsync(cancellationToken));
    }

    private async Task<ShipmentBrokerResponse> SaveAsync(Shipment shipment,ShipmentBroker assignment,string action,CancellationToken ct)
    {
        assignment.UpdatedAt = clock.GetUtcNow();
        shipment.Version++;
        shipment.UpdatedAt = assignment.UpdatedAt;
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.UserId,TargetType = "Shipment",TargetId = shipment.Id,
            Action = "broker." + action,Outcome = "Succeeded",OccurredAt = assignment.UpdatedAt });
        await db.SaveChangesAsync(ct);
        return new(shipment.Id,shipment.Title,assignment.BrokerUserId,assignment.Status,shipment.Version);
    }
}
