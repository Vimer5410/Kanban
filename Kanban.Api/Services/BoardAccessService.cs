using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services;

public class BoardAccessService
{
    private readonly AppDbContext _db;

    public BoardAccessService(AppDbContext db)
    {
        _db = db;
    }

    
    public async Task<BoardRole?> GetRoleAsync(int boardId, int userId)
    {
        var board = await _db.Boards.FindAsync(boardId);
        if (board is null)
            return null;

        if (board.OwnerId == userId)
            return BoardRole.Owner;

        var membership = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.BoardId == boardId && ur.UserId == userId);

        return membership?.Role;
    }
}
