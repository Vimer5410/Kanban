using System.Security.Claims;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

public record CommentDto(int Id, int CardId, string Text, int AuthorId, string AuthorUsername, DateTime CreatedAt);
public record CreateCommentRequest(string Text);

[ApiController]
[Route("api/boards/{boardId}/columns/{columnId}/cards/{cardId}/comments")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CommentsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private async Task<BoardRole?> GetRole(int boardId, int userId)
    {
        var board = await _db.Boards.FindAsync(boardId);
        if (board == null)
            return null;

        if (board.OwnerId == userId)
            return BoardRole.Owner;

        var member = await _db.UserRoles.FirstOrDefaultAsync(x => x.BoardId == boardId && x.UserId == userId);
        if (member == null)
            return null;

        return member.Role;
    }

    private async Task<bool> CardBelongsHere(int boardId, int columnId, int cardId)
    {
        var columnOk = await _db.Columns.AnyAsync(c => c.Id == columnId && c.BoardId == boardId);
        if (!columnOk)
            return false;

        return await _db.Cards.AnyAsync(c => c.Id == cardId && c.ColumnId == columnId);
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(int boardId, int columnId, int cardId)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        if (!await CardBelongsHere(boardId, columnId, cardId))
            return NotFound();

        var comments = await _db.Comments.Where(c => c.CardId == cardId).OrderBy(c => c.CreatedAt).ToListAsync();

        var result = new List<CommentDto>();
        foreach (var c in comments)
        {
            var author = await _db.Users.FindAsync(c.AuthorId);
            result.Add(new CommentDto(c.Id, c.CardId, c.Text, c.AuthorId, author!.Username, c.CreatedAt));
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(int boardId, int columnId, int cardId, CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text обязателен.");

        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        if (!await CardBelongsHere(boardId, columnId, cardId))
            return NotFound();

        var comment = new Comment
        {
            CardId = cardId,
            AuthorId = userId,
            Text = request.Text
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var author = await _db.Users.FindAsync(userId);
        return Ok(new CommentDto(comment.Id, comment.CardId, comment.Text, comment.AuthorId, author!.Username, comment.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteComment(int boardId, int columnId, int cardId, int id)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        if (!await CardBelongsHere(boardId, columnId, cardId))
            return NotFound();

        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.CardId == cardId);
        if (comment == null)
            return NotFound();

        if (comment.AuthorId != userId && role != BoardRole.Owner)
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
