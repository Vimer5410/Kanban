namespace Kanban.Api.Dtos;

public record ColumnDto(int Id, int BoardId, string Title, int Order);
public record CreateColumnRequest(string Title);
public record UpdateColumnRequest(string Title, int Order);
