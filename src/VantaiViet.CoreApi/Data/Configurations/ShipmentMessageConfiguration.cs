using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class ShipmentMessageConfiguration : IEntityTypeConfiguration<ShipmentMessage>
{
    public void Configure(EntityTypeBuilder<ShipmentMessage> builder)
    {
        builder.ToTable("ShipmentMessages", "public", table =>
            table.HasCheckConstraint("CK_ShipmentMessages_Body", $"char_length(\"Body\") BETWEEN 1 AND {ChatLimits.MaxBodyLength}"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Body).HasMaxLength(ChatLimits.MaxBodyLength);
        builder.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CarrierUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ShipmentId, x.CarrierUserId, x.Id });
    }
}
