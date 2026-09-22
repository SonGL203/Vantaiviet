namespace VantaiViet.CoreApi.Entities;

public static class KycStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    // Reported to clients when the account has no application yet; never stored.
    public const string None = "None";
}

public static class KycSubjects
{
    public const string Individual = "Individual";
    public const string Business = "Business";
}

public static class KycDocumentTypes
{
    public const string IdentityFront = "IdentityFront";
    public const string IdentityBack = "IdentityBack";
    public const string Selfie = "Selfie";
    public const string BusinessLicense = "BusinessLicense";

    public static readonly IReadOnlyList<string> All = [IdentityFront, IdentityBack, Selfie, BusinessLicense];

    public static IReadOnlyList<string> RequiredFor(string subjectType)
    {
        return subjectType == KycSubjects.Business ? All : [IdentityFront, IdentityBack, Selfie];
    }
}
