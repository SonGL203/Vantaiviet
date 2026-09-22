using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class ShipperFollowUpConfiguration :
    IEntityTypeConfiguration<TripReview>, IEntityTypeConfiguration<ShipmentIncident>,
    IEntityTypeConfiguration<UserNotification>, IEntityTypeConfiguration<SavedAddress>
{
    public void Configure(EntityTypeBuilder<TripReview> builder)
    {
        builder.ToTable("TripReviews", "public", table =>
        {
            table.HasCheckConstraint("CK_TripReviews_Stars", "\"Stars\" BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_TripReviews_Comment", $"\"Comment\" IS NULL OR char_length(\"Comment\") <= {FollowUpLimits.MaxReviewComment}");
        });
        // One rating per carrier per trip: the database, not a lookup, is what stops a second one.
        builder.HasKey(x => new { x.TripId, x.TargetUserId });
        builder.Property(x => x.Comment).HasMaxLength(FollowUpLimits.MaxReviewComment);
        builder.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.TargetUserId, x.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<ShipmentIncident> builder)
    {
        builder.ToTable("ShipmentIncidents", "public", table =>
        {
            table.HasCheckConstraint("CK_ShipmentIncidents_Category",
                $"\"Category\" IN ({string.Join(",", IncidentCategories.All.Select(x => $"'{x}'"))})");
            table.HasCheckConstraint("CK_ShipmentIncidents_Status", "\"Status\" IN ('Open')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Category).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(FollowUpLimits.MaxIncidentLength);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IncidentStatuses.Open);
        builder.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ShipmentId, x.CreatedAt });
    }

    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications", "public");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Type).HasMaxLength(40);
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.ActorName).HasMaxLength(150);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // The list is read newest first per user, and the badge counts only what is unread.
        builder.HasIndex(x => new { x.UserId, x.Id });
        builder.HasIndex(x => new { x.UserId, x.ReadAt });
    }

    public void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        builder.ToTable("SavedAddresses", "public", table =>
        {
            table.HasCheckConstraint("CK_SavedAddresses_Point",
                "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Label).HasMaxLength(FollowUpLimits.MaxAddressLabel);
        builder.Property(x => x.Address).HasMaxLength(FollowUpLimits.MaxAddressLength);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        // The service also refuses a label that differs only in letter case; this index is the backstop for the exact same one.
        builder.HasIndex(x => new { x.OwnerUserId, x.Label }).IsUnique();
    }
}
