using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class ShipmentService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentService
{
    private async Task<bool> CanPostAsync(bool external, CancellationToken ct)
    {
        var roles = external ? new[] { "BROKER" } : new[] { "SHIPPER", "BROKER" };
        return await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where u.Id == actor.UserId && (u.AccountStatus == "Active" || u.AccountStatus == "PendingKyc")
                && roles.Contains(r.NormalizedName!)
            select u.Id).AnyAsync(ct);
    }
    private static ShipmentResponse Map(Shipment s) => new(s.Id,s.Title,s.PickupAddress,s.DeliveryAddress,s.WeightKg,s.PickupAt,s.IsExternalOrder,s.Status,s.Version,
        s.PickupLatitude.HasValue ? new(s.PickupLatitude.Value,s.PickupLongitude!.Value) : null,
        s.DeliveryLatitude.HasValue ? new(s.DeliveryLatitude.Value,s.DeliveryLongitude!.Value) : null);
    private static bool ValidStops(CoordinateDto? pickup, CoordinateDto? delivery) =>
        (pickup is null && delivery is null) || (ValidCoordinate(pickup) && ValidCoordinate(delivery));
    private static bool ValidCoordinate(CoordinateDto? p) => p is not null &&
        double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude) && p.Latitude is >=-90 and <=90 && p.Longitude is >=-180 and <=180;
    public async Task<ShipmentResult<ShipmentResponse>> CreateAsync(CreateShipmentRequest r, CancellationToken cancellationToken)
    {
        if(!ValidStops(r.Pickup,r.Delivery)) { return ShipmentResult<ShipmentResponse>.Fail("invalid_coordinates"); }
        if (!await CanPostAsync(r.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (r.PickupAt <= clock.GetUtcNow() || (r.IsExternalOrder && (string.IsNullOrWhiteSpace(r.ExternalCustomerName) || string.IsNullOrWhiteSpace(r.ExternalCustomerPhone))))
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_details");
        }

        var s = new Shipment { Id=Guid.NewGuid(), OwnerUserId=actor.UserId,Title=r.Title.Trim(),PickupAddress=r.PickupAddress.Trim(),
            DeliveryAddress=r.DeliveryAddress.Trim(),WeightKg=r.WeightKg,PickupAt=r.PickupAt.ToUniversalTime(),IsExternalOrder=r.IsExternalOrder,
            ExternalCustomerName=r.IsExternalOrder?r.ExternalCustomerName?.Trim():null,ExternalCustomerPhone=r.IsExternalOrder?r.ExternalCustomerPhone:null,
            CreatedAt=clock.GetUtcNow(),UpdatedAt=clock.GetUtcNow(),
            PickupLatitude=r.Pickup?.Latitude,PickupLongitude=r.Pickup?.Longitude,
            DeliveryLatitude=r.Delivery?.Latitude,DeliveryLongitude=r.Delivery?.Longitude };
        db.Shipments.Add(s);
        return await SaveAsync(s,"created",cancellationToken);
    }
    public async Task<ShipmentResult<ShipmentOwnerResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var s=await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && (x.OwnerUserId==actor.UserId ||
            db.Set<ShipmentBroker>().Any(a=>a.ShipmentId==id && a.BrokerUserId==actor.UserId && a.Status=="Accepted")),cancellationToken);
        return s is null ? ShipmentResult<ShipmentOwnerResponse>.Fail("not_found") : new(new(Map(s),s.ExternalCustomerName,s.ExternalCustomerPhone));
    }
    public async Task<ShipmentResult<IReadOnlyList<ShipmentResponse>>> ListAsync(bool mine,int page,int pageSize,CancellationToken cancellationToken)
    {
        if(page<1 || page>100000 || pageSize<1 || pageSize>100)
        {
            return ShipmentResult<IReadOnlyList<ShipmentResponse>>.Fail("invalid_pagination");
        }

        var q=db.Shipments.AsNoTracking();
        q=mine?q.Where(x=>x.OwnerUserId==actor.UserId):q.Where(x=>x.Status=="Published" && x.PickupAt>clock.GetUtcNow());
        var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x=>new ShipmentResponse(x.Id,x.Title,x.PickupAddress,x.DeliveryAddress,x.WeightKg,x.PickupAt,x.IsExternalOrder,x.Status,x.Version,
                x.PickupLatitude.HasValue ? new CoordinateDto(x.PickupLatitude.Value,x.PickupLongitude!.Value) : null,
                x.DeliveryLatitude.HasValue ? new CoordinateDto(x.DeliveryLatitude.Value,x.DeliveryLongitude!.Value) : null)).ToListAsync(cancellationToken);
        return new(rows);
    }
    public async Task<ShipmentResult<ShipmentResponse>> UpdateAsync(Guid id,UpdateShipmentRequest r,CancellationToken cancellationToken)
    {
        if(!ValidStops(r.Pickup,r.Delivery)) { return ShipmentResult<ShipmentResponse>.Fail("invalid_coordinates"); }
        var s=await db.Shipments.SingleOrDefaultAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken);
        if(s is null)
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        if (!await CanPostAsync(s.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (s.Status!="Draft" || s.Version!=r.Version)
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (r.PickupAt<=clock.GetUtcNow())
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_details");
        }

        s.Title=r.Title.Trim();s.PickupAddress=r.PickupAddress.Trim();s.DeliveryAddress=r.DeliveryAddress.Trim();s.WeightKg=r.WeightKg;s.PickupAt=r.PickupAt.ToUniversalTime();
        // Older clients omit both coordinates: preserve existing stops when editing text.
        if(r.Pickup is not null && r.Delivery is not null)
        {
            s.PickupLatitude=r.Pickup.Latitude;s.PickupLongitude=r.Pickup.Longitude;
            s.DeliveryLatitude=r.Delivery.Latitude;s.DeliveryLongitude=r.Delivery.Longitude;
        }
        return await SaveAsync(s,"updated",cancellationToken);
    }
    public async Task<ShipmentResult<ShipmentResponse>> TransitionAsync(Guid id,string action,long version,CancellationToken cancellationToken)
    {
        var s=await db.Shipments.SingleOrDefaultAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken);
        if(s is null)
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        if (!await CanPostAsync(s.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (s.Version!=version || s.Status is "Cancelled" or "Booked" or "Completed")
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="publish" && (s.Status!="Draft" || s.PickupAt<=clock.GetUtcNow()))
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="delete" && s.Status!="Draft")
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="delete")
        {
            db.Shipments.Remove(s);
        }
        else if(action=="publish")
        {
            s.Status="Published";
        }
        else if(action=="cancel")
        {
            s.Status="Cancelled";
        }
        else
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_action");
        }

        return await SaveAsync(s,action,cancellationToken);
    }
    private async Task<ShipmentResult<ShipmentResponse>> SaveAsync(Shipment s,string action,CancellationToken ct)
    {
        s.Version++;s.UpdatedAt=clock.GetUtcNow();
        db.AuditEvents.Add(new AuditEvent { ActorUserId=actor.UserId,Action="shipment."+action,TargetType="Shipment",TargetId=s.Id,Outcome="Succeeded",OccurredAt=clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateConcurrencyException) { db.ChangeTracker.Clear();return ShipmentResult<ShipmentResponse>.Fail("conflict"); }
        return new(Map(s));
    }
}
