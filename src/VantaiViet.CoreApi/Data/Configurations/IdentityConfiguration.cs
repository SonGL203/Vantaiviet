using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class IdentityConfiguration :
    IEntityTypeConfiguration<User>,
    IEntityTypeConfiguration<IdentityRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "public", table =>
        {
            table.HasCheckConstraint("CK_Users_Status", "\"AccountStatus\" IN ('Active','PendingKyc','Suspended','Disabled')");
            table.HasCheckConstraint("CK_Users_Phone", "\"PhoneNumber\" ~ '^\\+[1-9][0-9]{7,14}$'");
            table.HasCheckConstraint("CK_Users_Name", "length(btrim(\"DisplayName\")) > 0");
            table.HasCheckConstraint("CK_Users_Password", "length(btrim(\"PasswordHash\")) > 0");
            table.HasCheckConstraint("CK_Users_Failures", "\"AccessFailedCount\" >= 0");
        });
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountStatus).HasMaxLength(20).HasDefaultValue("Active");
        builder.Property(x => x.UserName).IsRequired();
        builder.Property(x => x.NormalizedUserName).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PhoneNumberConfirmed).HasDefaultValue(true);
        builder.Property(x => x.EmailConfirmed).HasDefaultValue(false);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.SecurityStamp).IsRequired();
        builder.Property(x => x.ConcurrencyStamp).IsRequired();
        builder.Property(x => x.TwoFactorEnabled).HasDefaultValue(false);
        builder.Property(x => x.LockoutEnabled).HasDefaultValue(true);
        builder.Property(x => x.AccessFailedCount).HasDefaultValue(0);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => x.NormalizedUserName).IsUnique().HasDatabaseName("UX_Users_NormalizedUserName");
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("UX_Users_NormalizedEmail")
            .HasFilter("\"NormalizedEmail\" IS NOT NULL");
        builder.HasIndex(x => x.PhoneNumber).IsUnique().HasDatabaseName("Users_PhoneNumber_key");
        builder.HasIndex(x => x.RegistrationApplicationId).IsUnique().HasDatabaseName("Users_RegistrationApplicationId_key");
        builder.HasOne<RegistrationApplication>().WithMany().HasForeignKey(x => x.RegistrationApplicationId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("Roles", "public");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.NormalizedName).IsRequired();
        builder.Property(x => x.ConcurrencyStamp).IsRequired();
        builder.HasIndex(x => x.NormalizedName).HasDatabaseName("Roles_NormalizedName_key");
    }

    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("UserRoles", "public");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("UserClaims", "public");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("RoleClaims", "public");
        builder.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("UserLogins", "public");
        builder.Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Property(x => x.ProviderKey).HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("UserTokens", "public");
        builder.Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Property(x => x.Name).HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
