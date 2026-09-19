using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents", "public", table =>
        {
            table.HasCheckConstraint("CK_AuditEvents_1", "\"Outcome\" IN ('Succeeded','Failed','Denied')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.ActorReference).HasMaxLength(200);
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.TargetType).HasMaxLength(100);
        builder.Property(x => x.Outcome).HasMaxLength(20);
        builder.Property(x => x.TraceId).HasMaxLength(100);
        builder.Property(x => x.OccurredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt }).HasDatabaseName("IX_Audit_Target");
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt }).HasDatabaseName("IX_Audit_Actor");
    }
}
