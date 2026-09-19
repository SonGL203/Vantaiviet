using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class RegistrationSessionConfiguration : IEntityTypeConfiguration<RegistrationSession>
{
    public void Configure(EntityTypeBuilder<RegistrationSession> builder)
    {
        builder.ToTable("RegistrationSessions", "public", table =>
        {
            table.HasCheckConstraint("CK_RegistrationSessions_1", "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint("CK_RegistrationSessions_2", "\"ExpiresAt\" > \"CreatedAt\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<RegistrationApplication>().WithMany().HasForeignKey(x => x.RegistrationApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TokenHash).HasDatabaseName("RegistrationSessions_TokenHash_key").IsUnique();
        builder.HasIndex(x => x.RegistrationApplicationId).HasDatabaseName("IX_RegistrationSessions_Application");
    }
}
