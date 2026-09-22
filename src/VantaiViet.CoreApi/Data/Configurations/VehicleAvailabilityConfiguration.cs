using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class VehicleAvailabilityConfiguration : IEntityTypeConfiguration<VehicleAvailability>
{
    public void Configure(EntityTypeBuilder<VehicleAvailability> builder)
    {
        builder.ToTable("VehicleAvailabilities", "public", table =>
        {
            table.HasCheckConstraint("CK_VehicleAvailabilities_Status", "\"Status\" IN ('Open','Withdrawn','Matched')");
            table.HasCheckConstraint("CK_VehicleAvailabilities_Text",
                "length(btrim(\"FromAddress\")) > 0 AND length(btrim(\"ToAddress\")) > 0");
            table.HasCheckConstraint("CK_VehicleAvailabilities_Expiry", "\"ExpiresAt\" > \"AvailableFrom\"");
            table.HasCheckConstraint("CK_VehicleAvailabilities_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromAddress).HasMaxLength(500);
        builder.Property(x => x.ToAddress).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.NoAction);
        // The search reads open, unexpired postings; this is exactly that filter.
        builder.HasIndex(x => new { x.Status, x.ExpiresAt }).HasDatabaseName("IX_VehicleAvailabilities_Search");
        builder.HasIndex(x => x.DriverUserId).HasDatabaseName("IX_VehicleAvailabilities_Driver");
    }
}
