using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class BookingConfiguration : IEntityTypeConfiguration<TransportRequest>, IEntityTypeConfiguration<Booking>, IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<TransportRequest> b)
    {
        b.ToTable("TransportRequests","public", t => t.HasCheckConstraint("CK_TransportRequests_Status", "\"Status\" IN ('Pending','Accepted','Rejected','Withdrawn')"));
        b.HasKey(x=>x.Id);
        b.Property(x=>x.Status).HasMaxLength(20);
        b.HasIndex(x=>new {x.ShipmentId,x.DriverUserId}).IsUnique();
        b.HasOne<Shipment>().WithMany().HasForeignKey(x=>x.ShipmentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("Bookings","public",t=>t.HasCheckConstraint("CK_Bookings_Status","\"Status\" IN ('Confirmed','Completed','Cancelled')"));
        b.Property(x=>x.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
        b.HasKey(x=>x.Id);
        b.HasIndex(x=>x.ShipmentId).IsUnique();
        b.HasIndex(x=>x.RequestId).IsUnique();
        b.HasOne<Shipment>().WithMany().HasForeignKey(x=>x.ShipmentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TransportRequest>().WithMany().HasForeignKey(x=>x.RequestId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<Trip> b)
    {
        b.ToTable("Trips","public",t=>t.HasCheckConstraint("CK_Trips_Status","\"Status\" IN ('Assigned','InProgress','Loaded','Delivered','Completed','Cancelled')"));
        b.Property(x=>x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        b.HasKey(x=>x.Id);
        b.Property(x=>x.Status).HasMaxLength(20);
        b.HasIndex(x=>x.BookingId).IsUnique();
        b.HasIndex(x=>x.DriverUserId).IsUnique().HasFilter("\"Status\" IN ('Assigned','InProgress','Loaded','Delivered')");
        b.HasIndex(x=>x.VehicleId).IsUnique().HasFilter("\"Status\" IN ('Assigned','InProgress','Loaded','Delivered')");
        b.HasOne<Booking>().WithMany().HasForeignKey(x=>x.BookingId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x=>x.VehicleId).OnDelete(DeleteBehavior.NoAction);
    }
}
