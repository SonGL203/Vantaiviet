namespace VantaiViet.CoreApi.Entities;

public sealed class KycDocument
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public required string DocumentType { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? DeleteAfter { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
