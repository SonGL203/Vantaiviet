using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class SavedDriverConfiguration : IEntityTypeConfiguration<SavedDriver>
{
    public void Configure(EntityTypeBuilder<SavedDriver> builder)
    {
        builder.ToTable("SavedDrivers", "public", table =>
        {
            table.HasCheckConstraint("CK_SavedDrivers_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.BrokerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<DriverGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
        // A broker saves a given driver once; editing the note, favourite flag or group updates that same row.
        builder.HasIndex(x => new { x.BrokerUserId, x.DriverUserId }).IsUnique().HasDatabaseName("IX_SavedDrivers_Broker_Driver");
        builder.HasIndex(x => x.GroupId).HasDatabaseName("IX_SavedDrivers_Group");
    }
}
