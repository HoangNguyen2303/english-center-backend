using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 9 (phần 2) - Thông báo: gửi cho lớp / học viên, lịch sử đã gửi, hộp thông báo đến giáo viên (chuông)
[Route("api/teacher")]
public class NotificationsController : TeacherControllerBase
{
    private readonly NotificationService _notify;

    public NotificationsController(AppDbContext db, NotificationService notify) : base(db)
    {
        _notify = notify;
    }

    // GET api/teacher/notifications?unreadOnly=false&type=&page=1&pageSize=20
    [HttpGet("notifications")]
    public async Task<IActionResult> GetMine(bool unreadOnly = false, string? type = null, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = Db.Notifications.Where(n => n.RecipientUserId == UserId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(n => n.Type == type);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { n.Id, n.Type, n.Title, n.Content, n.Link, n.IsRead, n.CreatedAt })
            .ToListAsync();

        return Ok(new
        {
            totalItems = total,
            page,
            pageSize,
            unreadCount = await Db.Notifications.CountAsync(n => n.RecipientUserId == UserId && !n.IsRead),
            items
        });
    }

    // GET api/teacher/notifications/unread-count  (số trên chuông)
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await Db.Notifications.CountAsync(n => n.RecipientUserId == UserId && !n.IsRead);
        return Ok(new { unreadCount = count });
    }

    // POST api/teacher/notifications/9/read
    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var n = await Db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientUserId == UserId);
        if (n == null) return Error(404, "Không tìm thấy thông báo");
        n.IsRead = true;
        await Db.SaveChangesAsync();
        return Ok(new { n.Id, n.IsRead });
    }

    // POST api/teacher/notifications/read-all
    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var items = await Db.Notifications.Where(n => n.RecipientUserId == UserId && !n.IsRead).ToListAsync();
        foreach (var n in items) n.IsRead = true;
        await Db.SaveChangesAsync();
        return Ok(new { updated = items.Count });
    }

    // POST api/teacher/announcements  -> gửi thông báo cho cả lớp (đổi phòng, nhắc hạn nộp, nghỉ học) hoặc từng học viên
    [HttpPost("announcements")]
    public async Task<IActionResult> Send(AnnouncementCreate req)
    {
        if (req.ClassId != null && !await OwnsClassAsync(req.ClassId.Value))
            return Error(400, "Lớp không tồn tại hoặc bạn không phụ trách lớp này");

        List<int> studentIds;
        if (req.StudentIds.Count > 0)
        {
            var ids = req.StudentIds.Distinct().ToList();
            var allowed = Db.Enrollments.Where(e => e.Class.TeacherId == TeacherId && ids.Contains(e.StudentId));
            if (req.ClassId != null) allowed = allowed.Where(e => e.ClassId == req.ClassId);
            studentIds = await allowed.Select(e => e.StudentId).Distinct().ToListAsync();
            if (studentIds.Count != ids.Count)
                return Error(400, "Có học viên không thuộc lớp bạn dạy");
        }
        else
        {
            studentIds = await Db.Enrollments
                .Where(e => e.ClassId == req.ClassId && e.Status == "Active")
                .Select(e => e.StudentId).ToListAsync();
            if (studentIds.Count == 0) return Error(409, "Lớp chưa có học viên đang học");
        }

        var userIds = await Db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.UserId).ToListAsync();

        var a = new Announcement
        {
            TeacherId = TeacherId,
            ClassId = req.ClassId,
            Title = req.Title.Trim(),
            Content = req.Content.Trim(),
            RecipientCount = userIds.Count,
            CreatedAt = AppTime.Now
        };
        Db.Announcements.Add(a);
        _notify.Add(userIds, "Announcement", a.Title, a.Content, "/student/notifications", UserId, a);

        await Db.SaveChangesAsync();
        return StatusCode(201, new { a.Id, a.RecipientCount, message = $"Đã gửi thông báo đến {a.RecipientCount} học viên" });
    }

    // GET api/teacher/announcements?classId=  -> lịch sử thông báo đã gửi
    [HttpGet("announcements")]
    public async Task<IActionResult> History(int? classId, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = Db.Announcements.Where(a => a.TeacherId == TeacherId);
        if (classId != null) q = q.Where(a => a.ClassId == classId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Title, a.Content, a.ClassId, ClassName = a.Class != null ? a.Class.Name : null,
                a.RecipientCount, ReadCount = a.Notifications.Count(n => n.IsRead), a.CreatedAt
            })
            .ToListAsync();

        return Ok(new { totalItems = total, page, pageSize, items });
    }
}
