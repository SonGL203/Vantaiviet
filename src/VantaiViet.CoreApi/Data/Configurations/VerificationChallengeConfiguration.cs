using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class VerificationChallengeConfiguration : IEntityTypeConfiguration<VerificationChallenge>
{
    public void Configure(EntityTypeBuilder<VerificationChallenge> builder)
    {
        builder.ToTable("VerificationChallenges", "public", table =>
        {
            table.HasCheckConstraint("CK_VerificationChallenges_1", "(\"RegistrationApplicationId\" IS NOT NULL) <> (\"UserId\" IS NOT NULL)");
            table.HasCheckConstraint("CK_VerificationChallenges_2", "(\"RegistrationApplicationId\" IS NOT NULL AND \"Purpose\" = 'RegistrationPhone') OR (\"UserId\" IS NOT NULL AND \"Purpose\" IN ('PasswordReset','VerifyEmail'))");
            table.HasCheckConstraint("CK_VerificationChallenges_3", "octet_length(\"CodeHmac\") = 32");
            table.HasCheckConstraint("CK_VerificationChallenges_4", "\"MaxAttempts\" > 0 AND \"AttemptCount\" BETWEEN 0 AND \"MaxAttempts\"");
            table.HasCheckConstraint("CK_VerificationChallenges_5", "\"ExpiresAt\" > \"CreatedAt\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Purpose).HasMaxLength(30);
        builder.Property(x => x.Destination).HasMaxLength(256);
        builder.Property(x => x.HmacKeyId).HasMaxLength(100);
        builder.Property(x => x.MaxAttempts).HasDefaultValue(5);
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<RegistrationApplication>().WithMany().HasForeignKey(x => x.RegistrationApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.RegistrationApplicationId, x.CreatedAt }).HasDatabaseName("IX_Challenges_Registration").IsDescending(false, true);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_Challenges_User").IsDescending(false, true);
    }
}
