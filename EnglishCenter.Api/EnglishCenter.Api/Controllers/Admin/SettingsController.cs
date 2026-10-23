using System.Security.Claims;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "admin")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public SettingsController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    // GET api/admin/settings/center
    [HttpGet("center")]
    public async Task<IActionResult> GetCenter()
    {
        var c = await _db.CenterSettings.FindAsync(1);
        if (c == null)
            return NotFound(new { message = "Chưa có thông tin trung tâm" });

        return Ok(new CenterSettingResponse
        {
            Name = c.Name,
            LogoUrl = c.LogoUrl,
            Address = c.Address,
            Phone = c.Phone
        });
    }

    // PUT api/admin/settings/center
    [HttpPut("center")]
    public async Task<IActionResult> UpdateCenter(CenterSettingRequest req)
    {
        var c = await _db.CenterSettings.FindAsync(1);
        if (c == null)
            return NotFound(new { message = "Chưa có thông tin trung tâm" });

        c.Name = req.Name.Trim();
        c.Address = req.Address?.Trim();
        c.Phone = req.Phone?.Trim();
        await _db.SaveChangesAsync();

        return Ok(new CenterSettingResponse
        {
            Name = c.Name,
            LogoUrl = c.LogoUrl,
            Address = c.Address,
            Phone = c.Phone
        });
    }

    // POST api/admin/settings/change-password
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "Không tìm thấy tài khoản" });

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.TokenVersion++;   // làm mọi token cũ mất hiệu lực
        await _db.SaveChangesAsync();

        // Trả token mới để admin không bị đá ra khỏi phiên hiện tại
        return Ok(new { message = "Đổi mật khẩu thành công", token = _tokens.CreateToken(user) });
    }
}