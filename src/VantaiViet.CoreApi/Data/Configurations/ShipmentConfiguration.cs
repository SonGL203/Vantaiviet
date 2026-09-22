using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VantaiViet.CoreApi.Entities;
namespace VantaiViet.CoreApi.Data.Configurations;
public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("Shipments", "public", t => {
            t.HasCheckConstraint("CK_Shipment_Status", "\"Status\" IN ('Draft','Published','Cancelled','Booked','Completed')");
            t.HasCheckConstraint("CK_Shipment_Weight", "\"WeightKg\" > 0");
            t.HasCheckConstraint("CK_Shipment_CargoType",
                $"\"CargoType\" IN ({string.Join(",", CargoTypes.All.Select(x => $"'{x}'"))})");
            t.HasCheckConstraint("CK_Shipment_Quantity", $"\"Quantity\" BETWEEN 1 AND {ShipmentLimits.MaxQuantity}");
            t.HasCheckConstraint("CK_Shipment_QuantityUnit",
                $"\"QuantityUnit\" IN ({string.Join(",", QuantityUnits.All.Select(x => $"'{x}'"))})");
            t.HasCheckConstraint("CK_Shipment_VehicleType",
                $"\"VehicleType\" IN ({string.Join(",", RequiredVehicleTypes.All.Select(x => $"'{x}'"))})");
            t.HasCheckConstraint("CK_Shipment_ProposedPrice", "\"ProposedPrice\" IS NULL OR \"ProposedPrice\" > 0");
            t.HasCheckConstraint("CK_Shipment_BrokerCommissionRate",
                "\"BrokerCommissionRate\" IS NULL OR (\"BrokerCommissionRate\" >= 0 AND \"BrokerCommissionRate\" <= 100)");
            t.HasCheckConstraint("CK_Shipment_Currency", $"\"Currency\" = '{ShipmentLimits.DefaultCurrency}'");
            t.HasCheckConstraint("CK_Shipment_DeliverBy", "\"DeliverByAt\" IS NULL OR \"DeliverByAt\" > \"PickupAt\"");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(12).HasSentinel(string.Empty)
            .HasDefaultValueSql("'VT' || lpad(nextval('public.\"ShipmentCodes\"')::text, 7, '0')");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.PickupAddress).HasMaxLength(500);
        b.Property(x => x.DeliveryAddress).HasMaxLength(500);
        b.Property(x => x.ExternalCustomerName).HasMaxLength(150);
        b.Property(x => x.ExternalCustomerPhone).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.CargoType).HasMaxLength(20).HasDefaultValue(CargoTypes.General);
        b.Property(x => x.CargoDescription).HasMaxLength(1000);
        b.Property(x => x.Quantity).HasDefaultValue(1);
        b.Property(x => x.QuantityUnit).HasMaxLength(20).HasDefaultValue(QuantityUnits.Package);
        b.Property(x => x.VehicleType).HasMaxLength(20).HasDefaultValue(RequiredVehicleTypes.Any);
        b.Property(x => x.ProposedPrice).HasPrecision(ShipmentLimits.PricePrecision, 0);
        b.Property(x => x.BrokerCommissionRate).HasPrecision(5, 2);
        b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue(ShipmentLimits.DefaultCurrency);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.WeightKg).HasPrecision(12,2);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
