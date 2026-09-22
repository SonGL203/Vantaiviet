namespace VantaiViet.CoreApi.Entities;

public static class CargoTypes
{
    public const string General = "General";
    public const string Food = "Food";
    public const string Refrigerated = "Refrigerated";
    public const string Agricultural = "Agricultural";
    public const string Construction = "Construction";
    public const string Machinery = "Machinery";
    public const string Furniture = "Furniture";
    public const string Electronics = "Electronics";
    public const string Textile = "Textile";
    public const string Chemical = "Chemical";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All =
    [
        General, Food, Refrigerated, Agricultural, Construction, Machinery, Furniture, Electronics, Textile, Chemical, Other
    ];
}
