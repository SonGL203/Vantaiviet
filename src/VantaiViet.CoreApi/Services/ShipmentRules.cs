using Microsoft.EntityFrameworkCore;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Services;

/// <summary>Rules that both the shipment and the shipment photo services must apply the same way.</summary>
internal static class ShipmentRules
{
    private const string PendingStatus = "Pending";
    private const string AcceptedStatus = "Accepted";

    public static async Task<bool> CanPostAsync(CoreDbContext db, Guid userId, bool external, CancellationToken cancellationToken)
    {
        var roles = external ? new[] { "BROKER" } : new[] { "SHIPPER", "BROKER" };
        return await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where u.Id == userId && (u.AccountStatus == "Active" || u.AccountStatus == "PendingKyc")
                && roles.Contains(r.NormalizedName!)
            select u.Id).AnyAsync(cancellationToken);
    }

    /// <summary>People waiting for the owner's answer on one shipment: drivers who asked, and a broker the job was offered to.</summary>
    public static async Task<int> PendingRequestsAsync(CoreDbContext db, Guid shipmentId, CancellationToken cancellationToken)
    {
        return await db.TransportRequests.CountAsync(x => x.ShipmentId == shipmentId && x.Status == PendingStatus, cancellationToken)
            + await db.Set<ShipmentBroker>().CountAsync(x => x.ShipmentId == shipmentId && x.Status == PendingStatus, cancellationToken);
    }

    /// <summary>A driver waiting for an answer, or a broker offered or holding the job, has agreed to the terms as they stand.</summary>
    public static async Task<bool> HasTakersAsync(CoreDbContext db, Guid shipmentId, CancellationToken cancellationToken)
    {
        return await db.TransportRequests.AnyAsync(x => x.ShipmentId == shipmentId && x.Status == PendingStatus, cancellationToken)
            || await db.Set<ShipmentBroker>().AnyAsync(x => x.ShipmentId == shipmentId && (x.Status == PendingStatus || x.Status == AcceptedStatus), cancellationToken);
    }
}
