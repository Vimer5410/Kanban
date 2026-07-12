using Kanban.Api.Data;
using Kanban.Api.Dtos;
using Kanban.Api.Extensions;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api/boards/{boardId}/columns")]
[Authorize]
public class ColumnsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardAccessService _access;

    public ColumnsController(AppDbContext db, BoardAccessService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<ColumnDto>>> GetColumns(int boardId)
    {
        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(boardId, userId);
        if (role is null)
            return NotFound();

        var columns = await _db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Order)
            .Select(c => new ColumnDto(c.Id, c.BoardId, c.Title, c.Order))
            .ToListAsync();

        return Ok(columns);
    }

    [HttpPost]
    public async Task<ActionResult<ColumnDto>> CreateColumn(int boardId, CreateColumnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(boardId, userId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var maxOrder = await _db.Columns
            .Where(c => c.BoardId == boardId)
            .Select(c => (int?)c.Order)
            .MaxAsync() ?? 0;

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

        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(boardId, userId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column is null)
            return NotFound();

        column.Title = request.Title;
        column.Order = request.Order;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteColumn(int boardId, int id)
    {
        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(boardId, userId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column is null)
            return NotFound();

        _db.Columns.Remove(column);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
