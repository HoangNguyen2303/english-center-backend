using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 9 (phần 1) - Hộp yêu cầu từ học viên: đặt / hủy lịch 1 kèm 1, xin nghỉ, đổi lịch học
[Route("api/teacher/requests")]
public class RequestsController : TeacherControllerBase
{
    private readonly NotificationService _notify;

    public RequestsController(AppDbContext db, NotificationService notify) : base(db)
    {
        _notify = notify;
    }

    // GET api/teacher/requests?status=Pending&type=Leave|Reschedule|OneOnOneBooking|OneOnOneCancel
    [HttpGet]
    public async Task<IActionResult> GetAll(string? status, string? type)
    {
        var q = Db.StudentRequests.Where(r => r.TeacherId == TeacherId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(r => r.Type == type);

        var items = await q
            .OrderBy(r => r.Status == "Pending" ? 0 : 1).ThenByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.Type, r.Status, r.Reason, r.ResponseNote, r.CreatedAt, r.ProcessedAt,
                student = new { r.Student.Id, r.Student.Code, r.Student.FullName, r.Student.Phone },
                @class = r.Class == null ? null : new { r.Class.Id, r.Class.Name },
                session = r.Session == null ? null : new { r.Session.Id, r.Session.SessionNo, r.Session.Date, r.Session.StartTime, r.Session.EndTime },
                r.AvailabilityId, r.OneOnOneSessionId,
                r.Date, r.StartTime, r.EndTime
            })
            .ToListAsync();
        return Ok(items);
    }

    // POST api/teacher/requests/8/approve  { "note": "..." }
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id, NoteRequest req)
    {
        var r = await Db.StudentRequests.Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == id && x.TeacherId == TeacherId);
        if (r == null) return Error(404, "Không tìm thấy yêu cầu");
        if (r.Status != "Pending") return Error(409, "Yêu cầu đã được xử lý");

        var now = AppTime.Now;
        string message;

        switch (r.Type)
        {
            case "Leave":
            case "Reschedule":
            {
                // Duyệt xin nghỉ -> điểm danh tự đánh "Vắng có phép" cho buổi đó
                if (r.SessionId == null) return Error(400, "Yêu cầu không có buổi học");
                var session = await MySessionAsync(r.SessionId.Value);
                if (session == null) return Error(404, "Không tìm thấy buổi học của yêu cầu");

                var a = await Db.Attendances.FirstOrDefaultAsync(x => x.SessionId == session.Id && x.StudentId == r.StudentId);
                if (a == null)
                {
                    Db.Attendances.Add(new Attendance
                    {
                        SessionId = session.Id,
                        StudentId = r.StudentId,
                        Status = "Excused",
                        Note = r.Type == "Leave" ? "Đơn xin nghỉ đã duyệt" : "Đổi lịch đã duyệt",
                        IsAuto = true,
                        MarkedAt = now
                    });
                }
                else if (a.Status == "Absent")
                {
                    a.Status = "Excused";
                    a.IsAuto = true;
                    a.MarkedAt = now;
                }
                message = $"Yêu cầu {(r.Type == "Leave" ? "xin nghỉ" : "đổi lịch")} buổi {session.Date:dd/MM/yyyy} đã được duyệt";
                break;
            }

            case "OneOnOneBooking":
            {
                // Duyệt lịch 1 kèm 1 -> buổi tự hiện trên Lịch dạy của giáo viên và Schedule của học viên
                TeacherAvailability? slot = null;
                if (r.AvailabilityId != null)
                {
                    slot = await Db.TeacherAvailabilities.FirstOrDefaultAsync(x => x.Id == r.AvailabilityId && x.TeacherId == TeacherId);
                    if (slot == null) return Error(404, "Khung giờ rảnh không còn tồn tại");
                    if (slot.IsBooked) return Error(409, "Khung giờ này đã có học viên khác đặt");
                }

                var date = r.Date ?? slot?.Date;
                var start = r.StartTime ?? slot?.StartTime;
                var end = r.EndTime ?? slot?.EndTime;
                if (date == null || start == null || end == null || start >= end)
                    return Error(400, "Yêu cầu thiếu ngày giờ hợp lệ");
                if (AppTime.At(date.Value, start.Value) <= now) return Error(409, "Thời gian đặt lịch đã qua");

                var clash = await FindClashAsync(date.Value, start.Value, end.Value);
                if (clash != null) return Error(409, $"Trùng lịch dạy: {clash}");

                if (slot != null) slot.IsBooked = true;
                Db.OneOnOneSessions.Add(new OneOnOneSession
                {
                    TeacherId = TeacherId,
                    StudentId = r.StudentId,
                    AvailabilityId = slot?.Id,
                    RequestId = r.Id,
                    Date = date.Value,
                    StartTime = start.Value,
                    EndTime = end.Value,
                    Topic = r.Reason,
                    Status = "Scheduled",
                    CreatedAt = now
                });
                message = $"Lịch 1 kèm 1 ngày {date:dd/MM/yyyy} {start:HH\\:mm}-{end:HH\\:mm} đã được xác nhận";
                break;
            }

            case "OneOnOneCancel":
            {
                if (r.OneOnOneSessionId == null) return Error(400, "Yêu cầu không có buổi 1 kèm 1");
                var one = await Db.OneOnOneSessions.FirstOrDefaultAsync(x => x.Id == r.OneOnOneSessionId
                    && x.TeacherId == TeacherId && x.StudentId == r.StudentId);
                if (one == null) return Error(404, "Không tìm thấy buổi 1 kèm 1");

                one.Status = "Cancelled";
                if (one.AvailabilityId != null)
                {
                    var slot = await Db.TeacherAvailabilities.FindAsync(one.AvailabilityId.Value);
                    if (slot != null) slot.IsBooked = false;
                }
                message = $"Đã hủy buổi 1 kèm 1 ngày {one.Date:dd/MM/yyyy}";
                break;
            }

            default:
                return Error(400, "Loại yêu cầu không hợp lệ");
        }

        r.Status = "Approved";
        r.ResponseNote = req.Note?.Trim();
        r.ProcessedAt = now;
        _notify.Add(r.Student.UserId, "Request", message, r.ResponseNote, "/student/schedule", UserId);

        await Db.SaveChangesAsync();
        return Ok(new { r.Id, r.Status, message });
    }

    // POST api/teacher/requests/8/reject  { "reason": "..." }
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, ReasonRequest req)
    {
        var r = await Db.StudentRequests.Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == id && x.TeacherId == TeacherId);
        if (r == null) return Error(404, "Không tìm thấy yêu cầu");
        if (r.Status != "Pending") return Error(409, "Yêu cầu đã được xử lý");

        r.Status = "Rejected";
        r.ResponseNote = req.Reason.Trim();
        r.ProcessedAt = AppTime.Now;

        var label = r.Type switch
        {
            "Leave" => "xin nghỉ",
            "Reschedule" => "đổi lịch học",
            "OneOnOneBooking" => "đặt lịch 1 kèm 1",
            _ => "hủy lịch 1 kèm 1"
        };
        _notify.Add(r.Student.UserId, "Request", $"Yêu cầu {label} của bạn bị từ chối", r.ResponseNote, "/student/schedule", UserId);

        await Db.SaveChangesAsync();
        return Ok(new { r.Id, r.Status, r.ResponseNote });
    }

    // GET api/teacher/requests/one-on-one?from=&to=  -> các buổi 1 kèm 1 đã xác nhận
    [HttpGet("one-on-one")]
    public async Task<IActionResult> OneOnOneSessions(DateOnly? from, DateOnly? to)
    {
        var start = from ?? AppTime.Today.AddDays(-30);
        var end = to ?? AppTime.Today.AddDays(60);
        var items = await Db.OneOnOneSessions
            .Where(o => o.TeacherId == TeacherId && o.Date >= start && o.Date <= end)
            .OrderBy(o => o.Date).ThenBy(o => o.StartTime)
            .Select(o => new
            {
                o.Id, o.Date, o.StartTime, o.EndTime, o.Topic, o.MeetingUrl, o.Status,
                student = new { o.Student.Id, o.Student.Code, o.Student.FullName }
            })
            .ToListAsync();
        return Ok(items);
    }

    // PUT api/teacher/requests/one-on-one/3/meeting-url  -> gắn link online cho buổi 1 kèm 1
    [HttpPut("one-on-one/{id}/meeting-url")]
    public async Task<IActionResult> SetOneOnOneLink(int id, MeetingUrlRequest req)
    {
        var o = await Db.OneOnOneSessions.FirstOrDefaultAsync(x => x.Id == id && x.TeacherId == TeacherId);
        if (o == null) return Error(404, "Không tìm thấy buổi 1 kèm 1");
        o.MeetingUrl = string.IsNullOrWhiteSpace(req.MeetingUrl) ? null : req.MeetingUrl.Trim();
        await Db.SaveChangesAsync();
        return Ok(new { o.Id, o.MeetingUrl });
    }
}
