using System.Security.Claims;
using Kanban.Api.Data;
using Kanban.Api.Dtos;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username и password обязательны.");

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return Conflict("Юзер с таким именем уже есть.");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = PasswordHasher.Hash(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Username));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Неверный логин или пароль.");

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Username));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<string> Me()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        return Ok(username);
    }
}
