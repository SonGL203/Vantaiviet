using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class BrokerRouteInterestConfiguration : IEntityTypeConfiguration<BrokerRouteInterest>
{
    public void Configure(EntityTypeBuilder<BrokerRouteInterest> builder)
    {
        builder.ToTable("BrokerRouteInterests", "public", table =>
        {
            table.HasCheckConstraint("CK_BrokerRouteInterests_FromArea", "length(btrim(\"FromArea\")) > 0");
            table.HasCheckConstraint("CK_BrokerRouteInterests_ToArea", "length(btrim(\"ToArea\")) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromArea).HasMaxLength(200);
        builder.Property(x => x.ToArea).HasMaxLength(200);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.BrokerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.BrokerUserId).HasDatabaseName("IX_BrokerRouteInterests_Broker");
    }
}
