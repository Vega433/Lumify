using Lumify.Application.DTOs;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseController
    {
        private readonly IdentityApplicationService _identity;

        public AuthController(IdentityApplicationService identity)
        {
            _identity = identity;
        }

        /// <summary>
        /// birthDate: YYYY-MM-DD (например 2000-01-15)
        /// </summary>
        [HttpPost("register")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserDto>> Register(
            [FromForm] string userName,
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] DateOnly birthDate,
            IFormFile? avatar,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return BadRequest("userName, email and password are required");
            }

            RegisterUserRequest request = new RegisterUserRequest(
                userName,
                email,
                password,
                birthDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                avatar?.OpenReadStream(),
                avatar?.FileName);

            try
            {
                UserDto user = await _identity.RegisterUserAsync(request, ct);
                return Created($"/api/users/{user.UserId}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] AuthUserRequest request, CancellationToken ct)
        {
            try
            {
                AuthResponse response = await _identity.AuthUserAsync(request, ct);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        {
            try
            {
                AuthResponse response = await _identity.RefreshAsync(request.RefreshToken, ct);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            if (UserId == Guid.Empty)
                return Unauthorized();

            await _identity.LogoutAsync(UserId, ct);
            return NoContent();
        }
    }
}
