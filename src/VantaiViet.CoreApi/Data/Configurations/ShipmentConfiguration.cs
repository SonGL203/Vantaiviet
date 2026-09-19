using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("Shipments", "public", t => {
            t.HasCheckConstraint("CK_Shipment_Status", "\"Status\" IN ('Draft','Published','Cancelled','Booked','Completed')");
            t.HasCheckConstraint("CK_Shipment_Weight", "\"WeightKg\" > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.PickupAddress).HasMaxLength(500);
        b.Property(x => x.DeliveryAddress).HasMaxLength(500);
        b.Property(x => x.ExternalCustomerName).HasMaxLength(150);
        b.Property(x => x.ExternalCustomerPhone).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.WeightKg).HasPrecision(12,2);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
