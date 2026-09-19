namespace VantaiViet.CoreApi.Entities;

public sealed class KycIdentityDetail
{
    public Guid ApplicationId { get; set; }
    public required string FullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public required byte[] IdentityNumberEncrypted { get; set; }
    public required string EncryptionKeyId { get; set; }
    public required byte[] IdentityNumberHmac { get; set; }
    public required string HmacKeyId { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}
