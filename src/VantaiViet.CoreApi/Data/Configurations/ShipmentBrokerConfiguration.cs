using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class ShipmentBrokerConfiguration : IEntityTypeConfiguration<ShipmentBroker>
{
    public void Configure(EntityTypeBuilder<ShipmentBroker> builder)
    {
        builder.ToTable("ShipmentBrokers", "public", table => table.HasCheckConstraint("CK_ShipmentBrokers_Status",
            "\"Status\" IN ('Pending','Accepted','Rejected','Revoked')"));
        builder.HasKey(x => x.ShipmentId);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasOne<Shipment>().WithOne().HasForeignKey<ShipmentBroker>(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.BrokerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.BrokerUserId, x.Status });
    }
}
