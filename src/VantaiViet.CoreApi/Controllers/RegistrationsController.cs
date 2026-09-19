using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController]
[Route("api/registrations")]
public sealed class RegistrationsController(
    IRegistrationService registrationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ApiResponse<RegistrationCreatedResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<RegistrationCreatedResponse>>> CreateAsync(
        CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await registrationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(CreateAsync),
            new { response.RegistrationApplicationId },
            ApiResponse<RegistrationCreatedResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

}
