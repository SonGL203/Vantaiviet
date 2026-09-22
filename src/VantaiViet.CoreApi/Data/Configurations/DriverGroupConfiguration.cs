using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class DriverGroupConfiguration : IEntityTypeConfiguration<DriverGroup>
{
    public void Configure(EntityTypeBuilder<DriverGroup> builder)
    {
        builder.ToTable("DriverGroups", "public", table =>
        {
            table.HasCheckConstraint("CK_DriverGroups_Name", "length(btrim(\"Name\")) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.BrokerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.BrokerUserId).HasDatabaseName("IX_DriverGroups_Broker");
    }
}
