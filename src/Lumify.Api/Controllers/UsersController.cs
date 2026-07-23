using Lumify.Application.DTOs;
using Lumify.Application.Models;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
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

        [HttpGet("{userId:guid}")]
        public async Task<ActionResult<UserDto>> GetUserById(Guid userId, CancellationToken ct)
        {
            UserDto? user = await _service.GetUserByIdAsync(userId, ct);
            if (user == null)
                return NotFound();

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
                return Unauthorized();

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
}
