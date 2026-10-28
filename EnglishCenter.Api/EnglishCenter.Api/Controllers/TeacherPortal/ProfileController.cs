using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 10 - Hồ sơ. Thông tin do Admin quản lý, giáo viên chỉ xem (trừ ảnh đại diện và mật khẩu).
[Route("api/teacher/profile")]
public class ProfileController : TeacherControllerBase
{
    private readonly TokenService _tokens;
    private readonly FileStorageService _files;

    public ProfileController(AppDbContext db, TokenService tokens, FileStorageService files) : base(db)
    {
        _tokens = tokens;
        _files = files;
    }

    // GET api/teacher/profile
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var t = await Db.Teachers.Where(x => x.Id == TeacherId)
            .Select(x => new
            {
                x.Id, x.FullName, x.User.Email, x.Phone, x.Specialty, x.Degree, x.HireDate, x.Status,
                x.User.AvatarUrl,
                ClassCount = x.Classes.Count()
            })
            .FirstAsync();
        return Ok(t);
    }

    // POST api/teacher/profile/avatar  (multipart/form-data, field "file": ảnh jpg/png/webp)
    [HttpPost("avatar")]
    [RequestSizeLimit(FileStorageService.MaxBytes)]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var error = FileStorageService.Validate(file, "avatars");
        if (error != null) return Error(400, error);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return Error(400, "Ảnh đại diện phải là jpg, png hoặc webp");
        if (file.Length > 5 * 1024 * 1024) return Error(400, "Ảnh đại diện tối đa 5 MB");

        var saved = await _files.SaveAsync(file, "avatars");
        var user = await Db.Users.FirstAsync(u => u.Id == UserId);
        user.AvatarUrl = saved.Url;
        await Db.SaveChangesAsync();
        return Ok(new { avatarUrl = user.AvatarUrl });
    }

    // GET api/teacher/profile/classes  -> lịch sử các lớp đã / đang dạy
    [HttpGet("classes")]
    public async Task<IActionResult> Classes()
    {
        var today = AppTime.Today;
        var items = await MyClasses
            .OrderByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id, c.Name, CourseName = c.Course.Name, c.StartDate, c.EndDate,
                StudentCount = c.Enrollments.Count(e => e.Status != "Dropped")
            })
            .ToListAsync();
        return Ok(items.Select(c => new
        {
            c.Id, c.Name, c.CourseName, c.StartDate, c.EndDate, c.StudentCount,
            status = ClassStatus(c.StartDate, c.EndDate, today)
        }));
    }

    // GET api/teacher/profile/ratings  -> đánh giá từ học viên (Rating / Survey bên Student)
    [HttpGet("ratings")]
    public async Task<IActionResult> Ratings(int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = Db.TeacherRatings.Where(r => r.TeacherId == TeacherId);
        var total = await q.CountAsync();
        var avg = total == 0 ? (double?)null : await q.AverageAsync(r => (double)r.Score);
        var distribution = await q.GroupBy(r => r.Score)
            .Select(g => new { score = g.Key, count = g.Count() })
            .ToListAsync();

        var comments = await q.Where(r => r.Comment != null && r.Comment != "")
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.Score, r.Comment, r.CreatedAt,
                ClassName = r.Class != null ? r.Class.Name : null,
                StudentName = r.Student.FullName
            })
            .ToListAsync();

        return Ok(new
        {
            totalRatings = total,
            averageScore = avg == null ? null : (double?)Math.Round(avg.Value, 2),
            distribution = Enumerable.Range(1, 5).Select(s => new
            {
                score = s,
                count = distribution.FirstOrDefault(d => d.score == s)?.count ?? 0
            }),
            page,
            pageSize,
            comments
        });
    }

    // GET api/teacher/profile/timesheet?month=2026-11  -> bảng công: số buổi, số giờ đã dạy (tính từ buổi đã điểm danh)
    [HttpGet("timesheet")]
    public async Task<IActionResult> Timesheet(string? month)
    {
        var today = AppTime.Today;
        int year = today.Year, mon = today.Month;
        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out year) || !int.TryParse(parts[1], out mon) || mon < 1 || mon > 12)
                return Error(400, "month phải có dạng yyyy-MM, ví dụ 2026-11");
        }
        var start = new DateOnly(year, mon, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var sessions = await Db.ClassSessions
            .Where(s => s.Class.TeacherId == TeacherId && s.Status == "Scheduled"
                && s.Date >= start && s.Date <= end && s.AttendanceTakenAt != null)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id, s.Date, s.StartTime, s.EndTime, s.Type, s.SessionNo, s.ClassId, ClassName = s.Class.Name,
                RoomName = s.Room != null ? s.Room.Name : null
            })
            .ToListAsync();

        var now = AppTime.Now;
        var ones = (await Db.OneOnOneSessions
                .Where(o => o.TeacherId == TeacherId && o.Status == "Scheduled" && o.Date >= start && o.Date <= end)
                .Select(o => new { o.Id, o.Date, o.StartTime, o.EndTime, StudentName = o.Student.FullName })
                .ToListAsync())
            .Where(o => AppTime.At(o.Date, o.EndTime) <= now)
            .ToList();

        static decimal Hours(TimeOnly s, TimeOnly e) => Math.Round((decimal)(e - s).TotalHours, 2);

        // Buổi đã đến giờ nhưng chưa điểm danh -> chưa được tính công
        var notMarked = await Db.ClassSessions
            .Where(s => s.Class.TeacherId == TeacherId && s.Status == "Scheduled"
                && s.Date >= start && s.Date <= end && s.AttendanceTakenAt == null
                && (s.Date < today))
            .CountAsync();

        return Ok(new
        {
            month = $"{year:0000}-{mon:00}",
            classSessions = sessions.Count,
            classHours = sessions.Sum(s => Hours(s.StartTime, s.EndTime)),
            makeupSessions = sessions.Count(s => s.Type == "Makeup"),
            oneOnOneSessions = ones.Count,
            oneOnOneHours = ones.Sum(o => Hours(o.StartTime, o.EndTime)),
            totalSessions = sessions.Count + ones.Count,
            totalHours = sessions.Sum(s => Hours(s.StartTime, s.EndTime)) + ones.Sum(o => Hours(o.StartTime, o.EndTime)),
            sessionsNotMarked = notMarked,
            byClass = sessions.GroupBy(s => new { s.ClassId, s.ClassName }).Select(g => new
            {
                g.Key.ClassId, g.Key.ClassName, sessions = g.Count(), hours = g.Sum(s => Hours(s.StartTime, s.EndTime))
            }),
            details = sessions.Select(s => new
            {
                kind = s.Type == "Makeup" ? "Makeup" : "Class", s.Id, s.Date, s.StartTime, s.EndTime,
                title = s.ClassName, s.SessionNo, s.RoomName, hours = Hours(s.StartTime, s.EndTime)
            }).Concat(ones.Select(o => new
            {
                kind = "OneOnOne", o.Id, o.Date, o.StartTime, o.EndTime,
                title = "1 kèm 1: " + o.StudentName, SessionNo = 0, RoomName = (string?)null, hours = Hours(o.StartTime, o.EndTime)
            })).OrderBy(x => x.Date).ThenBy(x => x.StartTime)
        });
    }

    // POST api/teacher/profile/change-password
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(TeacherChangePasswordRequest req)
    {
        var user = await Db.Users.FirstAsync(u => u.Id == UserId);
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Error(400, "Mật khẩu hiện tại không đúng");
        if (req.CurrentPassword == req.NewPassword)
            return Error(400, "Mật khẩu mới phải khác mật khẩu hiện tại");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.TokenVersion++;   // các phiên đăng nhập khác bị đăng xuất
        await Db.SaveChangesAsync();

        // Trả token mới để giáo viên không bị đá ra khỏi phiên hiện tại
        return Ok(new { message = "Đổi mật khẩu thành công", token = _tokens.CreateToken(user) });
    }

    // POST api/teacher/profile/logout
    // JWT không lưu phiên trên server: tăng TokenVersion để token hiện tại (và mọi token cũ) hết hiệu lực.
    // Frontend xóa token đã lưu sau khi gọi.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await Db.Users.FirstAsync(u => u.Id == UserId);
        user.TokenVersion++;
        await Db.SaveChangesAsync();
        return Ok(new { message = "Đã đăng xuất" });
    }
}
