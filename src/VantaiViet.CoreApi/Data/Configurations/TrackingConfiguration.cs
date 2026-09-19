using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class TrackingConfiguration : IEntityTypeConfiguration<TripLocation>, IEntityTypeConfiguration<TripParticipant>
{
    public void Configure(EntityTypeBuilder<TripLocation> b)
    {
        b.ToTable("TripLocations","public",t =>
        {
            t.HasCheckConstraint("CK_TripLocation_Coordinates","\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("CK_TripLocation_Accuracy","\"AccuracyMeters\" BETWEEN 0 AND 1000");
        });
        b.HasKey(x=>x.Id);
        b.HasIndex(x=>new {x.TripId,x.PointId}).IsUnique();
        b.HasIndex(x=>new {x.TripId,x.RecordedAt,x.Id});
        b.HasIndex(x=>new {x.TripId,x.Id});
        b.HasOne<Trip>().WithMany().HasForeignKey(x=>x.TripId).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<TripParticipant> b)
    {
        b.ToTable("TripParticipants","public");
        b.HasKey(x=>new {x.TripId,x.UserId});
        b.HasOne<Trip>().WithMany().HasForeignKey(x=>x.TripId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.GrantedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
