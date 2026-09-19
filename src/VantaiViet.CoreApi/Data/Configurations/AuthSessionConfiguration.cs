using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("AuthSessions", "public", table =>
        {
            table.HasCheckConstraint("CK_AuthSessions_1", "octet_length(\"RefreshTokenHash\") = 32");
            table.HasCheckConstraint("CK_AuthSessions_2", "\"ExpiresAt\" > \"CreatedAt\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.FamilyId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.RevocationReason).HasMaxLength(100);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.RefreshTokenHash).HasDatabaseName("AuthSessions_RefreshTokenHash_key").IsUnique();
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_AuthSessions_User");
        builder.HasIndex(x => x.FamilyId).HasDatabaseName("IX_AuthSessions_Family");
    }
}
