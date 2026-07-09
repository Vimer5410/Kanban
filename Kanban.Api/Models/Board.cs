namespace Kanban.Api.Models;

public class Board
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public List<BoardColumn> Columns { get; set; } = new();
    public List<UserRole> Members { get; set; } = new();
}
