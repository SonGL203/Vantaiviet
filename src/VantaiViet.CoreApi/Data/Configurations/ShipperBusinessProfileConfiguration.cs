using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class ShipperBusinessProfileConfiguration : IEntityTypeConfiguration<ShipperBusinessProfile>
{
    public void Configure(EntityTypeBuilder<ShipperBusinessProfile> builder)
    {
        builder.ToTable("ShipperBusinessProfiles", "public", table =>
        {
            table.HasCheckConstraint("CK_ShipperBusinessProfiles_TaxCode", "\"TaxCode\" ~ '^[0-9]{10}(-[0-9]{3})?$'");
            table.HasCheckConstraint("CK_ShipperBusinessProfiles_Text",
                "length(btrim(\"CompanyName\")) > 0 AND length(btrim(\"Address\")) > 0");
            table.HasCheckConstraint("CK_ShipperBusinessProfiles_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.TaxCode).HasMaxLength(14);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<User>().WithOne().HasForeignKey<ShipperBusinessProfile>(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        // Not unique: several staff of one company may register, and ownership of a tax code is proven by KYC review.
        builder.HasIndex(x => x.TaxCode).HasDatabaseName("IX_ShipperBusinessProfiles_TaxCode");
    }
}
