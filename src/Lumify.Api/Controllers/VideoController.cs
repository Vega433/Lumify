using Lumify.Application.DTOs;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class VideoController : BaseController
    {
        private readonly VideoApplicationService _service;

        public VideoController(VideoApplicationService service) => _service = service;

        [Authorize]
        [HttpPost("Upload")]
        [RequestSizeLimit(512 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<VideoDto>> UploadVideo(
            IFormFile video,
            IFormFile? preview,
            [FromForm] string title,
            [FromForm] string? description,
            CancellationToken ct)
        {
            if (video == null || video.Length == 0)
                return BadRequest("file not selected");

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
                return Ok(result);
            }
            catch (InvalidOperationException ex)
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
        public async Task<IActionResult> DeleteVideoById(Guid videoId, CancellationToken ct)
        {
            bool deleted = await _service.DeleteVideoById(videoId, UserId, ct);

            if (!deleted)
                return Forbid("Video not found or access denied");

            return NoContent();
        }
    }
}
