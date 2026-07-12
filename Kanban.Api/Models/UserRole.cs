namespace Kanban.Api.Models;

public enum BoardRole
{
    Owner,
    Editor,
    Viewer
}


public class UserRole
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;

    public BoardRole Role { get; set; }
}
