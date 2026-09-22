using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class ShipmentPhotoConfiguration : IEntityTypeConfiguration<ShipmentPhoto>
{
    public void Configure(EntityTypeBuilder<ShipmentPhoto> builder)
    {
        builder.ToTable("ShipmentPhotos", "public", table =>
        {
            table.HasCheckConstraint("CK_ShipmentPhotos_ContentType", "\"ContentType\" IN ('image/jpeg','image/png')");
            table.HasCheckConstraint("CK_ShipmentPhotos_SizeBytes", "\"SizeBytes\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ContentType).HasMaxLength(20);
        builder.Property(x => x.Content).HasColumnType("bytea");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // A shipment can only be deleted while still a draft, and its photos go with it.
        builder.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ShipmentId, x.CreatedAt });
    }
}
