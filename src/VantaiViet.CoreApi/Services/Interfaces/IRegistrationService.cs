using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Services.Interfaces;

public interface IRegistrationService
{
    Task<RegistrationCreatedResponse> CreateAsync(
        CreateRegistrationRequest request,
        CancellationToken cancellationToken);
}
