namespace VantaiViet.CoreApi.Entities;

public static class ShipmentStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Booked = "Booked";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

/// <summary>How the shipper counts the goods; stored beside the weight so a driver can picture the load.</summary>
public static class QuantityUnits
{
    public const string Piece = "Piece";
    public const string Package = "Package";
    public const string Box = "Box";
    public const string Pallet = "Pallet";
    public const string Bag = "Bag";
    public const string Roll = "Roll";
    public const string Drum = "Drum";

    public static readonly IReadOnlyList<string> All = [Piece, Package, Box, Pallet, Bag, Roll, Drum];
}

/// <summary>
/// The kind of vehicle the shipper asks for. Drivers still describe their own vehicles in free text,
/// so this is shown to them but is not (yet) a hard eligibility filter.
/// </summary>
public static class RequiredVehicleTypes
{
    public const string Any = "Any";
    public const string Van = "Van";
    public const string SmallTruck = "SmallTruck";
    public const string MediumTruck = "MediumTruck";
    public const string HeavyTruck = "HeavyTruck";
    public const string Refrigerated = "Refrigerated";
    public const string Tractor = "Tractor";
    public const string Container = "Container";
    public const string Tanker = "Tanker";

    public static readonly IReadOnlyList<string> All =
        [Any, Van, SmallTruck, MediumTruck, HeavyTruck, Refrigerated, Tractor, Container, Tanker];
}

public static class ShipmentLimits
{
    public const int MaxQuantity = 1_000_000;
    public const int MaxPhotos = 6;
    public const string DefaultCurrency = "VND";

    // VND has no minor unit, so a price is a whole number of dong; 11 digits is just under 100 billion.
    public const int MaxPriceDigits = 11;
    public const string PricePattern = "^[1-9][0-9]{0,10}$";
    public const int PricePrecision = 15;
}

public static class ChatLimits
{
    public const int MaxBodyLength = 1000;
    public const int PageSize = 100;
}
