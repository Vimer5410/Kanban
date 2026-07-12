using System.Security.Claims;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

public record ColumnDto(int Id, int BoardId, string Title, int Order);
public record CreateColumnRequest(string Title);
public record UpdateColumnRequest(string Title, int Order);

[ApiController]
[Route("api/boards/{boardId}/columns")]
[Authorize]
public class ColumnsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ColumnsController(AppDbContext db)
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

    [HttpGet]
    public async Task<IActionResult> GetColumns(int boardId)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        var columns = await _db.Columns.Where(c => c.BoardId == boardId).OrderBy(c => c.Order).ToListAsync();
        var result = columns.Select(c => new ColumnDto(c.Id, c.BoardId, c.Title, c.Order)).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateColumn(int boardId, CreateColumnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var existing = await _db.Columns.Where(c => c.BoardId == boardId).ToListAsync();
        var maxOrder = existing.Count > 0 ? existing.Max(c => c.Order) : 0;

        var column = new BoardColumn
        {
            BoardId = boardId,
            Title = request.Title,
            Order = maxOrder + 1000
        };

        _db.Columns.Add(column);
        await _db.SaveChangesAsync();

        return Ok(new ColumnDto(column.Id, column.BoardId, column.Title, column.Order));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateColumn(int boardId, int id, UpdateColumnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column == null)
            return NotFound();

        column.Title = request.Title;
        column.Order = request.Order;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteColumn(int boardId, int id)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column == null)
            return NotFound();

        _db.Columns.Remove(column);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
