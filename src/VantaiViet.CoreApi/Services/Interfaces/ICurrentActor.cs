namespace VantaiViet.CoreApi.Services.Interfaces;
public interface ICurrentActor
{
    Guid UserId { get; }
    Guid SessionId => Guid.Empty;
}
