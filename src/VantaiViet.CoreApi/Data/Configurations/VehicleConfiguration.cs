using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", "public", table =>
        {
            table.HasCheckConstraint("CK_Vehicles_Status", "\"Status\" IN ('Submitted','Approved','Rejected')");
            table.HasCheckConstraint("CK_Vehicles_Payload", "\"MaxPayloadKg\" > 0");
            table.HasCheckConstraint("CK_Vehicles_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
        builder.Property(x => x.VehicleType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MaxPayloadKg).HasPrecision(12, 2);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Submitted");
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => x.LicensePlate).IsUnique();
        builder.HasIndex(x => x.DriverUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
