using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos;
using EnglishCenter.Api.Dtos.Common;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);

        // Cùng một thông báo cho "sai email" và "sai mật khẩu" để không lộ email nào tồn tại
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });

        if (user.IsLocked)
            return StatusCode(403, new { message = "Tài khoản đã bị khóa" });

        return Ok(new LoginResponse
        {
            Token = _tokens.CreateToken(user),
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role
        });
    }

    // Endpoint thử: ai đăng nhập rồi cũng gọi được
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }
}