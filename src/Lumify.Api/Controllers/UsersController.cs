using Lumify.Application.DTOs;
using Lumify.Application.Models;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : BaseController
{
    private readonly UserApplicationService _service;

    public UsersController(UserApplicationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(CancellationToken ct)
    {
        List<UserDto> users = await _service.GetUsersAsync(ct);
        return Ok(users);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        UserProfileDto? user = await _service.GetCurrentUserAsync(UserId, ct);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(user);
    }

    [Authorize]
    [HttpPatch("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            UserProfileDto profile = await _service.UpdateProfileAsync(UserId, request, ct);
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Incorrect password" || ex.Message == "User not found")
        {
            return ex.Message == "User not found" ? Unauthorized() : BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [Authorize]
    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> ChangeAvatar(IFormFile avatar, CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (avatar == null || avatar.Length == 0)
        {
            return BadRequest("file not selected");
        }

        try
        {
            UserProfileDto profile = await _service.ChangeAvatarAsync(
                UserId,
                avatar.OpenReadStream(),
                avatar.FileName,
                ct);
            return Ok(profile);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }

    [Authorize]
    [HttpDelete("me/avatar")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> RemoveAvatar(CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        UserProfileDto? profile = await _service.RemoveAvatarAsync(UserId, ct);
        if (profile == null)
        {
            return Unauthorized();
        }

        return Ok(profile);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid userId, CancellationToken ct)
    {
        UserDto? user = await _service.GetUserByIdAsync(userId, ct);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [Authorize]
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUserById(Guid userId, CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        UserDeleteResult result = await _service.DeleteUserAsync(userId, UserId, ct);

        return result switch
        {
            UserDeleteResult.NotFound => NotFound(),
            UserDeleteResult.Forbidden => Forbid(),
            UserDeleteResult.Deleted => NoContent(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
