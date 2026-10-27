using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 3 (tiếp) - Chi tiết từng buổi học: giáo án, tài liệu, link online, nhật ký buổi học
[Route("api/teacher/sessions")]
public class SessionsController : TeacherControllerBase
{
    public SessionsController(AppDbContext db) : base(db) { }

    // GET api/teacher/sessions/15
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await Db.ClassSessions
            .Include(x => x.Class).ThenInclude(c => c.Course)
            .Include(x => x.Room)
            .Include(x => x.SyllabusLesson)
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (s == null) return Error(404, "Không tìm thấy buổi học");

        var now = AppTime.Now;
        var counts = await Db.Attendances.Where(a => a.SessionId == id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            s.Id, s.ClassId, ClassName = s.Class.Name, CourseName = s.Class.Course.Name,
            s.SessionNo, s.Date, s.StartTime, s.EndTime, s.Type, RoomName = s.Room?.Name,
            status = SessionService.StatusOf(s, now),
            lesson = s.SyllabusLesson == null ? null : new { s.SyllabusLesson.Id, s.SyllabusLesson.OrderNo, s.SyllabusLesson.Title },
            s.IsTaught, s.TaughtAt,
            meetingUrl = s.MeetingUrl,
            classMeetingUrl = s.Class.MeetingUrl,
            effectiveMeetingUrl = SessionService.EffectiveMeetingUrl(s, s.Class),
            s.ContentTaught, s.Homework,
            materials = s.Materials.OrderBy(m => m.CreatedAt).Select(m => new { m.Id, m.Title, m.Kind, m.Url, m.CreatedAt }),
            attendance = new
            {
                taken = s.AttendanceTakenAt != null,
                s.AttendanceTakenAt,
                locked = SessionService.IsAttendanceLocked(s, now),
                lockAt = SessionService.AttendanceLockAt(s),
                present = counts.FirstOrDefault(c => c.Status == "Present")?.Count ?? 0,
                late = counts.FirstOrDefault(c => c.Status == "Late")?.Count ?? 0,
                absent = counts.FirstOrDefault(c => c.Status == "Absent")?.Count ?? 0,
                excused = counts.FirstOrDefault(c => c.Status == "Excused")?.Count ?? 0
            }
        });
    }

    // PUT api/teacher/sessions/15/lesson  { "syllabusLessonId": 3, "isTaught": true }
    [HttpPut("{id}/lesson")]
    public async Task<IActionResult> UpdateLesson(int id, SessionLessonRequest req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");
        if (s.Status == "Cancelled") return Error(409, "Buổi học đã bị hủy");

        if (req.SyllabusLessonId != null &&
            !await Db.SyllabusLessons.AnyAsync(l => l.Id == req.SyllabusLessonId && l.CourseId == s.Class.CourseId))
            return Error(400, "Bài không thuộc syllabus của khóa học này");

        if (req.IsTaught && AppTime.At(s.Date, s.StartTime) > AppTime.Now)
            return Error(409, "Buổi học chưa diễn ra, chưa thể đánh dấu đã dạy");

        s.SyllabusLessonId = req.SyllabusLessonId;
        if (req.IsTaught && !s.IsTaught) s.TaughtAt = AppTime.Now;
        if (!req.IsTaught) s.TaughtAt = null;
        s.IsTaught = req.IsTaught;
        await Db.SaveChangesAsync();

        return Ok(new { s.Id, s.SyllabusLessonId, s.IsTaught, s.TaughtAt });
    }

    // PUT api/teacher/sessions/15/log  (nhật ký: nội dung đã dạy, bài tập về nhà)
    [HttpPut("{id}/log")]
    public async Task<IActionResult> UpdateLog(int id, SessionLogRequest req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");

        s.ContentTaught = string.IsNullOrWhiteSpace(req.ContentTaught) ? null : req.ContentTaught.Trim();
        s.Homework = string.IsNullOrWhiteSpace(req.Homework) ? null : req.Homework.Trim();
        await Db.SaveChangesAsync();

        return Ok(new { s.Id, s.ContentTaught, s.Homework });
    }

    // PUT api/teacher/sessions/15/meeting-url  (link online riêng cho buổi; để trống = dùng link của lớp)
    [HttpPut("{id}/meeting-url")]
    public async Task<IActionResult> UpdateMeetingUrl(int id, MeetingUrlRequest req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");

        s.MeetingUrl = string.IsNullOrWhiteSpace(req.MeetingUrl) ? null : req.MeetingUrl.Trim();
        await Db.SaveChangesAsync();

        return Ok(new { s.Id, s.MeetingUrl, effectiveMeetingUrl = SessionService.EffectiveMeetingUrl(s, s.Class) });
    }

    // POST api/teacher/sessions/15/materials
    // Upload file trước bằng POST api/teacher/files?folder=materials rồi gửi url vào đây; link video thì gửi thẳng
    [HttpPost("{id}/materials")]
    public async Task<IActionResult> AddMaterial(int id, MaterialCreate req)
    {
        var s = await MySessionAsync(id);
        if (s == null) return Error(404, "Không tìm thấy buổi học");

        var url = req.Url.Trim();
        if (!url.StartsWith("/uploads/") && !Uri.TryCreate(url, UriKind.Absolute, out _))
            return Error(400, "Đường dẫn tài liệu không hợp lệ");

        var m = new SessionMaterial
        {
            SessionId = id,
            Title = req.Title.Trim(),
            Kind = req.Kind,
            Url = url,
            CreatedAt = AppTime.Now
        };
        Db.SessionMaterials.Add(m);
        await Db.SaveChangesAsync();

        return StatusCode(201, new { m.Id, m.Title, m.Kind, m.Url, m.CreatedAt });
    }

    // DELETE api/teacher/sessions/15/materials/3
    [HttpDelete("{id}/materials/{materialId}")]
    public async Task<IActionResult> DeleteMaterial(int id, int materialId)
    {
        var m = await Db.SessionMaterials
            .FirstOrDefaultAsync(x => x.Id == materialId && x.SessionId == id && x.Session.Class.TeacherId == TeacherId);
        if (m == null) return Error(404, "Không tìm thấy tài liệu");

        Db.SessionMaterials.Remove(m);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
