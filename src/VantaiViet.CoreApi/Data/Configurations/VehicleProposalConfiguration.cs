using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;

namespace VantaiViet.CoreApi.Data.Configurations;

public sealed class VehicleProposalConfiguration : IEntityTypeConfiguration<VehicleProposal>
{
    public void Configure(EntityTypeBuilder<VehicleProposal> builder)
    {
        builder.ToTable("VehicleProposals", "public", table =>
        {
            table.HasCheckConstraint("CK_VehicleProposals_Status", "\"Status\" IN ('Pending','CounterOffered','Accepted','Rejected','Withdrawn')");
            table.HasCheckConstraint("CK_VehicleProposals_Price", "\"ProposedPrice\" > 0 AND (\"CounterPrice\" IS NULL OR \"CounterPrice\" > 0)");
            table.HasCheckConstraint("CK_VehicleProposals_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProposedPrice).HasPrecision(ShipmentLimits.PricePrecision, 0);
        builder.Property(x => x.CounterPrice).HasPrecision(ShipmentLimits.PricePrecision, 0);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<VehicleAvailability>().WithMany().HasForeignKey(x => x.VehicleAvailabilityId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.BrokerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DriverUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.NoAction);
        // Only one live (non-final) proposal per posting at a time: a broker withdraws or the driver decides before another is sent.
        builder.HasIndex(x => x.VehicleAvailabilityId).HasDatabaseName("IX_VehicleProposals_Availability");
        builder.HasIndex(x => new { x.DriverUserId, x.Status }).HasDatabaseName("IX_VehicleProposals_Driver");
        builder.HasIndex(x => new { x.BrokerUserId, x.Status }).HasDatabaseName("IX_VehicleProposals_Broker");
    }
}
