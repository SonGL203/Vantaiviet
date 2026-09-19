using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class TripEvidenceConfiguration : IEntityTypeConfiguration<TripEvent>, IEntityTypeConfiguration<TripProof>
{
    public void Configure(EntityTypeBuilder<TripEvent> b)
    {
        b.ToTable("TripEvents", "public");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.TripId, x.OccurredAt });
        b.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TripProof>().WithMany().HasForeignKey(x => x.ProofId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<TripProof> b)
    {
        b.ToTable("TripProofs", "public", t =>
        {
            t.HasCheckConstraint("CK_TripProof_Size", "octet_length(\"Content\") BETWEEN 8 AND 5242880");
            t.HasCheckConstraint("CK_TripProof_Type", "\"ContentType\" IN ('image/jpeg','image/png')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.ContentType).HasMaxLength(30);
        b.HasIndex(x => x.TripId);
        b.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploaderUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
