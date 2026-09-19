using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class TripRouteConfiguration : IEntityTypeConfiguration<TripRoute>
{
    public void Configure(EntityTypeBuilder<TripRoute> builder)
    {
        builder.ToTable("TripRoutes", "public");
        builder.HasKey(x => new { x.TripId, x.InputHash });
        builder.Property(x => x.InputHash).HasMaxLength(64);
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb");
        builder.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
    }
}
