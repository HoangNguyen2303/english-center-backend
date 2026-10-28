using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.Admin;

// Admin duyệt đơn của giáo viên: xin nghỉ, đổi buổi (tạo buổi dạy bù), xin mở lại điểm danh đã khóa
[ApiController]
[Route("api/admin/teacher-requests")]
[Authorize(Roles = "admin")]
public class TeacherRequestsController : ControllerBase
{
    public const int UnlockHours = 24;   // mở lại điểm danh trong 24 giờ

    private readonly AppDbContext _db;
    private readonly SessionService _sessions;
    private readonly NotificationService _notify;

    public TeacherRequestsController(AppDbContext db, SessionService sessions, NotificationService notify)
    {
        _db = db;
        _sessions = sessions;
        _notify = notify;
    }

    // GET api/admin/teacher-requests?status=Pending&type=Leave|Reschedule|AttendanceUnlock
    [HttpGet]
    public async Task<IActionResult> GetAll(string? status, string? type)
    {
        var q = _db.TeacherRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(r => r.Type == type);

        var items = await q
            .OrderBy(r => r.Status == "Pending" ? 0 : 1).ThenByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.Type, r.Status, r.Reason, r.AdminNote, r.CreatedAt, r.ProcessedAt,
                r.ProposedDate, r.ProposedStartTime, r.ProposedEndTime,
                teacher = new { r.Teacher.Id, r.Teacher.FullName },
                session = new
                {
                    r.Session.Id, r.Session.ClassId, ClassName = r.Session.Class.Name, r.Session.SessionNo,
                    r.Session.Date, r.Session.StartTime, r.Session.EndTime, r.Session.RoomId,
                    RoomName = r.Session.Room != null ? r.Session.Room.Name : null
                }
            })
            .ToListAsync();
        return Ok(items);
    }

    // POST api/admin/teacher-requests/4/approve  { "note": "..." }
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id, NoteRequest req)
    {
        var r = await _db.TeacherRequests
            .Include(x => x.Teacher)
            .Include(x => x.Session).ThenInclude(s => s.Class)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "Không tìm thấy đơn" });
        if (r.Status != "Pending") return Conflict(new { message = "Đơn đã được xử lý" });

        var now = AppTime.Now;
        var s = r.Session;
        string teacherMsg;

        if (r.Type == "AttendanceUnlock")
        {
            s.AttendanceUnlockedUntil = now.AddHours(UnlockHours);
            teacherMsg = $"Admin đã mở lại điểm danh buổi {s.SessionNo} lớp {s.Class.Name} trong {UnlockHours} giờ";
        }
        else
        {
            if (s.Status == "Cancelled") return Conflict(new { message = "Buổi học đã bị hủy trước đó" });

            ClassSession? makeup = null;
            if (r.ProposedDate != null && r.ProposedStartTime != null && r.ProposedEndTime != null)
            {
                var date = r.ProposedDate.Value;
                var start = r.ProposedStartTime.Value;
                var end = r.ProposedEndTime.Value;

                // Kiểm tra trùng phòng và trùng lịch giáo viên ở buổi bù
                if (s.RoomId != null)
                {
                    var roomClash = await _db.ClassSessions.AnyAsync(x => x.RoomId == s.RoomId && x.Status == "Scheduled"
                        && x.Date == date && x.StartTime < end && x.EndTime > start);
                    if (roomClash) return Conflict(new { message = "Phòng học bị trùng lịch ở buổi bù đề xuất" });
                }
                var teacherClash = await _db.ClassSessions.AnyAsync(x => x.Class.TeacherId == r.TeacherId && x.Status == "Scheduled"
                        && x.Id != s.Id && x.Date == date && x.StartTime < end && x.EndTime > start)
                    || await _db.OneOnOneSessions.AnyAsync(o => o.TeacherId == r.TeacherId && o.Status == "Scheduled"
                        && o.Date == date && o.StartTime < end && o.EndTime > start);
                if (teacherClash) return Conflict(new { message = "Giáo viên bị trùng lịch ở buổi bù đề xuất" });

                if (await _db.ClassSessions.AnyAsync(x => x.ClassId == s.ClassId && x.Date == date && x.StartTime == start))
                    return Conflict(new { message = "Lớp đã có buổi học vào giờ này" });

                makeup = new ClassSession
                {
                    ClassId = s.ClassId,
                    RoomId = s.RoomId,
                    Date = date,
                    StartTime = start,
                    EndTime = end,
                    Type = "Makeup",
                    Status = "Scheduled",
                    ReplacesSessionId = s.Id,
                    SyllabusLessonId = s.SyllabusLessonId
                };
                _db.ClassSessions.Add(makeup);
            }

            s.Status = "Cancelled";

            var change = $"Buổi {s.Date:dd/MM/yyyy} {s.StartTime:HH\\:mm} lớp {s.Class.Name} được nghỉ"
                + (makeup != null ? $", dạy bù ngày {makeup.Date:dd/MM/yyyy} {makeup.StartTime:HH\\:mm}-{makeup.EndTime:HH\\:mm}" : "");
            teacherMsg = "Đơn đã được duyệt. " + change;
            await _notify.AddToClassStudentsAsync(s.ClassId, "ScheduleChange", "Lịch học thay đổi", change, "/student/schedule");
        }

        r.Status = "Approved";
        r.AdminNote = req.Note?.Trim();
        r.ProcessedAt = now;
        _notify.Add(r.Teacher.UserId, r.Type == "AttendanceUnlock" ? "Request" : "ScheduleChange",
            teacherMsg, r.AdminNote, "/teacher/schedule");

        await _db.SaveChangesAsync();
        if (r.Type != "AttendanceUnlock") await _sessions.SyncAsync(s.ClassId);   // đánh số buổi lại

        return Ok(new { r.Id, r.Status, message = teacherMsg });
    }

    // POST api/admin/teacher-requests/4/reject  { "reason": "..." }
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, ReasonRequest req)
    {
        var r = await _db.TeacherRequests.Include(x => x.Teacher).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "Không tìm thấy đơn" });
        if (r.Status != "Pending") return Conflict(new { message = "Đơn đã được xử lý" });

        r.Status = "Rejected";
        r.AdminNote = req.Reason.Trim();
        r.ProcessedAt = AppTime.Now;
        _notify.Add(r.Teacher.UserId, "Request", "Đơn của bạn bị từ chối", r.AdminNote, "/teacher/schedule");

        await _db.SaveChangesAsync();
        return Ok(new { r.Id, r.Status, r.AdminNote });
    }
}
