using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class KycDocumentConfiguration : IEntityTypeConfiguration<KycDocument>
{
    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.ToTable("KycDocuments", "public", table =>
        {
            table.HasCheckConstraint("CK_KycDocuments_1", "\"DocumentType\" IN ('IdentityFront','IdentityBack','Selfie')");
            table.HasCheckConstraint("CK_KycDocuments_2", "length(btrim(\"StorageKey\")) > 0");
            table.HasCheckConstraint("CK_KycDocuments_3", "\"SizeBytes\" > 0");
            table.HasCheckConstraint("CK_KycDocuments_4", "\"DeleteAfter\" IS NULL OR \"DeleteAfter\" > \"UploadedAt\"");
            table.HasCheckConstraint("CK_KycDocuments_5", "\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"UploadedAt\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocumentType).HasMaxLength(20);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<KycApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ApplicationId, x.DocumentType }).HasDatabaseName("KycDocuments_ApplicationId_DocumentType_key").IsUnique();
        builder.HasIndex(x => x.StorageKey).HasDatabaseName("KycDocuments_StorageKey_key").IsUnique();
    }
}
