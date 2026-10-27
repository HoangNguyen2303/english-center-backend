using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 4 - Điểm danh (nằm trong trang chi tiết lớp)
[Route("api/teacher")]
public class AttendanceController : TeacherControllerBase
{
    private readonly SessionService _sessions;
    private readonly StatsService _stats;
    private readonly NotificationService _notify;

    public AttendanceController(AppDbContext db, SessionService sessions, StatsService stats,
        NotificationService notify) : base(db)
    {
        _sessions = sessions;
        _stats = stats;
        _notify = notify;
    }

    // GET api/teacher/classes/5/attendance/default  -> buổi mặc định cần điểm danh (buổi gần nhất)
    [HttpGet("classes/{classId}/attendance/default")]
    public async Task<IActionResult> DefaultSession(int classId)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");
        await _sessions.SyncAsync(classId);
        var s = await _sessions.DefaultSessionAsync(classId);
        if (s == null) return Error(404, "Lớp chưa có buổi học nào");
        return Ok(new { sessionId = s.Id, s.SessionNo, s.Date, s.StartTime, s.EndTime });
    }

    // GET api/teacher/sessions/15/attendance  -> bảng điểm danh của buổi
    [HttpGet("sessions/{id}/attendance")]
    public async Task<IActionResult> GetSheet(int id)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");
        return Ok(await BuildSheetAsync(s));
    }

    private async Task<object> BuildSheetAsync(ClassSession s)
    {
        var now = AppTime.Now;

        var enrollments = await Db.Enrollments
            .Where(e => e.ClassId == s.ClassId)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new { e.StudentId, e.Student.Code, e.Student.FullName, e.Status })
            .ToListAsync();

        var marks = await Db.Attendances.Where(a => a.SessionId == s.Id).ToListAsync();

        // Đơn xin nghỉ của học viên đã được duyệt cho buổi này -> gợi ý "Vắng có phép"
        var excusedIds = await Db.StudentRequests
            .Where(r => r.Type == "Leave" && r.Status == "Approved" && r.SessionId == s.Id)
            .Select(r => r.StudentId)
            .ToListAsync();

        var stats = await _stats.AttendanceAsync(new[] { s.ClassId });

        var students = enrollments
            .Where(e => e.Status == "Active" || marks.Any(m => m.StudentId == e.StudentId))
            .Select(e =>
            {
                var mark = marks.FirstOrDefault(m => m.StudentId == e.StudentId);
                stats.TryGetValue((s.ClassId, e.StudentId), out var st);
                var approvedLeave = excusedIds.Contains(e.StudentId);
                return new
                {
                    e.StudentId, e.Code, e.FullName,
                    enrollmentStatus = e.Status,
                    requiresAttendance = e.Status == "Active",
                    status = mark?.Status ?? (approvedLeave ? "Excused" : null),
                    note = mark?.Note,
                    isMarked = mark != null,
                    autoExcused = mark?.IsAuto ?? approvedLeave,
                    hasApprovedLeave = approvedLeave,
                    attendanceRate = st?.AttendanceRate,
                    absenceRate = st?.AbsenceRate,
                    absenceWarning = st?.Warning ?? false
                };
            })
            .ToList();

        var notRequired = enrollments
            .Where(e => e.Status != "Active" && !marks.Any(m => m.StudentId == e.StudentId))
            .Select(e => new { e.StudentId, e.Code, e.FullName, enrollmentStatus = e.Status });

        return new
        {
            session = new
            {
                s.Id, s.ClassId, ClassName = s.Class.Name, s.SessionNo, s.Date, s.StartTime, s.EndTime, s.Type,
                status = SessionService.StatusOf(s, now)
            },
            taken = s.AttendanceTakenAt != null,
            takenAt = s.AttendanceTakenAt,
            canTakeYet = SessionService.CanTakeAttendanceYet(s, now),
            locked = SessionService.IsAttendanceLocked(s, now),
            lockAt = SessionService.AttendanceLockAt(s),
            unlockedUntil = s.AttendanceUnlockedUntil,
            pendingUnlockRequest = await Db.TeacherRequests.AnyAsync(r =>
                r.SessionId == s.Id && r.Type == "AttendanceUnlock" && r.Status == "Pending"),
            summary = new
            {
                total = students.Count(x => x.requiresAttendance),
                present = students.Count(x => x.status == "Present"),
                late = students.Count(x => x.status == "Late"),
                absent = students.Count(x => x.status == "Absent"),
                excused = students.Count(x => x.status == "Excused"),
                unmarked = students.Count(x => x.requiresAttendance && x.status == null)
            },
            students,
            notRequired
        };
    }

    // PUT api/teacher/sessions/15/attendance
    // { "items": [ { "studentId": 1, "status": "Present" }, { "studentId": 2, "status": "Late", "note": "Muộn 10 phút" } ] }
    // Nút "Tất cả có mặt" ở frontend chỉ cần gửi mọi học viên với status = Present
    [HttpPut("sessions/{id}/attendance")]
    public async Task<IActionResult> Save(int id, AttendanceSaveRequest req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");

        var now = AppTime.Now;
        if (s.Status == "Cancelled") return Error(409, "Buổi học đã bị hủy, không cần điểm danh");
        if (!SessionService.CanTakeAttendanceYet(s, now)) return Error(409, "Chưa đến giờ học, chưa thể điểm danh");
        if (SessionService.IsAttendanceLocked(s, now))
            return Error(423, $"Điểm danh đã khóa sau {SessionService.AttendanceLockHours} giờ. Hãy gửi yêu cầu Admin mở lại");

        if (req.Items.GroupBy(x => x.StudentId).Any(g => g.Count() > 1))
            return Error(400, "Một học viên xuất hiện nhiều lần trong danh sách");

        var enrollments = await Db.Enrollments
            .Where(e => e.ClassId == s.ClassId)
            .Select(e => new { e.StudentId, e.Status, e.Student.FullName })
            .ToListAsync();

        var existing = await Db.Attendances.Where(a => a.SessionId == id).ToListAsync();

        // Được điểm danh: học viên đang học, hoặc học viên đã có điểm danh trước khi bảo lưu / nghỉ
        var allowed = enrollments
            .Where(e => e.Status == "Active" || existing.Any(a => a.StudentId == e.StudentId))
            .Select(e => e.StudentId).ToHashSet();
        var invalid = req.Items.Where(i => !allowed.Contains(i.StudentId)).Select(i => i.StudentId).ToList();
        if (invalid.Count > 0)
            return Error(400, $"Học viên không thuộc danh sách điểm danh của lớp (id: {string.Join(", ", invalid)})");

        // Chặn lưu nếu còn học viên chưa điểm danh (học viên bảo lưu / nghỉ không cần)
        var missing = enrollments
            .Where(e => e.Status == "Active" && !req.Items.Any(i => i.StudentId == e.StudentId))
            .Select(e => e.FullName).ToList();
        if (missing.Count > 0)
            return Error(400, $"Còn {missing.Count} học viên chưa điểm danh: {string.Join(", ", missing)}");

        var before = await _stats.AttendanceAsync(new[] { s.ClassId });

        foreach (var item in req.Items)
        {
            var a = existing.FirstOrDefault(x => x.StudentId == item.StudentId);
            if (a == null)
            {
                Db.Attendances.Add(new Attendance
                {
                    SessionId = id,
                    StudentId = item.StudentId,
                    Status = item.Status,
                    Note = item.Note?.Trim(),
                    MarkedAt = now
                });
            }
            else if (a.Status != item.Status || a.Note != item.Note?.Trim())
            {
                a.Status = item.Status;
                a.Note = item.Note?.Trim();
                a.IsAuto = false;
                a.MarkedAt = now;
            }
        }

        s.AttendanceTakenAt ??= now;
        await Db.SaveChangesAsync();

        // Cảnh báo học viên vừa vượt ngưỡng vắng 20%
        var after = await _stats.AttendanceAsync(new[] { s.ClassId });
        var newWarnings = after.Values
            .Where(x => x.Warning && !(before.TryGetValue((x.ClassId, x.StudentId), out var b) && b.Warning))
            .Select(x => new
            {
                x.StudentId,
                fullName = enrollments.FirstOrDefault(e => e.StudentId == x.StudentId)?.FullName,
                x.AbsenceRate
            })
            .ToList();

        return Ok(new
        {
            message = "Đã lưu điểm danh",
            warnings = newWarnings,
            sheet = await BuildSheetAsync(s)
        });
    }

    // POST api/teacher/sessions/15/attendance/unlock-request  { "reason": "Nhập nhầm trạng thái của 2 học viên" }
    [HttpPost("sessions/{id}/attendance/unlock-request")]
    public async Task<IActionResult> RequestUnlock(int id, ReasonRequest req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");
        if (!SessionService.IsAttendanceLocked(s, AppTime.Now)) return Error(409, "Điểm danh chưa bị khóa, có thể sửa trực tiếp");

        if (await Db.TeacherRequests.AnyAsync(r => r.SessionId == id && r.Type == "AttendanceUnlock" && r.Status == "Pending"))
            return Error(409, "Đã có yêu cầu mở lại đang chờ duyệt");

        var r = new TeacherRequest
        {
            TeacherId = TeacherId,
            Type = "AttendanceUnlock",
            SessionId = id,
            Reason = req.Reason.Trim(),
            Status = "Pending",
            CreatedAt = AppTime.Now
        };
        Db.TeacherRequests.Add(r);

        var adminIds = await Db.Users.Where(u => u.Role == "admin" && !u.IsLocked).Select(u => u.Id).ToListAsync();
        _notify.Add(adminIds, "Request", $"{TeacherName} xin mở lại điểm danh",
            $"Lớp {s.Class.Name}, buổi {s.SessionNo} ngày {s.Date:dd/MM/yyyy}. Lý do: {r.Reason}",
            "/admin/teacher-requests", UserId);

        await Db.SaveChangesAsync();
        return StatusCode(201, new { r.Id, r.Status, message = "Đã gửi yêu cầu mở lại điểm danh" });
    }

    // GET api/teacher/classes/5/attendance  -> bảng tổng hợp chuyên cần cả lớp (mỗi học viên một dòng, mỗi buổi một cột)
    [HttpGet("classes/{classId}/attendance")]
    public async Task<IActionResult> ClassSummary(int classId)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");
        await _sessions.SyncAsync(classId);

        var sessions = await Db.ClassSessions
            .Where(s => s.ClassId == classId && s.Status == "Scheduled" && s.AttendanceTakenAt != null)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new { s.Id, s.SessionNo, s.Date })
            .ToListAsync();

        var marks = await Db.Attendances
            .Where(a => a.Session.ClassId == classId)
            .Select(a => new { a.SessionId, a.StudentId, a.Status })
            .ToListAsync();

        var students = await Db.Enrollments
            .Where(e => e.ClassId == classId)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new { e.StudentId, e.Student.Code, e.Student.FullName, e.Status })
            .ToListAsync();

        var stats = await _stats.AttendanceAsync(new[] { classId });

        return Ok(new
        {
            warningThresholdPercent = StatsService.AbsenceWarningPercent,
            sessions,
            students = students.Select(st =>
            {
                stats.TryGetValue((classId, st.StudentId), out var a);
                return new
                {
                    st.StudentId, st.Code, st.FullName, enrollmentStatus = st.Status,
                    marks = sessions.Select(s => new
                    {
                        sessionId = s.Id,
                        status = marks.FirstOrDefault(m => m.SessionId == s.Id && m.StudentId == st.StudentId)?.Status
                    }),
                    present = a?.Present ?? 0,
                    late = a?.Late ?? 0,
                    absent = a?.Absent ?? 0,
                    excused = a?.Excused ?? 0,
                    attendanceRate = a?.AttendanceRate,
                    absenceRate = a?.AbsenceRate,
                    consecutiveAbsences = a?.ConsecutiveAbsences ?? 0,
                    warning = a?.Warning ?? false
                };
            })
        });
    }
}
