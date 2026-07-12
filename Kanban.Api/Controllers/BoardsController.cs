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
[Route("api/boards")]
[Authorize]
public class BoardsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BoardAccessService _access;

    public BoardsController(AppDbContext db, BoardAccessService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<BoardDto>>> GetMyBoards()
    {
        var userId = User.GetUserId();

        var boards = await _db.Boards
            .Include(b => b.Owner)
            .Include(b => b.Members)
            .Where(b => b.OwnerId == userId || b.Members.Any(m => m.UserId == userId))
            .ToListAsync();

        var result = boards.Select(b => new BoardDto(
            b.Id,
            b.Title,
            b.Description,
            b.OwnerId,
            b.Owner.Username,
            b.OwnerId == userId ? BoardRole.Owner : b.Members.First(m => m.UserId == userId).Role,
            b.CreatedAt));

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BoardDto>> GetBoard(int id)
    {
        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, userId);
        if (role is null)
            return NotFound();

        var board = await _db.Boards.Include(b => b.Owner).FirstAsync(b => b.Id == id);
        return Ok(new BoardDto(board.Id, board.Title, board.Description, board.OwnerId, board.Owner.Username, role.Value, board.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<BoardDto>> CreateBoard(CreateBoardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title обязателен.");

        var userId = User.GetUserId();

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

        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, userId);
        if (role is null)
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
        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, userId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var board = await _db.Boards.FindAsync(id);
        _db.Boards.Remove(board!);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<MemberDto>>> GetMembers(int id)
    {
        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, userId);
        if (role is null)
            return NotFound();

        var board = await _db.Boards.Include(b => b.Owner).FirstAsync(b => b.Id == id);
        var members = await _db.UserRoles
            .Include(ur => ur.User)
            .Where(ur => ur.BoardId == id)
            .Select(ur => new MemberDto(ur.UserId, ur.User.Username, ur.Role))
            .ToListAsync();

        var result = new List<MemberDto> { new(board.OwnerId, board.Owner.Username, BoardRole.Owner) };
        result.AddRange(members);

        return Ok(result);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, AddMemberRequest request)
    {
        if (request.Role == BoardRole.Owner)
            return BadRequest("Owner привязан к владельцу доски, участнику эту роль назначить нельзя.");

        var userId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, userId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var targetUser = await _db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
        if (targetUser is null)
            return NotFound("Юзер не найден.");

        var board = await _db.Boards.FindAsync(id);
        if (targetUser.Id == board!.OwnerId)
            return BadRequest("Это уже владелец доски.");

        var existing = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.BoardId == id && ur.UserId == targetUser.Id);

        if (existing is not null)
            existing.Role = request.Role;
        else
            _db.UserRoles.Add(new UserRole { BoardId = id, UserId = targetUser.Id, Role = request.Role });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        var currentUserId = User.GetUserId();
        var role = await _access.GetRoleAsync(id, currentUserId);
        if (role is null)
            return NotFound();
        if (role != BoardRole.Owner)
            return Forbid();

        var membership = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.BoardId == id && ur.UserId == userId);
        if (membership is null)
            return NotFound();

        _db.UserRoles.Remove(membership);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
