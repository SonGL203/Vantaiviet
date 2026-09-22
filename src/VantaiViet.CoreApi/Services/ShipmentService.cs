using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Services;
public sealed class ShipmentService(CoreDbContext db, ICurrentActor actor, TimeProvider clock) : IShipmentService
{
    private const int MinimumOtherCargoDescriptionLength = 3;
    private const string PendingStatus = "Pending";
    private const string AcceptedStatus = "Accepted";
    private const string RejectedStatus = "Rejected";
    private const string RevokedStatus = "Revoked";
    private const string HasTakersReason = "has_takers";
    private const int MaxSearchLength = 100;
    private static readonly string[] AllStatuses =
        [ShipmentStatuses.Draft,ShipmentStatuses.Published,ShipmentStatuses.Booked,ShipmentStatuses.Completed,ShipmentStatuses.Cancelled];

    private sealed record DetailSet(int Quantity, string QuantityUnit, string VehicleType, decimal? ProposedPrice, DateTimeOffset? DeliverByAt, string? Notes, decimal? BrokerCommissionRate);

    private Task<bool> CanPostAsync(bool external, CancellationToken ct)
    {
        return ShipmentRules.CanPostAsync(db,actor.UserId,external,ct);
    }
    private static ShipmentResponse Map(Shipment s, int photoCount, int pendingRequests = 0)
    {
        return new(s.Id,s.Title,s.PickupAddress,s.DeliveryAddress,s.WeightKg,s.PickupAt,s.IsExternalOrder,s.Status,s.Version,
            s.PickupLatitude.HasValue ? new(s.PickupLatitude.Value,s.PickupLongitude!.Value) : null,
            s.DeliveryLatitude.HasValue ? new(s.DeliveryLatitude.Value,s.DeliveryLongitude!.Value) : null,
            s.CargoType,s.CargoDescription,s.Quantity,s.QuantityUnit,s.VehicleType,
            s.ProposedPrice?.ToString("F0",CultureInfo.InvariantCulture),s.Currency,s.DeliverByAt,s.Notes,photoCount,s.CreatedAt,s.Code,pendingRequests,
            s.BrokerCommissionRate);
    }
    private static string? CargoIssue(string cargoType,string? description)
    {
        if (!CargoTypes.All.Contains(cargoType)) { return "invalid_cargo_type"; }
        if (cargoType==CargoTypes.Other && (description?.Trim().Length ?? 0)<MinimumOtherCargoDescriptionLength) { return "cargo_description_required"; }
        return null;
    }
    private static string? CleanText(string? text)
    {
        var trimmed=text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
    // Money is an exact decimal string of whole dong; anything else is refused rather than rounded.
    private static bool TryParsePrice(string? text,out decimal? price)
    {
        price=null;
        if (string.IsNullOrEmpty(text)) { return true; }
        if (text.Length>ShipmentLimits.MaxPriceDigits || !decimal.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out var value) || value<=0)
        {
            return false;
        }

        price=value;
        return true;
    }
    private static ShipmentResult<DetailSet> BuildDetails(int quantity,string? unit,string? vehicleType,string? price,DateTimeOffset? deliverBy,string? notes,decimal? commissionRate)
    {
        if (quantity is <1 or >ShipmentLimits.MaxQuantity) { return ShipmentResult<DetailSet>.Fail("invalid_quantity"); }
        if (unit is null || !QuantityUnits.All.Contains(unit)) { return ShipmentResult<DetailSet>.Fail("invalid_quantity_unit"); }
        if (vehicleType is null || !RequiredVehicleTypes.All.Contains(vehicleType)) { return ShipmentResult<DetailSet>.Fail("invalid_vehicle_type"); }
        if (!TryParsePrice(price,out var parsedPrice)) { return ShipmentResult<DetailSet>.Fail("invalid_price"); }
        if (commissionRate is < 0 or > 100) { return ShipmentResult<DetailSet>.Fail("invalid_commission_rate"); }
        return new(new DetailSet(quantity,unit,vehicleType,parsedPrice,deliverBy?.ToUniversalTime(),CleanText(notes),commissionRate));
    }
    private static bool ValidStops(CoordinateDto? pickup, CoordinateDto? delivery)
    {
        return (pickup is null && delivery is null) || (ValidCoordinate(pickup) && ValidCoordinate(delivery));
    }
    private static bool ValidCoordinate(CoordinateDto? p)
    {
        return p is not null &&
            double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude) && p.Latitude is >=-90 and <=90 && p.Longitude is >=-180 and <=180;
    }
    public async Task<ShipmentResult<ShipmentResponse>> CreateAsync(CreateShipmentRequest r, CancellationToken cancellationToken)
    {
        if(!ValidStops(r.Pickup,r.Delivery)) { return ShipmentResult<ShipmentResponse>.Fail("invalid_coordinates"); }
        var cargoIssue=CargoIssue(r.CargoType,r.CargoDescription);
        if (cargoIssue is not null) { return ShipmentResult<ShipmentResponse>.Fail(cargoIssue); }
        var built=BuildDetails(r.Quantity,r.QuantityUnit,r.VehicleType,r.ProposedPrice,r.DeliverByAt,r.Notes,r.BrokerCommissionRate);
        if (built.Data is not { } details) { return ShipmentResult<ShipmentResponse>.Fail(built.ErrorCode ?? "invalid_details"); }
        if (details.DeliverByAt<=r.PickupAt) { return ShipmentResult<ShipmentResponse>.Fail("invalid_deliver_by"); }
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
            DeliveryLatitude=r.Delivery?.Latitude,DeliveryLongitude=r.Delivery?.Longitude,
            CargoType=r.CargoType,CargoDescription=CleanText(r.CargoDescription),
            Quantity=details.Quantity,QuantityUnit=details.QuantityUnit,VehicleType=details.VehicleType,
            ProposedPrice=details.ProposedPrice,DeliverByAt=details.DeliverByAt,Notes=details.Notes,BrokerCommissionRate=details.BrokerCommissionRate };
        db.Shipments.Add(s);
        return await SaveAsync(s,"created",cancellationToken);
    }
    public async Task<ShipmentResult<ShipmentOwnerResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var s=await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && (x.OwnerUserId==actor.UserId ||
            db.Set<ShipmentBroker>().Any(a=>a.ShipmentId==id && a.BrokerUserId==actor.UserId && a.Status==AcceptedStatus)),cancellationToken);
        if (s is null) { return ShipmentResult<ShipmentOwnerResponse>.Fail("not_found"); }
        var photos=await db.ShipmentPhotos.AsNoTracking().Where(p=>p.ShipmentId==id).OrderBy(p=>p.CreatedAt).ThenBy(p=>p.Id)
            .Select(p=>new ShipmentPhotoInfo(p.Id,p.ContentType,p.SizeBytes,p.CreatedAt)).ToListAsync(cancellationToken);
        var waiting=await ShipmentRules.PendingRequestsAsync(db,id,cancellationToken);
        return new(new(Map(s,photos.Count,waiting),s.ExternalCustomerName,s.ExternalCustomerPhone,photos,await ActionsAsync(s,cancellationToken)));
    }
    public async Task<ShipmentResult<IReadOnlyList<ShipmentResponse>>> ListAsync(bool mine,int page,int pageSize,string? search,string? status,CancellationToken cancellationToken)
    {
        if(page<1 || page>100000 || pageSize<1 || pageSize>100)
        {
            return ShipmentResult<IReadOnlyList<ShipmentResponse>>.Fail("invalid_pagination");
        }

        var term=CleanText(search);
        if (term is not null && term.Length>MaxSearchLength) { return ShipmentResult<IReadOnlyList<ShipmentResponse>>.Fail("invalid_query"); }
        var statuses=(status ?? string.Empty).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if (statuses.Any(x=>!AllStatuses.Contains(x))) { return ShipmentResult<IReadOnlyList<ShipmentResponse>>.Fail("invalid_status"); }

        var q=db.Shipments.AsNoTracking();
        if (mine)
        {
            q=q.Where(x=>x.OwnerUserId==actor.UserId);
            if (statuses.Length>0) { q=q.Where(x=>statuses.Contains(x.Status)); }
            if (term is not null)
            {
                // A shipper types a reference or a place name; the code, the title or either end of the route can match.
                var pattern="%"+term.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
                q=q.Where(x=>EF.Functions.ILike(x.Code,pattern,"\\") || EF.Functions.ILike(x.Title,pattern,"\\")
                    || EF.Functions.ILike(x.PickupAddress,pattern,"\\") || EF.Functions.ILike(x.DeliveryAddress,pattern,"\\"));
            }
        }
        else
        {
            q=q.Where(x=>x.Status==ShipmentStatuses.Published && x.PickupAt>clock.GetUtcNow());
        }

        var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x=>new { Shipment=x, PhotoCount=db.ShipmentPhotos.Count(p=>p.ShipmentId==x.Id),
                Waiting=db.TransportRequests.Count(r=>r.ShipmentId==x.Id && r.Status==PendingStatus)
                    + db.Set<ShipmentBroker>().Count(b=>b.ShipmentId==x.Id && b.Status==PendingStatus) }).ToListAsync(cancellationToken);
        return new(rows.Select(x=>Map(x.Shipment,x.PhotoCount,x.Waiting)).ToList());
    }
    public async Task<ShipmentResult<ShipmentResponse>> UpdateAsync(Guid id,UpdateShipmentRequest r,CancellationToken cancellationToken)
    {
        if(!ValidStops(r.Pickup,r.Delivery)) { return ShipmentResult<ShipmentResponse>.Fail("invalid_coordinates"); }
        var cargoIssue=r.CargoType is null ? null : CargoIssue(r.CargoType,r.CargoDescription);
        if (cargoIssue is not null) { return ShipmentResult<ShipmentResponse>.Fail(cargoIssue); }
        DetailSet? replacement=null;
        if (r.Quantity is { } quantity)
        {
            var built=BuildDetails(quantity,r.QuantityUnit,r.VehicleType,r.ProposedPrice,r.DeliverByAt,r.Notes,r.BrokerCommissionRate);
            if (built.Data is null) { return ShipmentResult<ShipmentResponse>.Fail(built.ErrorCode ?? "invalid_details"); }
            replacement=built.Data;
        }

        var s=await db.Shipments.SingleOrDefaultAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken);
        if(s is null)
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        if (!await CanPostAsync(s.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (s.Status is not (ShipmentStatuses.Draft or ShipmentStatuses.Published) || s.Version!=r.Version)
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        // Anyone who has already asked for the job (or was offered it) agreed to the old terms. Every request and
        // broker action bumps the version, so one arriving after this check makes the save below fail instead.
        if (s.Status==ShipmentStatuses.Published && await HasTakersAsync(id,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail(HasTakersReason);
        }

        if (r.PickupAt<=clock.GetUtcNow())
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_details");
        }

        var deliverBy=replacement is null ? s.DeliverByAt : replacement.DeliverByAt;
        if (deliverBy<=r.PickupAt)
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_deliver_by");
        }

        s.Title=r.Title.Trim();s.PickupAddress=r.PickupAddress.Trim();s.DeliveryAddress=r.DeliveryAddress.Trim();s.WeightKg=r.WeightKg;s.PickupAt=r.PickupAt.ToUniversalTime();
        if(r.CargoType is not null) { s.CargoType=r.CargoType;s.CargoDescription=CleanText(r.CargoDescription); }
        if(replacement is not null)
        {
            s.Quantity=replacement.Quantity;s.QuantityUnit=replacement.QuantityUnit;s.VehicleType=replacement.VehicleType;
            s.ProposedPrice=replacement.ProposedPrice;s.DeliverByAt=replacement.DeliverByAt;s.Notes=replacement.Notes;s.BrokerCommissionRate=replacement.BrokerCommissionRate;
            // A client that sends the details sends the whole state, so missing points clear the old ones
            // rather than leaving coordinates that no longer belong to the edited address.
            s.PickupLatitude=r.Pickup?.Latitude;s.PickupLongitude=r.Pickup?.Longitude;
            s.DeliveryLatitude=r.Delivery?.Latitude;s.DeliveryLongitude=r.Delivery?.Longitude;
        }
        // Older clients omit both coordinates: preserve existing stops when editing text.
        else if(r.Pickup is not null && r.Delivery is not null)
        {
            s.PickupLatitude=r.Pickup.Latitude;s.PickupLongitude=r.Pickup.Longitude;
            s.DeliveryLatitude=r.Delivery.Latitude;s.DeliveryLongitude=r.Delivery.Longitude;
        }
        return await SaveAsync(s,"updated",cancellationToken);
    }
    public async Task<ShipmentResult<ShipmentResponse>> TransitionAsync(Guid id,string action,long version,CancellationToken cancellationToken)
    {
        if (action=="cancel")
        {
            return await CancelAsync(id,version,cancellationToken);
        }

        var s=await db.Shipments.SingleOrDefaultAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken);
        if(s is null)
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        if (!await CanPostAsync(s.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (s.Version!=version || s.Status is ShipmentStatuses.Cancelled or ShipmentStatuses.Booked or ShipmentStatuses.Completed)
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="publish" && (s.Status!=ShipmentStatuses.Draft || s.PickupAt<=clock.GetUtcNow()))
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="delete" && s.Status!=ShipmentStatuses.Draft)
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        if (action=="delete")
        {
            db.Shipments.Remove(s);
        }
        else if(action=="publish")
        {
            s.Status=ShipmentStatuses.Published;
        }
        else
        {
            return ShipmentResult<ShipmentResponse>.Fail("invalid_action");
        }

        if (action=="publish")
        {
            await NotifyMatchingRouteInterestsAsync(s,cancellationToken);
        }

        return await SaveAsync(s,action,cancellationToken);
    }

    /// <summary>"Có hàng phù hợp": every broker who watches a route this shipment's pickup and delivery both fall
    /// within (a free-text partial match, done in memory since each broker only ever watches a handful of routes)
    /// hears about it as soon as it is published.</summary>
    private async Task NotifyMatchingRouteInterestsAsync(Shipment s,CancellationToken ct)
    {
        var interests=await db.BrokerRouteInterests.AsNoTracking().ToListAsync(ct);
        var watchers=interests.Where(x=>s.PickupAddress.Contains(x.FromArea,StringComparison.OrdinalIgnoreCase)
            && s.DeliveryAddress.Contains(x.ToArea,StringComparison.OrdinalIgnoreCase)).Select(x=>x.BrokerUserId).Distinct();
        foreach (var brokerUserId in watchers)
        {
            await NotificationEvents.RaiseAsync(db,clock,NotificationTypes.MatchingCargoAvailable,brokerUserId,s.Id,actor.UserId,ct);
        }
    }
    private async Task<ShipmentResult<ShipmentResponse>> CancelAsync(Guid id,long version,CancellationToken cancellationToken)
    {
        if (!await db.Shipments.AnyAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        // The lock makes a confirmation and this cancellation take turns instead of both succeeding.
        var s=await db.Shipments.FromSqlInterpolated($"""SELECT * FROM public."Shipments" WHERE "Id"={id} AND "OwnerUserId"={actor.UserId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        if(s is null)
        {
            return ShipmentResult<ShipmentResponse>.Fail("not_found");
        }

        if (!await CanPostAsync(s.IsExternalOrder,cancellationToken))
        {
            return ShipmentResult<ShipmentResponse>.Fail("forbidden");
        }

        if (s.Version!=version || s.Status is not (ShipmentStatuses.Draft or ShipmentStatuses.Published))
        {
            return ShipmentResult<ShipmentResponse>.Fail("conflict");
        }

        // Left alone, waiting requests and offers would stay open on a shipment nobody can take any more.
        var now=clock.GetUtcNow();
        var waiting=await db.TransportRequests.Where(x=>x.ShipmentId==id && x.Status==PendingStatus).Select(x=>x.DriverUserId).ToListAsync(cancellationToken);
        var brokers=await db.Set<ShipmentBroker>().Where(x=>x.ShipmentId==id && (x.Status==PendingStatus || x.Status==AcceptedStatus)).Select(x=>x.BrokerUserId).ToListAsync(cancellationToken);
        await db.TransportRequests.Where(x=>x.ShipmentId==id && x.Status==PendingStatus)
            .ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Status,RejectedStatus),cancellationToken);
        await db.Set<ShipmentBroker>().Where(x=>x.ShipmentId==id && (x.Status==PendingStatus || x.Status==AcceptedStatus))
            .ExecuteUpdateAsync(x=>x.SetProperty(p=>p.Status,RevokedStatus).SetProperty(p=>p.UpdatedAt,now),cancellationToken);
        s.Status=ShipmentStatuses.Cancelled;
        foreach (var userId in waiting.Concat(brokers).Distinct())
        {
            await NotificationEvents.RaiseAsync(db,clock,NotificationTypes.ShipmentCancelled,userId,id,actor.UserId,cancellationToken);
        }
        var result=await SaveAsync(s,"cancel",cancellationToken);
        if (result.ErrorCode is null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }
    public async Task<ShipmentResult<ShipmentSummaryResponse>> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts=await db.Shipments.AsNoTracking().Where(x=>x.OwnerUserId==actor.UserId).GroupBy(x=>x.Status)
            .Select(g=>new { Status=g.Key, Count=g.Count() }).ToListAsync(cancellationToken);
        int Of(string status) { return counts.Where(x=>x.Status==status).Select(x=>x.Count).SingleOrDefault(); }
        // Only an open shipment can still be answered, so requests left on a closed one are not "waiting".
        var waiting=await db.TransportRequests.CountAsync(r=>r.Status==PendingStatus && db.Shipments.Any(s=>s.Id==r.ShipmentId && s.OwnerUserId==actor.UserId && s.Status==ShipmentStatuses.Published),cancellationToken);
        var everSentOut=Of(ShipmentStatuses.Published)+Of(ShipmentStatuses.Booked)+Of(ShipmentStatuses.Completed)+Of(ShipmentStatuses.Cancelled);
        double? foundVehicleRate=everSentOut==0 ? null : Math.Round((Of(ShipmentStatuses.Booked)+Of(ShipmentStatuses.Completed))*100.0/everSentOut,1);
        return new(new ShipmentSummaryResponse(Of(ShipmentStatuses.Draft),Of(ShipmentStatuses.Published),Of(ShipmentStatuses.Booked),
            Of(ShipmentStatuses.Completed),Of(ShipmentStatuses.Cancelled),waiting,foundVehicleRate));
    }
    /// <summary>Feature 24 ("Quản lý"): the owner's own routes actually reused (posted more than once), most-posted first.
    /// Addresses are matched exactly (free text), so the same place typed two different ways counts as two routes.</summary>
    public async Task<ShipmentResult<IReadOnlyList<FrequentRouteResponse>>> FrequentRoutesAsync(int take,CancellationToken cancellationToken)
    {
        if (take<1 || take>20) { return ShipmentResult<IReadOnlyList<FrequentRouteResponse>>.Fail("invalid_pagination"); }
        var rows=await db.Shipments.AsNoTracking().Where(x=>x.OwnerUserId==actor.UserId)
            .GroupBy(x=>new { x.PickupAddress, x.DeliveryAddress })
            .Where(g=>g.Count()>1)
            .Select(g=>new { g.Key.PickupAddress, g.Key.DeliveryAddress, Count=g.Count(), LastUsedAt=g.Max(x=>x.CreatedAt) })
            .OrderByDescending(x=>x.Count).ThenByDescending(x=>x.LastUsedAt).Take(take).ToListAsync(cancellationToken);
        return new(rows.Select(x=>new FrequentRouteResponse(x.PickupAddress,x.DeliveryAddress,x.Count,x.LastUsedAt)).ToList());
    }
    /// <summary>Starts a new shipment from an existing one: a Published repost of a cancelled order, or a Draft copy of any order.</summary>
    public async Task<ShipmentResult<ShipmentResponse>> CloneAsync(Guid id,CloneShipmentRequest r,bool repost,CancellationToken cancellationToken)
    {
        var source=await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.OwnerUserId==actor.UserId,cancellationToken);
        if (source is null) { return ShipmentResult<ShipmentResponse>.Fail("not_found"); }
        if (!await CanPostAsync(source.IsExternalOrder,cancellationToken)) { return ShipmentResult<ShipmentResponse>.Fail("forbidden"); }
        if (repost && source.Status!=ShipmentStatuses.Cancelled) { return ShipmentResult<ShipmentResponse>.Fail("conflict"); }
        var now=clock.GetUtcNow();
        if (r.PickupAt<=now) { return ShipmentResult<ShipmentResponse>.Fail("invalid_details"); }
        var deliverBy=r.DeliverByAt ?? (source.DeliverByAt is { } previous ? r.PickupAt+(previous-source.PickupAt) : (DateTimeOffset?)null);
        if (deliverBy<=r.PickupAt) { return ShipmentResult<ShipmentResponse>.Fail("invalid_deliver_by"); }

        var copy=new Shipment { Id=Guid.NewGuid(),OwnerUserId=actor.UserId,Title=source.Title,PickupAddress=source.PickupAddress,
            DeliveryAddress=source.DeliveryAddress,WeightKg=source.WeightKg,PickupAt=r.PickupAt.ToUniversalTime(),
            IsExternalOrder=source.IsExternalOrder,ExternalCustomerName=source.ExternalCustomerName,ExternalCustomerPhone=source.ExternalCustomerPhone,
            CargoType=source.CargoType,CargoDescription=source.CargoDescription,Quantity=source.Quantity,QuantityUnit=source.QuantityUnit,
            VehicleType=source.VehicleType,ProposedPrice=source.ProposedPrice,Currency=source.Currency,DeliverByAt=deliverBy?.ToUniversalTime(),
            BrokerCommissionRate=source.BrokerCommissionRate,
            Notes=source.Notes,PickupLatitude=source.PickupLatitude,PickupLongitude=source.PickupLongitude,
            DeliveryLatitude=source.DeliveryLatitude,DeliveryLongitude=source.DeliveryLongitude,
            Status=repost ? ShipmentStatuses.Published : ShipmentStatuses.Draft,CreatedAt=now,UpdatedAt=now };
        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        db.Shipments.Add(copy);
        var saved=await SaveAsync(copy,repost ? "reposted" : "copied",cancellationToken);
        if (saved.ErrorCode is not null) { return saved; }
        // Photos are copied inside the database so a large image never passes through the API; the order is kept.
        var photos=await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."ShipmentPhotos" ("ShipmentId","ContentType","SizeBytes","Content","CreatedAt")
            SELECT {copy.Id},"ContentType","SizeBytes","Content","CreatedAt" FROM public."ShipmentPhotos" WHERE "ShipmentId"={id}
            """,cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Map(copy,photos));
    }
    private Task<bool> HasTakersAsync(Guid shipmentId,CancellationToken ct)
    {
        return ShipmentRules.HasTakersAsync(db,shipmentId,ct);
    }
    private async Task<ShipmentActions> ActionsAsync(Shipment s,CancellationToken ct)
    {
        if (s.OwnerUserId!=actor.UserId)
        {
            return ShipmentActions.None;
        }

        if (s.Status is not (ShipmentStatuses.Draft or ShipmentStatuses.Published))
        {
            return new(false,false,false,s.Status.ToLowerInvariant(),s.Status==ShipmentStatuses.Cancelled,true);
        }

        var taken=s.Status==ShipmentStatuses.Published && await HasTakersAsync(s.Id,ct);
        return new(!taken,s.Status==ShipmentStatuses.Draft && s.PickupAt>clock.GetUtcNow(),true,taken ? HasTakersReason : null,false,true);
    }
    private async Task<ShipmentResult<ShipmentResponse>> SaveAsync(Shipment s,string action,CancellationToken ct)
    {
        s.Version++;s.UpdatedAt=clock.GetUtcNow();
        db.AuditEvents.Add(new AuditEvent { ActorUserId=actor.UserId,Action="shipment."+action,TargetType="Shipment",TargetId=s.Id,Outcome="Succeeded",OccurredAt=clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateConcurrencyException) { db.ChangeTracker.Clear();return ShipmentResult<ShipmentResponse>.Fail("conflict"); }
        return new(Map(s,await db.ShipmentPhotos.CountAsync(p=>p.ShipmentId==s.Id,ct)));
    }
}
