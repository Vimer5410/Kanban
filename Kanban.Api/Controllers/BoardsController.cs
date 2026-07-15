using System.Security.Claims;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

public record BoardDto(int Id, string Title, string? Description, int OwnerId, string OwnerUsername, BoardRole MyRole, DateTime CreatedAt);
public record CreateBoardRequest(string Title, string? Description);
public record UpdateBoardRequest(string Title, string? Description);
public record AddMemberRequest(string Username, BoardRole Role);
public record MemberDto(int UserId, string Username, BoardRole Role);

[ApiController]
[Route("api/boards")]
[Authorize]
public class BoardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BoardsController(AppDbContext db)
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
    public async Task<IActionResult> GetMyBoards()
    {
        var userId = GetUserId();

        var owned = await _db.Boards.Where(b => b.OwnerId == userId).ToListAsync();
        var memberBoardIds = await _db.UserRoles.Where(x => x.UserId == userId).Select(x => x.BoardId).ToListAsync();
        var memberBoards = await _db.Boards.Where(b => memberBoardIds.Contains(b.Id)).ToListAsync();

        var all = owned.Concat(memberBoards).GroupBy(b => b.Id).Select(g => g.First()).ToList();

        var result = new List<BoardDto>();
        foreach (var b in all)
        {
            var owner = await _db.Users.FindAsync(b.OwnerId);
            var role = b.OwnerId == userId ? BoardRole.Owner : await GetRole(b.Id, userId);
            result.Add(new BoardDto(b.Id, b.Title, b.Description, b.OwnerId, owner!.Username, role!.Value, b.CreatedAt));
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoard(int id)
    {
        var userId = GetUserId();
        var role = await GetRole(id, userId);
        if (role == null)
            return NotFound();

        var board = await _db.Boards.FindAsync(id);
        var owner = await _db.Users.FindAsync(board!.OwnerId);

        return Ok(new BoardDto(board.Id, board.Title, board.Description, board.OwnerId, owner!.Username, role.Value, board.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard(CreateBoardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();

        var board = new Board
        {
            Title = request.Title,
            Description = request.Description,
            OwnerId = userId
        };

        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        var owner = await _db.Users.FindAsync(userId);
        return Ok(new BoardDto(board.Id, board.Title, board.Description, board.OwnerId, owner!.Username, BoardRole.Owner, board.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBoard(int id, UpdateBoardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = GetUserId();
        var role = await GetRole(id, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner && role != BoardRole.Editor)
            return Forbid();

        var board = await _db.Boards.FindAsync(id);
        board!.Title = request.Title;
        board.Description = request.Description;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBoard(int id)
    {
        var userId = GetUserId();
        var role = await GetRole(id, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var board = await _db.Boards.FindAsync(id);
        _db.Boards.Remove(board!);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        var userId = GetUserId();
        var role = await GetRole(id, userId);
        if (role == null)
            return NotFound();

        var board = await _db.Boards.FindAsync(id);
        var owner = await _db.Users.FindAsync(board!.OwnerId);

        var members = await _db.UserRoles.Where(x => x.BoardId == id).ToListAsync();

        var result = new List<MemberDto> { new MemberDto(board.OwnerId, owner!.Username, BoardRole.Owner) };
        foreach (var m in members)
        {
            var u = await _db.Users.FindAsync(m.UserId);
            result.Add(new MemberDto(m.UserId, u!.Username, m.Role));
        }

        return Ok(result);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, AddMemberRequest request)
    {
        if (request.Role == BoardRole.Owner)
            return BadRequest("Owner нельзя назначить участнику.");

        var userId = GetUserId();
        var role = await GetRole(id, userId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var targetUser = await _db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
        if (targetUser == null)
            return NotFound("Юзер не найден.");

        var board = await _db.Boards.FindAsync(id);
        if (targetUser.Id == board!.OwnerId)
            return BadRequest("Это уже владелец доски.");

        var existing = await _db.UserRoles.FirstOrDefaultAsync(x => x.BoardId == id && x.UserId == targetUser.Id);
        if (existing != null)
        {
            existing.Role = request.Role;
        }
        else
        {
            _db.UserRoles.Add(new UserRole { BoardId = id, UserId = targetUser.Id, Role = request.Role });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        var currentUserId = GetUserId();
        var role = await GetRole(id, currentUserId);
        if (role == null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var membership = await _db.UserRoles.FirstOrDefaultAsync(x => x.BoardId == id && x.UserId == userId);
        if (membership == null)
            return NotFound();

        _db.UserRoles.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
