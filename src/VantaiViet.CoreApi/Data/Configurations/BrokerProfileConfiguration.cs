using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class BrokerProfileConfiguration : IEntityTypeConfiguration<BrokerProfile>
{
    public void Configure(EntityTypeBuilder<BrokerProfile> builder)
    {
        builder.ToTable("BrokerProfiles", "public", table =>
        {
            table.HasCheckConstraint("CK_BrokerProfiles_OperatingArea", "length(btrim(\"OperatingArea\")) > 0");
            table.HasCheckConstraint("CK_BrokerProfiles_YearsExperience", "\"YearsExperience\" >= 0");
            table.HasCheckConstraint("CK_BrokerProfiles_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.OperatingArea).HasMaxLength(200);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<User>().WithOne().HasForeignKey<BrokerProfile>(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
