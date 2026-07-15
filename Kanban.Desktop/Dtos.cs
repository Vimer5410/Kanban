namespace Kanban.Desktop;

public record AuthResult(string Token, string Username);
public record BoardDto(int Id, string Title);
public record ColumnDto(int Id, string Title);
public record CardDto(int Id, string Title, string? Description, DateTime? Deadline, string? AssigneeUsername);
public record MemberDto(int UserId, string Username);
public record CommentDto(string AuthorUsername, string Text);
public record CardRef(int CardId, int ColumnId);
