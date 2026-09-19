using System.IdentityModel.Tokens.Jwt;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Hosting;
internal sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid SessionId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sid")?.Value,out var id)
        ? id : throw new UnauthorizedAccessException();
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw new UnauthorizedAccessException();
}
