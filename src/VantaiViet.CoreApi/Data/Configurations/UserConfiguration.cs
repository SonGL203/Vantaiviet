using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Self-referencing and optional: who invited this account in, feature 17.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReferredByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.ReferredByUserId).HasDatabaseName("IX_Users_ReferredByUserId");
    }
}
