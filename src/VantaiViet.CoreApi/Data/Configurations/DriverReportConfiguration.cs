using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class DriverReportConfiguration : IEntityTypeConfiguration<DriverReport>
{
    public void Configure(EntityTypeBuilder<DriverReport> b)
    {
        b.ToTable("DriverReports", "public", t => t.HasCheckConstraint("CK_DriverReport_Category",
            $"\"Category\" IN ({string.Join(",", DriverReportCategories.All.Select(x => $"'{x}'"))})"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.DriverUserId);
        b.HasIndex(x => new { x.ReporterUserId, x.CreatedAt });
    }
}
