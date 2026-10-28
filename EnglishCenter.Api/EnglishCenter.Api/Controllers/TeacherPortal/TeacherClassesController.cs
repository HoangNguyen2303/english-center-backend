using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 3 - Lớp của tôi. Lớp do Admin tạo và phân công, giáo viên không tạo / xóa lớp.
[Route("api/teacher/classes")]
public class TeacherClassesController : TeacherControllerBase
{
    private readonly SessionService _sessions;
    private readonly StatsService _stats;

    public TeacherClassesController(AppDbContext db, SessionService sessions, StatsService stats) : base(db)
    {
        _sessions = sessions;
        _stats = stats;
    }

    // GET api/teacher/classes?status=Upcoming|Ongoing|Finished
    [HttpGet]
    public async Task<IActionResult> GetAll(string? status)
    {
        var today = AppTime.Today;
        var query = MyClasses;

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.Trim().ToLower())
            {
                case "upcoming": query = query.Where(c => today < c.StartDate); break;
                case "ongoing": query = query.Where(c => c.StartDate <= today && today <= c.EndDate); break;
                case "finished": query = query.Where(c => today > c.EndDate); break;
                default: return Error(400, "status phải là Upcoming, Ongoing hoặc Finished");
            }
        }

        await _sessions.SyncTeacherAsync(TeacherId);

        var classes = await query
            .OrderByDescending(c => c.StartDate).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id, c.Name, c.CourseId, CourseName = c.Course.Name, c.StartDate, c.EndDate,
                c.MaxStudents, c.MeetingUrl, c.Course.TotalSessions,
                EnrolledCount = c.Enrollments.Count(e => e.Status == "Active"),
                Schedules = c.Schedules.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                    .Select(s => new { s.DayOfWeek, s.StartTime, s.EndTime, RoomName = s.Room.Name }).ToList()
            })
            .ToListAsync();

        var progress = await ProgressAsync(classes.Select(c => c.Id).ToList());

        return Ok(classes.Select(c => new
        {
            c.Id, c.Name, c.CourseId, c.CourseName, c.StartDate, c.EndDate,
            c.EnrolledCount, c.MaxStudents, c.MeetingUrl, c.Schedules,
            c.TotalSessions,
            doneSessions = progress.GetValueOrDefault(c.Id).Done,
            taughtLessons = progress.GetValueOrDefault(c.Id).Taught,
            progressPercent = c.TotalSessions == 0 ? 0
                : Math.Min(100, Math.Round(progress.GetValueOrDefault(c.Id).Done * 100m / c.TotalSessions, 0)),
            status = ClassStatus(c.StartDate, c.EndDate, today)
        }));
    }

    // Số buổi đã dạy xong và số buổi đã đánh dấu "đã dạy" theo giáo án
    private async Task<Dictionary<int, (int Done, int Taught)>> ProgressAsync(List<int> classIds)
    {
        var now = AppTime.Now;
        var today = AppTime.Today;
        var nowTime = TimeOnly.FromDateTime(now);

        var rows = await Db.ClassSessions
            .Where(s => classIds.Contains(s.ClassId) && s.Status == "Scheduled")
            .GroupBy(s => s.ClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Done = g.Count(s => s.Date < today || (s.Date == today && s.EndTime <= nowTime)),
                Taught = g.Count(s => s.IsTaught)
            })
            .ToListAsync();
        return rows.ToDictionary(r => r.ClassId, r => (r.Done, r.Taught));
    }

    // GET api/teacher/classes/5 (chi tiết lớp: thông tin, lịch học, học viên)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await OwnsClassAsync(id)) return Error(404, "Không tìm thấy lớp học");
        await _sessions.SyncAsync(id);

        var today = AppTime.Today;
        var c = await Db.Classes.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Name, x.CourseId, CourseName = x.Course.Name, x.Course.TotalSessions,
                x.Course.BandFrom, x.Course.BandTo,
                x.StartDate, x.EndDate, x.MaxStudents, x.MeetingUrl,
                EnrolledCount = x.Enrollments.Count(e => e.Status == "Active"),
                Schedules = x.Schedules.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                    .Select(s => new { s.Id, s.DayOfWeek, s.StartTime, s.EndTime, s.RoomId, RoomName = s.Room.Name }).ToList()
            })
            .FirstAsync();

        var students = await Db.Enrollments
            .Where(e => e.ClassId == id)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new
            {
                e.StudentId, e.Student.Code, e.Student.FullName, e.Student.Phone,
                Email = e.Student.User.Email, e.Student.TargetBand,
                Status = e.Status,                 // Active | OnHold (bảo lưu) | Dropped (nghỉ)
                StudentStatus = e.Student.Status
            })
            .ToListAsync();

        var attendance = await _stats.AttendanceAsync(new[] { id });
        var progress = (await ProgressAsync(new List<int> { id })).GetValueOrDefault(id);
        var defaultSession = await _sessions.DefaultSessionAsync(id);
        var lessonCount = await Db.SyllabusLessons.CountAsync(l => l.CourseId == c.CourseId);

        return Ok(new
        {
            c.Id, c.Name, c.CourseId, c.CourseName, c.BandFrom, c.BandTo,
            c.StartDate, c.EndDate, c.MaxStudents, c.EnrolledCount, c.MeetingUrl, c.Schedules,
            status = ClassStatus(c.StartDate, c.EndDate, today),
            c.TotalSessions,
            doneSessions = progress.Done,
            taughtLessons = progress.Taught,
            syllabusLessons = lessonCount,
            progressPercent = c.TotalSessions == 0 ? 0 : Math.Min(100, Math.Round(progress.Done * 100m / c.TotalSessions, 0)),
            defaultSessionId = defaultSession?.Id,
            students = students.Select(s =>
            {
                attendance.TryGetValue((id, s.StudentId), out var a);
                return new
                {
                    s.StudentId, s.Code, s.FullName, s.Phone, s.Email, s.TargetBand, s.Status, s.StudentStatus,
                    attendanceRate = a?.AttendanceRate,
                    absenceWarning = a?.Warning ?? false
                };
            })
        });
    }

    // GET api/teacher/classes/5/sessions (danh sách buổi học của lớp)
    [HttpGet("{id}/sessions")]
    public async Task<IActionResult> GetSessions(int id)
    {
        if (!await OwnsClassAsync(id)) return Error(404, "Không tìm thấy lớp học");
        await _sessions.SyncAsync(id);

        var now = AppTime.Now;
        var rows = await Db.ClassSessions
            .Where(s => s.ClassId == id)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id, s.SessionNo, s.Date, s.StartTime, s.EndTime, s.Type, s.Status,
                RoomName = s.Room != null ? s.Room.Name : null,
                s.SyllabusLessonId, LessonTitle = s.SyllabusLesson != null ? s.SyllabusLesson.Title : null,
                s.IsTaught, s.AttendanceTakenAt, s.AttendanceUnlockedUntil,
                MaterialCount = s.Materials.Count,
                HasLog = s.ContentTaught != null || s.Homework != null
            })
            .ToListAsync();

        var defaultSession = await _sessions.DefaultSessionAsync(id);
        return Ok(new
        {
            defaultSessionId = defaultSession?.Id,
            sessions = rows.Select(s => new
            {
                s.Id, s.SessionNo, s.Date, s.StartTime, s.EndTime, s.Type, s.RoomName,
                s.SyllabusLessonId, s.LessonTitle, s.IsTaught, s.MaterialCount, s.HasLog,
                attendanceTaken = s.AttendanceTakenAt != null,
                status = s.Status == "Cancelled" ? "Cancelled" : SessionService.StatusOf(s.Date, s.StartTime, s.EndTime, now)
            })
        });
    }

    // PUT api/teacher/classes/5/meeting-url (link Zoom / Google Meet của cả lớp)
    [HttpPut("{id}/meeting-url")]
    public async Task<IActionResult> UpdateMeetingUrl(int id, MeetingUrlRequest req)
    {
        var cls = await MyClasses.FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return Error(404, "Không tìm thấy lớp học");

        cls.MeetingUrl = string.IsNullOrWhiteSpace(req.MeetingUrl) ? null : req.MeetingUrl.Trim();
        await Db.SaveChangesAsync();
        return Ok(new { cls.Id, cls.MeetingUrl });
    }

    // ---------- Giáo án theo syllabus ----------

    // GET api/teacher/classes/5/syllabus (danh sách bài của khóa + buổi nào đã gắn / đã dạy)
    [HttpGet("{id}/syllabus")]
    public async Task<IActionResult> GetSyllabus(int id)
    {
        var cls = await MyClasses.FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return Error(404, "Không tìm thấy lớp học");

        var lessons = await Db.SyllabusLessons
            .Where(l => l.CourseId == cls.CourseId)
            .OrderBy(l => l.OrderNo).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.OrderNo, l.Title, l.Description })
            .ToListAsync();

        var links = await Db.ClassSessions
            .Where(s => s.ClassId == id && s.SyllabusLessonId != null)
            .Select(s => new { LessonId = s.SyllabusLessonId!.Value, SessionId = s.Id, s.SessionNo, s.Date, s.IsTaught })
            .ToListAsync();

        var total = lessons.Count;
        var taught = lessons.Count(l => links.Any(x => x.LessonId == l.Id && x.IsTaught));

        return Ok(new
        {
            courseId = cls.CourseId,
            totalLessons = total,
            taughtLessons = taught,
            completionPercent = total == 0 ? 0 : Math.Round(taught * 100m / total, 0),
            lessons = lessons.Select(l => new
            {
                l.Id, l.OrderNo, l.Title, l.Description,
                sessions = links.Where(x => x.LessonId == l.Id)
                    .Select(x => new { x.SessionId, x.SessionNo, x.Date, x.IsTaught }),
                isTaught = links.Any(x => x.LessonId == l.Id && x.IsTaught)
            })
        });
    }

    // POST api/teacher/classes/5/syllabus (thêm bài vào syllabus của khóa)
    [HttpPost("{id}/syllabus")]
    public async Task<IActionResult> AddLesson(int id, SyllabusLessonRequest req)
    {
        var cls = await MyClasses.FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return Error(404, "Không tìm thấy lớp học");

        var lessons = await Db.SyllabusLessons.Where(l => l.CourseId == cls.CourseId).ToListAsync();
        var max = lessons.Count == 0 ? 0 : lessons.Max(l => l.OrderNo);
        var order = req.OrderNo == null || req.OrderNo > max ? max + 1 : req.OrderNo.Value;

        foreach (var l in lessons.Where(l => l.OrderNo >= order))
            l.OrderNo++;

        var lesson = new SyllabusLesson
        {
            CourseId = cls.CourseId,
            OrderNo = order,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim()
        };
        Db.SyllabusLessons.Add(lesson);
        await Db.SaveChangesAsync();
        return StatusCode(201, new { lesson.Id, lesson.OrderNo, lesson.Title, lesson.Description });
    }

    // PUT api/teacher/classes/5/syllabus/12
    [HttpPut("{id}/syllabus/{lessonId}")]
    public async Task<IActionResult> UpdateLesson(int id, int lessonId, SyllabusLessonRequest req)
    {
        var cls = await MyClasses.FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return Error(404, "Không tìm thấy lớp học");

        var lessons = await Db.SyllabusLessons.Where(l => l.CourseId == cls.CourseId).ToListAsync();
        var lesson = lessons.FirstOrDefault(l => l.Id == lessonId);
        if (lesson == null) return Error(404, "Không tìm thấy bài trong syllabus");

        if (req.OrderNo != null && req.OrderNo != lesson.OrderNo)
        {
            // Đổi vị trí: dời các bài ở giữa
            var target = Math.Min(req.OrderNo.Value, lessons.Max(l => l.OrderNo));
            if (target < lesson.OrderNo)
                foreach (var l in lessons.Where(l => l.OrderNo >= target && l.OrderNo < lesson.OrderNo)) l.OrderNo++;
            else
                foreach (var l in lessons.Where(l => l.OrderNo <= target && l.OrderNo > lesson.OrderNo)) l.OrderNo--;
            lesson.OrderNo = target;
        }
        lesson.Title = req.Title.Trim();
        lesson.Description = req.Description?.Trim();
        await Db.SaveChangesAsync();
        return Ok(new { lesson.Id, lesson.OrderNo, lesson.Title, lesson.Description });
    }

    // DELETE api/teacher/classes/5/syllabus/12
    [HttpDelete("{id}/syllabus/{lessonId}")]
    public async Task<IActionResult> DeleteLesson(int id, int lessonId)
    {
        var cls = await MyClasses.FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null) return Error(404, "Không tìm thấy lớp học");

        var lessons = await Db.SyllabusLessons.Where(l => l.CourseId == cls.CourseId).ToListAsync();
        var lesson = lessons.FirstOrDefault(l => l.Id == lessonId);
        if (lesson == null) return Error(404, "Không tìm thấy bài trong syllabus");

        if (await Db.ClassSessions.AnyAsync(s => s.SyllabusLessonId == lessonId))
            return Error(409, "Bài đã được gắn với buổi học (có thể của lớp khác cùng khóa), hãy bỏ gắn trước");

        foreach (var l in lessons.Where(l => l.OrderNo > lesson.OrderNo)) l.OrderNo--;
        Db.SyllabusLessons.Remove(lesson);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
