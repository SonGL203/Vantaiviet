using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles", "public", table =>
        {
            table.HasCheckConstraint("CK_DriverProfiles_Status", "\"Status\" IN ('Submitted','Approved','Rejected')");
            table.HasCheckConstraint("CK_DriverProfiles_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.LicenseClass).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Submitted");
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
