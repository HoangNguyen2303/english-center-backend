using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Dtos.Common;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/classes")]
[Authorize(Roles = "admin")]
public class ClassesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ScheduleService _schedules;
    private readonly EnrollmentService _enrollments;
    private readonly NotificationService _notify;

    public ClassesController(AppDbContext db, ScheduleService schedules, EnrollmentService enrollments,
        NotificationService notify)
    {
        _db = db;
        _schedules = schedules;
        _enrollments = enrollments;
        _notify = notify;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    private static string StatusOf(DateOnly start, DateOnly end, DateOnly today)
    {
        if (today < start) return "Upcoming";
        if (today > end) return "Finished";
        return "Ongoing";
    }

    private static IQueryable<ClassResponse> Project(IQueryable<CourseClass> query)
    {
        return query.Select(c => new ClassResponse
        {
            Id = c.Id,
            Name = c.Name,
            CourseId = c.CourseId,
            CourseName = c.Course.Name,
            TeacherId = c.TeacherId,
            TeacherName = c.Teacher.FullName,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            MaxStudents = c.MaxStudents,
            MeetingUrl = c.MeetingUrl,
            EnrolledCount = c.Enrollments.Count(e => e.Status == "Active"),
            Schedules = c.Schedules
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .Select(s => new ClassScheduleItem
                {
                    Id = s.Id,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    RoomId = s.RoomId,
                    RoomName = s.Room.Name
                }).ToList()
        });
    }

    // Lấy một lớp kèm danh sách học viên (trang chi tiết)
    private async Task<ClassResponse?> BuildDetail(int id)
    {
        var cls = await Project(_db.Classes.Where(c => c.Id == id)).FirstOrDefaultAsync();
        if (cls == null) return null;

        cls.Status = StatusOf(cls.StartDate, cls.EndDate, Today());

        cls.Students = await _db.Enrollments
            .Where(e => e.ClassId == id)
            .OrderBy(e => e.Student.FullName)
            .Select(e => new ClassStudentItem
            {
                EnrollmentId = e.Id,
                StudentId = e.StudentId,
                Code = e.Student.Code,
                FullName = e.Student.FullName,
                Phone = e.Student.Phone,
                Status = e.Status,
                FinalTuitionFee = e.FinalTuitionFee,
                IsPaid = e.Payment != null
            })
            .ToListAsync();

        return cls;
    }

    // GET api/admin/classes?courseId=&teacherId=&status=&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(int? courseId, int? teacherId, string? status,
        int page = 1, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var today = Today();
        var query = _db.Classes.AsQueryable();

        if (courseId != null)
            query = query.Where(c => c.CourseId == courseId);
        if (teacherId != null)
            query = query.Where(c => c.TeacherId == teacherId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.Trim().ToLower())
            {
                case "upcoming":
                    query = query.Where(c => today < c.StartDate);
                    break;
                case "ongoing":
                    query = query.Where(c => c.StartDate <= today && today <= c.EndDate);
                    break;
                case "finished":
                    query = query.Where(c => today > c.EndDate);
                    break;
                default:
                    return BadRequest(new { message = "status phải là Upcoming, Ongoing hoặc Finished" });
            }
        }

        var total = await query.CountAsync();

        var items = await Project(query.OrderByDescending(c => c.StartDate).ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync();

        foreach (var item in items)
            item.Status = StatusOf(item.StartDate, item.EndDate, today);

        return Ok(new PagedResult<ClassResponse>
        {
            Items = items,
            TotalItems = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET api/admin/classes/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cls = await BuildDetail(id);
        if (cls == null)
            return NotFound(new { message = "Không tìm thấy lớp học" });

        return Ok(cls);
    }

    // POST api/admin/classes
    [HttpPost]
    public async Task<IActionResult> Create(ClassRequest req)
    {
        var course = await _db.Courses.FindAsync(req.CourseId);
        if (course == null)
            return BadRequest(new { message = "Khóa học không tồn tại" });

        if (!await _db.Teachers.AnyAsync(t => t.Id == req.TeacherId))
            return BadRequest(new { message = "Giáo viên không tồn tại" });

        var check = await _schedules.CheckAsync(null, req, course);
        if (check.Error != null)
            return StatusCode(check.StatusCode, new { message = check.Error });

        var cls = new CourseClass
        {
            Name = req.Name.Trim(),
            CourseId = req.CourseId,
            TeacherId = req.TeacherId,
            StartDate = req.StartDate,
            EndDate = check.EndDate,
            MaxStudents = req.MaxStudents,
            MeetingUrl = req.MeetingUrl?.Trim(),
            Schedules = req.Schedules.Select(s => new ClassSchedule
            {
                RoomId = s.RoomId,
                DayOfWeek = (byte)s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList()
        };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync();

        var result = await BuildDetail(cls.Id);
        return CreatedAtAction(nameof(GetById), new { id = cls.Id }, result);
    }

    // PUT api/admin/classes/5 (thay toàn bộ lịch học bằng danh sách mới)
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ClassRequest req)
    {
        var cls = await _db.Classes.Include(c => c.Schedules).FirstOrDefaultAsync(c => c.Id == id);
        if (cls == null)
            return NotFound(new { message = "Không tìm thấy lớp học" });

        var course = await _db.Courses.FindAsync(req.CourseId);
        if (course == null)
            return BadRequest(new { message = "Khóa học không tồn tại" });

        if (!await _db.Teachers.AnyAsync(t => t.Id == req.TeacherId))
            return BadRequest(new { message = "Giáo viên không tồn tại" });

        if (cls.CourseId != req.CourseId && await _db.Enrollments.AnyAsync(e => e.ClassId == id))
            return Conflict(new { message = "Lớp đã có học viên ghi danh, không thể đổi khóa học" });

        var activeCount = await _db.Enrollments.CountAsync(e => e.ClassId == id && e.Status == "Active");
        if (req.MaxStudents < activeCount)
            return Conflict(new { message = $"Lớp đang có {activeCount} học viên, sĩ số tối đa không được nhỏ hơn" });

        var check = await _schedules.CheckAsync(id, req, course);
        if (check.Error != null)
            return StatusCode(check.StatusCode, new { message = check.Error });

        // Xóa lịch cũ rồi thêm lịch mới, làm trong một transaction
        await using var tx = await _db.Database.BeginTransactionAsync();

        var oldTeacherId = cls.TeacherId;
        cls.Name = req.Name.Trim();
        cls.CourseId = req.CourseId;
        cls.TeacherId = req.TeacherId;
        cls.StartDate = req.StartDate;
        cls.EndDate = check.EndDate;
        cls.MaxStudents = req.MaxStudents;
        cls.MeetingUrl = req.MeetingUrl?.Trim();

        _db.ClassSchedules.RemoveRange(cls.Schedules.ToList());
        await _db.SaveChangesAsync();

        _db.ClassSchedules.AddRange(req.Schedules.Select(s => new ClassSchedule
        {
            ClassId = id,
            RoomId = s.RoomId,
            DayOfWeek = (byte)s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime
        }));
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        // Chuông thông báo cho giáo viên: lịch lớp bị đổi (buổi học cụ thể tự đồng bộ khi giáo viên mở lịch)
        var teacherUserIds = await _db.Teachers
            .Where(t => t.Id == cls.TeacherId || t.Id == oldTeacherId)
            .Select(t => t.UserId).ToListAsync();
        _notify.Add(teacherUserIds, "ScheduleChange", $"Lớp {cls.Name} vừa được cập nhật lịch học / phân công",
            null, $"/teacher/classes/{id}");
        await _db.SaveChangesAsync();

        var result = await BuildDetail(id);
        return Ok(result);
    }

    // DELETE api/admin/classes/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cls = await _db.Classes.FindAsync(id);
        if (cls == null)
            return NotFound(new { message = "Không tìm thấy lớp học" });

        if (await _db.Enrollments.AnyAsync(e => e.ClassId == id))
            return Conflict(new { message = "Lớp đã có học viên ghi danh, không thể xóa" });

        _db.Classes.Remove(cls);   // lịch học của lớp tự xóa theo (cascade)
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // POST api/admin/classes/5/students  { "studentId": 2 }
    [HttpPost("{id}/students")]
    public async Task<IActionResult> AddStudent(int id, AddStudentRequest req)
    {
        var result = await _enrollments.EnrollAsync(req.StudentId, id);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { message = result.Message });

        var detail = await BuildDetail(id);
        return StatusCode(StatusCodes.Status201Created, detail);
    }

    // DELETE api/admin/classes/5/students/12  (12 = id ghi danh)
    [HttpDelete("{id}/students/{enrollmentId}")]
    public async Task<IActionResult> RemoveStudent(int id, int enrollmentId)
    {
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.ClassId == id);
        if (enrollment == null)
            return NotFound(new { message = "Không tìm thấy ghi danh trong lớp này" });

        var result = await _enrollments.WithdrawAsync(enrollment.StudentId, enrollmentId);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new { message = result.Message });
    }
}