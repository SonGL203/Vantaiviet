using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class KycIdentityDetailConfiguration : IEntityTypeConfiguration<KycIdentityDetail>
{
    public void Configure(EntityTypeBuilder<KycIdentityDetail> builder)
    {
        builder.ToTable("KycIdentityDetails", "public", table =>
        {
            table.HasCheckConstraint("CK_KycIdentityDetails_1", "length(btrim(\"FullName\")) > 0");
            table.HasCheckConstraint("CK_KycIdentityDetails_2", "octet_length(\"IdentityNumberEncrypted\") > 0");
            table.HasCheckConstraint("CK_KycIdentityDetails_3", "octet_length(\"IdentityNumberHmac\") = 32");
            table.HasCheckConstraint("CK_KycIdentityDetails_4", "\"IssuedOn\" IS NULL OR \"IssuedOn\" >= \"DateOfBirth\"");
            table.HasCheckConstraint("CK_KycIdentityDetails_5", "\"ExpiresOn\" IS NULL OR \"IssuedOn\" IS NULL OR \"ExpiresOn\" >= \"IssuedOn\"");
        });
        builder.HasKey(x => x.ApplicationId);
        builder.Property(x => x.FullName).HasMaxLength(150);
        builder.Property(x => x.EncryptionKeyId).HasMaxLength(100);
        builder.Property(x => x.HmacKeyId).HasMaxLength(100);
        builder.HasOne<KycApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.HmacKeyId, x.IdentityNumberHmac }).HasDatabaseName("IX_KycIdentity_IdentityNumber");
    }
}
