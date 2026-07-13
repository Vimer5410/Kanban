using System.Security.Claims;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

public record CardDto(int Id, int ColumnId, string Title, string? Description, DateTime? Deadline, int Order, int? AssigneeId, string? AssigneeUsername, DateTime CreatedAt);
public record CreateCardRequest(string Title, string? Description, DateTime? Deadline, int? AssigneeId);
public record UpdateCardRequest(string Title, string? Description, DateTime? Deadline, int? AssigneeId);
public record MoveCardRequest(int ColumnId, int Order);

[ApiController]
[Route("api/boards/{boardId}/columns/{columnId}/cards")]
[Authorize]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CardsController(AppDbContext db)
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

    private async Task<CardDto> ToDto(Card c)
    {
        string? assigneeUsername = null;
        if (c.AssigneeId != null)
        {
            var assignee = await _db.Users.FindAsync(c.AssigneeId);
            assigneeUsername = assignee?.Username;
        }

        return new CardDto(c.Id, c.ColumnId, c.Title, c.Description, c.Deadline, c.Order, c.AssigneeId, assigneeUsername, c.CreatedAt);
    }

    [HttpGet]
    public async Task<IActionResult> GetCards(int boardId, int columnId)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
        if (column == null)
            return NotFound();

        var cards = await _db.Cards.Where(c => c.ColumnId == columnId).OrderBy(c => c.Order).ToListAsync();

        var result = new List<CardDto>();
        foreach (var c in cards)
        {
            result.Add(await ToDto(c));
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCard(int boardId, int columnId, int id)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id && c.ColumnId == columnId);
        if (card == null)
            return NotFound();

        return Ok(await ToDto(card));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCard(int boardId, int columnId, CreateCardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var column = await _db.Columns.FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId);
        if (column == null)
            return NotFound();

        if (request.AssigneeId != null)
        {
            var assigneeExists = await _db.Users.AnyAsync(u => u.Id == request.AssigneeId);
            if (!assigneeExists)
                return BadRequest("Такого юзера нет.");
        }

        var existing = await _db.Cards.Where(c => c.ColumnId == columnId).ToListAsync();
        var maxOrder = existing.Count > 0 ? existing.Max(c => c.Order) : 0;

        var card = new Card
        {
            ColumnId = columnId,
            Title = request.Title,
            Description = request.Description,
            Deadline = request.Deadline,
            AssigneeId = request.AssigneeId,
            Order = maxOrder + 1000
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        return Ok(await ToDto(card));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCard(int boardId, int columnId, int id, UpdateCardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id && c.ColumnId == columnId);
        if (card == null)
            return NotFound();

        if (request.AssigneeId != null)
        {
            var assigneeExists = await _db.Users.AnyAsync(u => u.Id == request.AssigneeId);
            if (!assigneeExists)
                return BadRequest("Такого юзера нет.");
        }

        card.Title = request.Title;
        card.Description = request.Description;
        card.Deadline = request.Deadline;
        card.AssigneeId = request.AssigneeId;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCard(int boardId, int columnId, int id)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id && c.ColumnId == columnId);
        if (card == null)
            return NotFound();

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/move")]
    public async Task<IActionResult> MoveCard(int boardId, int columnId, int id, MoveCardRequest request)
    {
        var userId = GetUserId();
        var role = await GetRole(boardId, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id && c.ColumnId == columnId);
        if (card == null)
            return NotFound();

        var targetColumn = await _db.Columns.FirstOrDefaultAsync(c => c.Id == request.ColumnId && c.BoardId == boardId);
        if (targetColumn == null)
            return BadRequest("Колонка назначения не найдена на этой доске.");

        card.ColumnId = request.ColumnId;
        card.Order = request.Order;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
