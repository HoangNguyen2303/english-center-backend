using System.Security.Claims;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Lớp cha cho mọi API của giáo viên (route api/teacher/...).
// Trước mỗi action: lấy hồ sơ Teacher từ tài khoản đang đăng nhập, gán vào TeacherId.
// Giáo viên chỉ được thao tác trên lớp mình phụ trách -> luôn lọc theo TeacherId.
[ApiController]
[Authorize(Roles = "teacher")]
public abstract class TeacherControllerBase : ControllerBase, IAsyncActionFilter
{
    protected readonly AppDbContext Db;

    protected TeacherControllerBase(AppDbContext db)
    {
        Db = db;
    }

    protected int UserId { get; private set; }
    protected int TeacherId { get; private set; }
    protected string TeacherName { get; private set; } = "";

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Result = Unauthorized(new { message = "Token không hợp lệ" });
            return;
        }

        var teacher = await Db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id, t.FullName })
            .FirstOrDefaultAsync();

        if (teacher == null)
        {
            context.Result = StatusCode(403, new { message = "Tài khoản chưa có hồ sơ giáo viên" });
            return;
        }

        UserId = userId;
        TeacherId = teacher.Id;
        TeacherName = teacher.FullName;
        await next();
    }

    protected IQueryable<CourseClass> MyClasses => Db.Classes.Where(c => c.TeacherId == TeacherId);

    protected Task<bool> OwnsClassAsync(int classId) => MyClasses.AnyAsync(c => c.Id == classId);

    protected Task<ClassSession?> MySessionAsync(int sessionId) =>
        Db.ClassSessions.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == TeacherId);

    // Học viên thuộc ít nhất một lớp mình dạy
    protected Task<bool> TeachesStudentAsync(int studentId) =>
        Db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.Class.TeacherId == TeacherId);

    // Kiểm tra trùng lịch của giáo viên (buổi lớp + 1 kèm 1) trong một khung giờ
    protected async Task<string?> FindClashAsync(DateOnly date, TimeOnly start, TimeOnly end, int? ignoreOneOnOneId = null)
    {
        var cls = await Db.ClassSessions
            .Where(s => s.Class.TeacherId == TeacherId && s.Status == "Scheduled" && s.Date == date
                && s.StartTime < end && s.EndTime > start)
            .Select(s => new { s.Class.Name, s.StartTime, s.EndTime })
            .FirstOrDefaultAsync();
        if (cls != null) return $"lớp {cls.Name} ({cls.StartTime:HH\\:mm}-{cls.EndTime:HH\\:mm})";

        var one = await Db.OneOnOneSessions
            .Where(o => o.TeacherId == TeacherId && o.Status == "Scheduled" && o.Date == date
                && o.StartTime < end && o.EndTime > start && o.Id != ignoreOneOnOneId)
            .Select(o => new { o.Student.FullName, o.StartTime, o.EndTime })
            .FirstOrDefaultAsync();
        if (one != null) return $"buổi 1 kèm 1 với {one.FullName} ({one.StartTime:HH\\:mm}-{one.EndTime:HH\\:mm})";

        return null;
    }

    protected ObjectResult Error(int statusCode, string message) =>
        StatusCode(statusCode, new { message });

    protected static string ClassStatus(DateOnly start, DateOnly end, DateOnly today)
    {
        if (today < start) return "Upcoming";
        if (today > end) return "Finished";
        return "Ongoing";
    }
}
