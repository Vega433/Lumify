using Lumify.Application.DTOs;
using Lumify.Application.Models;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
    [ApiController]
    [Route("api/videos")]
    public class VideoController : BaseController
    {
        private readonly VideoApplicationService _service;

        public VideoController(VideoApplicationService service) => _service = service;

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(512 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(VideoDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<VideoDto>> UploadVideo(
            IFormFile video,
            IFormFile? preview,
            [FromForm] string title,
            [FromForm] string? description,
            CancellationToken ct)
        {
            if (UserId == Guid.Empty)
                return Unauthorized("Invalid access token claims");

            if (video == null || video.Length == 0)
                return BadRequest("file not selected");

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("title is required");

            UploadVideoRequest request = new UploadVideoRequest(
                title,
                description,
                UserId,
                video.OpenReadStream(),
                video.FileName,
                preview?.OpenReadStream(),
                preview?.FileName
                );

            try
            {
                VideoDto result = await _service.UploadAsync(request, ct);
                return Created($"/api/videos/{result.VideoId}", result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<VideoDto>>> GetAllVideo(CancellationToken ct)
        {
            List<VideoDto> videos = await _service.GetVideoListAsync(ct);
            return Ok(videos);
        }

        [HttpGet("{videoId:guid}")]
        public async Task<ActionResult<VideoDto>> GetVideoById(Guid videoId, CancellationToken ct)
        {
            VideoDto? video = await _service.GetVideoByIdAsync(videoId, ct);
            if (video == null)
                return NotFound();

            return Ok(video);
        }

        [HttpGet("author/{authorId:guid}")]
        public async Task<ActionResult<List<VideoDto>>> GetVideoListByAuthor(Guid authorId, CancellationToken ct)
        {
            List<VideoDto> videos = await _service.GetVideoListByAuthorAsync(authorId, ct);
            return Ok(videos);
        }

        [Authorize]
        [HttpDelete("{videoId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteVideoById(Guid videoId, CancellationToken ct)
        {
            if (UserId == Guid.Empty)
                return Unauthorized();

            VideoDeleteResult result = await _service.DeleteVideoById(videoId, UserId, ct);

            return result switch
            {
                VideoDeleteResult.NotFound => NotFound(),
                VideoDeleteResult.Forbidden => Forbid(),
                VideoDeleteResult.Deleted => NoContent(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
