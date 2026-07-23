using Lumify.Application.DTOs;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        [HttpDelete("{userId:guid}")]
        public async Task<ActionResult> DeleteUserById(Guid userId, CancellationToken ct)
        {
            bool deleted = await _service.DeleteUserAsync(userId, UserId, ct);

            if (!deleted)
                return Forbid();

            return NoContent();
        }
    }
}
