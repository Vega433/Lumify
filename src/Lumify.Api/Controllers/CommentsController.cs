using Lumify.Application.DTOs;
using Lumify.Application.Models;
using Lumify.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumify.Api.Controllers;

[ApiController]
[Route("api")]
public class CommentsController : BaseController
{
    private readonly CommentApplicationService _service;

    public CommentsController(CommentApplicationService service)
    {
        _service = service;
    }

    [HttpGet("videos/{videoId:guid}/comments")]
    [ProducesResponseType(typeof(List<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CommentDto>>> GetByVideo(Guid videoId, CancellationToken ct)
    {
        List<CommentDto>? comments = await _service.GetByVideoAsync(videoId, ct);
        if (comments == null)
        {
            return NotFound();
        }

        return Ok(comments);
    }

    [Authorize]
    [HttpPost("videos/{videoId:guid}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Create(
        Guid videoId,
        [FromBody] CreateCommentRequest request,
        CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("text is required");
        }

        try
        {
            CommentDto comment = await _service.CreateAsync(videoId, UserId, request, ct);
            return Created($"/api/videos/{videoId}/comments", comment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPut("comments/{commentId:guid}")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Update(
        Guid commentId,
        [FromBody] UpdateCommentRequest request,
        CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("text is required");
        }

        try
        {
            (CommentAccess access, CommentDto? comment) = await _service.UpdateAsync(
                commentId,
                UserId,
                request.Text,
                ct);

            return access switch
            {
                CommentAccess.NotFound => NotFound(),
                CommentAccess.Forbidden => Forbid(),
                CommentAccess.Ok => Ok(comment),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpDelete("comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid commentId, CancellationToken ct)
    {
        if (UserId == Guid.Empty)
        {
            return Unauthorized();
        }

        CommentAccess access = await _service.DeleteAsync(commentId, UserId, ct);
        return access switch
        {
            CommentAccess.NotFound => NotFound(),
            CommentAccess.Forbidden => Forbid(),
            CommentAccess.Ok => NoContent(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
