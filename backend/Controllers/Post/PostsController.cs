using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SocialApp.DTOs;
using SocialApp.Services.Post;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SocialApp.Controllers.Post;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        IPostService postService,
        ILogger<PostsController> logger)
    {
        _postService = postService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PostResponseDTO>> CreatePost([FromBody] CreatePostDTO postDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var post = await _postService.CreatePostAsync(currentUserId, postDto);

            if (post == null)
            {
                return BadRequest(new { message = "Failed to create post" });
            }

            return Ok(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating post");
            return StatusCode(500, new { message = "An error occurred while creating the post" });
        }
    }

    [HttpGet("{postId}")]
    public async Task<ActionResult<PostResponseDTO>> GetPost(int postId)
    {
        try
        {
            int? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            }

            var post = await _postService.GetPostByIdAsync(postId, currentUserId);

            if (post == null)
            {
                return NotFound(new { message = "Post not found" });
            }

            return Ok(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving post {PostId}", postId);
            return StatusCode(500, new { message = "An error occurred while retrieving the post" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PostPagedResponseDTO>> GetPosts([FromQuery] PostFilterDTO filter)
    {
        try
        {
            int? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            }

            var posts = await _postService.GetPostsAsync(filter, currentUserId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving posts");
            return StatusCode(500, new { message = "An error occurred while retrieving posts" });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<PostPagedResponseDTO>> GetUserPosts(
        int userId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            int? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            }

            var posts = await _postService.GetPostsByUserAsync(userId, pageNumber, pageSize, currentUserId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving posts for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user posts" });
        }
    }

    [HttpPut("{postId}")]
    [Authorize]
    public async Task<ActionResult<PostResponseDTO>> UpdatePost(int postId, [FromBody] UpdatePostDTO postDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var post = await _postService.UpdatePostAsync(currentUserId, postId, postDto);

            if (post == null)
            {
                return NotFound(new { message = "Post not found or you don't have permission to update it" });
            }

            return Ok(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating post {PostId}", postId);
            return StatusCode(500, new { message = "An error occurred while updating the post" });
        }
    }

    [HttpDelete("{postId}")]
    [Authorize]
    public async Task<IActionResult> DeletePost(int postId)
    {
        try
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            bool isAdmin = User.IsInRole("Admin");

            // If user is an admin, allow them to delete any post
            if (isAdmin)
            {
                var post = await _postService.GetPostByIdAsync(postId);
                if (post == null)
                {
                    return NotFound(new { message = "Post not found" });
                }

                bool result = await _postService.DeletePostAsync(post.UserId, postId);
                if (result)
                {
                    return Ok(new { message = "Post deleted successfully by admin" });
                }
                else
                {
                    return BadRequest(new { message = "Failed to delete post" });
                }
            }
            else
            {
                // Regular users can only delete their own posts
                bool result = await _postService.DeletePostAsync(currentUserId, postId);
                if (!result)
                {
                    return NotFound(new { message = "Post not found or you don't have permission to delete it" });
                }

                return Ok(new { message = "Post deleted successfully" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting post {PostId}", postId);
            return StatusCode(500, new { message = "An error occurred while deleting the post" });
        }
    }

    [HttpPost("upload-multiple-media")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMultipleMedia([FromForm] MultipleMediaUploadDTO uploadDto)
    {
        try
        {
            if (uploadDto == null || uploadDto.MediaFiles == null || !uploadDto.MediaFiles.Any())
            {
                return BadRequest(new { message = "No media files provided" });
            }

            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var result = await _postService.UploadMultipleMediaAsync(currentUserId, uploadDto.MediaFiles, uploadDto.MediaTypes);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                results = result.Results.Select(r => new
                {
                    mediaUrl = r.MediaUrl,
                    publicId = r.PublicId,
                    width = r.Width,
                    height = r.Height,
                    format = r.Format,
                    duration = r.Duration,
                    fileSize = r.FileSize,
                    resourceType = r.ResourceType,
                    mediaType = r.MediaType,
                    mediaFilename = r.MediaFilename
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading multiple media files");
            return StatusCode(500, new { message = "An error occurred while uploading media files" });
        }
    }
}
