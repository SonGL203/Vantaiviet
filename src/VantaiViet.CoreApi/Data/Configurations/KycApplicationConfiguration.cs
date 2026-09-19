using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class KycApplicationConfiguration : IEntityTypeConfiguration<KycApplication>
{
    public void Configure(EntityTypeBuilder<KycApplication> builder)
    {
        builder.ToTable("KycApplications", "public", table =>
        {
            table.HasCheckConstraint("CK_KycApplications_1", "\"Status\" IN ('Draft','Submitted','UnderReview','Approved','Rejected')");
            table.HasCheckConstraint("CK_KycApplications_2", "\"Status\" = 'Draft' OR \"SubmittedAt\" IS NOT NULL");
            table.HasCheckConstraint("CK_KycApplications_3", "\"Status\" NOT IN ('Approved','Rejected') OR (\"ReviewedAt\" IS NOT NULL AND \"ReviewerReference\" IS NOT NULL AND length(btrim(\"ReviewerReference\")) > 0)");
            table.HasCheckConstraint("CK_KycApplications_4", "\"Status\" <> 'Rejected' OR (\"RejectionCode\" IS NOT NULL AND length(btrim(\"RejectionCode\")) > 0)");
            table.HasCheckConstraint("CK_KycApplications_5", "\"ReviewedAt\" IS NULL OR (\"SubmittedAt\" IS NOT NULL AND \"ReviewedAt\" >= \"SubmittedAt\")");
            table.HasCheckConstraint("CK_KycApplications_6", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Draft");
        builder.Property(x => x.ReviewerReference).HasMaxLength(200);
        builder.Property(x => x.RejectionCode).HasMaxLength(100);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<RegistrationApplication>().WithMany().HasForeignKey(x => x.RegistrationApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.RegistrationApplicationId).HasDatabaseName("UX_Kyc_ActiveApplication").IsUnique().HasFilter("\"Status\" IN ('Draft','Submitted','UnderReview','Approved')");
        builder.HasIndex(x => new { x.Status, x.SubmittedAt, x.Id }).HasDatabaseName("IX_Kyc_ReviewQueue");
        builder.HasIndex(x => x.ReviewerUserId).HasDatabaseName("IX_Kyc_Reviewer");
    }
}
