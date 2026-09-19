using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class RegistrationApplicationConfiguration : IEntityTypeConfiguration<RegistrationApplication>
{
    public void Configure(EntityTypeBuilder<RegistrationApplication> builder)
    {
        builder.ToTable("RegistrationApplications", "public", table =>
        {
            table.HasCheckConstraint("CK_RegistrationApplications_1", "\"PhoneNumber\" ~ '^\\+[1-9][0-9]{7,14}$'");
            table.HasCheckConstraint("CK_RegistrationApplications_2", "\"Status\" IN ('InProgress','Completed','Expired','Cancelled')");
            table.HasCheckConstraint("CK_RegistrationApplications_3", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_RegistrationApplications_4", "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.DisplayName).HasMaxLength(150);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("InProgress");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => x.PhoneNumber).HasDatabaseName("UX_Registration_ActivePhone").IsUnique().HasFilter("\"Status\" = 'InProgress'");
    }
}
