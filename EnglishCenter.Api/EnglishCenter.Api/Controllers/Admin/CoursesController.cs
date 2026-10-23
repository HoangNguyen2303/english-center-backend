using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnglishCenter.Api.Dtos.Common;

namespace EnglishCenter.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "admin")]
public class CoursesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CoursesController(AppDbContext db)
    {
        _db = db;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    // Chuyển Course -> CourseResponse, kèm số lớp và số học viên (tính ngay trong SQL)
    private static IQueryable<CourseResponse> Project(IQueryable<Course> query, DateOnly today)
    {
        return query.Select(c => new CourseResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            BandFrom = c.BandFrom,
            BandTo = c.BandTo,
            TotalSessions = c.TotalSessions,
            SessionsPerWeek = c.SessionsPerWeek,
            Tuition = c.Tuition,
            DiscountPercent = c.DiscountPercent,
            OpenClasses = c.Classes.Count(x => x.EndDate >= today),
            ActiveStudents = c.Classes
                .Where(x => x.EndDate >= today)
                .SelectMany(x => x.Enrollments)
                .Count(e => e.Status == "Active")
        });
    }

    // GET api/admin/courses?search=ielts&page=1&pageSize=16
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, int page = 1, int pageSize = 16)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 16;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Courses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(c => c.Name.Contains(kw));
        }

        var total = await query.CountAsync();

        var items = await Project(query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize), Today())
            .ToListAsync();

        return Ok(new PagedResult<CourseResponse>
        {
            Items = items,
            TotalItems = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET api/admin/courses/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var course = await Project(_db.Courses.Where(c => c.Id == id), Today()).FirstOrDefaultAsync();
        if (course == null)
            return NotFound(new { message = "Không tìm thấy khóa học" });

        return Ok(course);
    }

    // POST api/admin/courses
    [HttpPost]
    public async Task<IActionResult> Create(CourseRequest req)
    {
        var name = req.Name.Trim();

        if (await _db.Courses.AnyAsync(c => c.Name == name))
            return Conflict(new { message = "Tên khóa học đã tồn tại" });

        var course = new Course
        {
            Name = name,
            Description = req.Description?.Trim(),
            BandFrom = req.BandFrom,
            BandTo = req.BandTo,
            TotalSessions = req.TotalSessions,
            SessionsPerWeek = req.SessionsPerWeek,
            Tuition = req.Tuition,
            DiscountPercent = req.DiscountPercent
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var result = await Project(_db.Courses.Where(c => c.Id == course.Id), Today()).FirstAsync();
        return CreatedAtAction(nameof(GetById), new { id = course.Id }, result);
    }

    // PUT api/admin/courses/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CourseRequest req)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null)
            return NotFound(new { message = "Không tìm thấy khóa học" });

        var name = req.Name.Trim();

        if (await _db.Courses.AnyAsync(c => c.Name == name && c.Id != id))
            return Conflict(new { message = "Tên khóa học đã tồn tại" });

        // Đổi học phí / giảm giá chỉ ảnh hưởng ghi danh MỚI,
        // vì các ghi danh cũ đã lưu snapshot giá trong bảng Enrollments.
        course.Name = name;
        course.Description = req.Description?.Trim();
        course.BandFrom = req.BandFrom;
        course.BandTo = req.BandTo;
        course.TotalSessions = req.TotalSessions;
        course.SessionsPerWeek = req.SessionsPerWeek;
        course.Tuition = req.Tuition;
        course.DiscountPercent = req.DiscountPercent;
        await _db.SaveChangesAsync();

        var result = await Project(_db.Courses.Where(c => c.Id == id), Today()).FirstAsync();
        return Ok(result);
    }

    // DELETE api/admin/courses/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null)
            return NotFound(new { message = "Không tìm thấy khóa học" });

        // Luật: khóa đã có lớp (kể cả lớp đã kết thúc) thì không được xóa
        if (await _db.Classes.AnyAsync(c => c.CourseId == id))
            return Conflict(new { message = "Khóa học đã có lớp, không thể xóa" });

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}