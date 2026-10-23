using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Dtos.Common;
using EnglishCenter.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/teachers")]
[Authorize(Roles = "admin")]
public class TeachersController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeachersController(AppDbContext db)
    {
        _db = db;
    }

    private static IQueryable<TeacherResponse> Project(IQueryable<Teacher> query)
    {
        return query.Select(t => new TeacherResponse
        {
            Id = t.Id,
            UserId = t.UserId,
            FullName = t.FullName,
            Phone = t.Phone,
            Email = t.User.Email,
            Specialty = t.Specialty,
            Degree = t.Degree,
            HireDate = t.HireDate,
            Status = t.Status,
            IsLocked = t.User.IsLocked,
            ClassCount = t.Classes.Count()
        });
    }

    // Trạng thái lớp tính từ ngày, không lưu trong database
    private static string ClassStatus(DateOnly start, DateOnly end, DateOnly today)
    {
        if (today < start) return "Upcoming";
        if (today > end) return "Finished";
        return "Ongoing";
    }

    private Task<Teacher?> GetWithUser(int id)
    {
        return _db.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
    }

    // GET api/admin/teachers?search=&specialty=&status=&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? specialty, string? status,
        int page = 1, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Teachers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(t => t.FullName.Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(t => t.Specialty == specialty);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        var total = await query.CountAsync();

        var items = await Project(query.OrderBy(t => t.FullName).ThenBy(t => t.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync();

        return Ok(new PagedResult<TeacherResponse>
        {
            Items = items,
            TotalItems = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET api/admin/teachers/5 (chi tiết, kèm danh sách lớp phụ trách)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var teacher = await Project(_db.Teachers.Where(t => t.Id == id)).FirstOrDefaultAsync();
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        var classes = await _db.Classes
            .Where(c => c.TeacherId == id)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new { c.Id, c.Name, CourseName = c.Course.Name, c.StartDate, c.EndDate })
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        teacher.Classes = classes.Select(c => new TeacherClassItem
        {
            Id = c.Id,
            Name = c.Name,
            CourseName = c.CourseName,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = ClassStatus(c.StartDate, c.EndDate, today)
        }).ToList();

        return Ok(teacher);
    }

    // POST api/admin/teachers
    [HttpPost]
    public async Task<IActionResult> Create(TeacherCreateRequest req)
    {
        var email = req.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email đã được sử dụng" });

        // Tạo User và Teacher trong CÙNG MỘT lần SaveChanges
        // -> EF tự bọc trong 1 transaction: hoặc cả hai được lưu, hoặc không cái nào.
        var teacher = new Teacher
        {
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            Specialty = req.Specialty.Trim(),
            Degree = req.Degree?.Trim(),
            HireDate = req.HireDate,
            Status = req.Status,
            User = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.TempPassword),
                Role = "teacher"
            }
        };
        _db.Teachers.Add(teacher);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Phòng trường hợp 2 người tạo cùng email gần như cùng lúc (unique index chặn)
            return Conflict(new { message = "Email đã được sử dụng" });
        }

        var result = await Project(_db.Teachers.Where(t => t.Id == teacher.Id)).FirstAsync();
        return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, result);
    }

    // PUT api/admin/teachers/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, TeacherUpdateRequest req)
    {
        var teacher = await _db.Teachers.FindAsync(id);
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        teacher.FullName = req.FullName.Trim();
        teacher.Phone = req.Phone.Trim();
        teacher.Specialty = req.Specialty.Trim();
        teacher.Degree = req.Degree?.Trim();
        teacher.HireDate = req.HireDate;
        teacher.Status = req.Status;
        await _db.SaveChangesAsync();

        var result = await Project(_db.Teachers.Where(t => t.Id == id)).FirstAsync();
        return Ok(result);
    }

    // DELETE api/admin/teachers/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var teacher = await GetWithUser(id);
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        if (await _db.Classes.AnyAsync(c => c.TeacherId == id))
            return Conflict(new { message = "Giáo viên đang phụ trách lớp, không thể xóa. Hãy chuyển trạng thái sang Tạm nghỉ (OnLeave)" });

        // Xóa luôn tài khoản đăng nhập
        _db.Teachers.Remove(teacher);
        _db.Users.Remove(teacher.User);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // POST api/admin/teachers/5/reset-password
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest req)
    {
        var teacher = await GetWithUser(id);
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        teacher.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        teacher.User.TokenVersion++;   // đá giáo viên ra khỏi các phiên đăng nhập cũ
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đặt lại mật khẩu thành công" });
    }

    // POST api/admin/teachers/5/lock
    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(int id)
    {
        var teacher = await GetWithUser(id);
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        teacher.User.IsLocked = true;
        teacher.User.TokenVersion++;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã khóa tài khoản" });
    }

    // POST api/admin/teachers/5/unlock
    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(int id)
    {
        var teacher = await GetWithUser(id);
        if (teacher == null)
            return NotFound(new { message = "Không tìm thấy giáo viên" });

        teacher.User.IsLocked = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã mở khóa tài khoản" });
    }
}