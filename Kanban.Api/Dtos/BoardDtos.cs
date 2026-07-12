using Kanban.Api.Models;

namespace Kanban.Api.Dtos;

public record BoardDto(int Id, string Title, string? Description, int OwnerId, string OwnerUsername, BoardRole MyRole, DateTime CreatedAt);
public record CreateBoardRequest(string Title, string? Description);
public record UpdateBoardRequest(string Title, string? Description);
public record AddMemberRequest(string Username, BoardRole Role);
public record MemberDto(int UserId, string Username, BoardRole Role);
