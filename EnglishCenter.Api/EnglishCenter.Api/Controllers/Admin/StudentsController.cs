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
[Route("api/admin/students")]
[Authorize(Roles = "admin")]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EnrollmentService _enrollments;

    public StudentsController(AppDbContext db, EnrollmentService enrollments)
    {
        _db = db;
        _enrollments = enrollments;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    private static IQueryable<StudentResponse> Project(IQueryable<Student> query, DateOnly today)
    {
        return query.Select(s => new StudentResponse
        {
            Id = s.Id,
            UserId = s.UserId,
            Code = s.Code,
            FullName = s.FullName,
            DateOfBirth = s.DateOfBirth,
            Phone = s.Phone,
            Email = s.User.Email,
            Address = s.Address,
            ParentName = s.ParentName,
            ParentPhone = s.ParentPhone,
            Status = s.Status,
            IsLocked = s.User.IsLocked,
            CreatedAt = s.CreatedAt,
            CurrentClass = s.Enrollments
                .Where(e => e.Status == "Active" && e.Class.EndDate >= today)
                .OrderBy(e => e.Class.StartDate)
                .Select(e => e.Class.Name)
                .FirstOrDefault()
        });
    }

    private static string ClassStatus(DateOnly start, DateOnly end, DateOnly today)
    {
        if (today < start) return "Upcoming";
        if (today > end) return "Finished";
        return "Ongoing";
    }

    // Lấy chi tiết học viên kèm danh sách ghi danh và phiếu thu
    private async Task<StudentResponse?> BuildDetail(int id)
    {
        var today = Today();

        var student = await Project(_db.Students.Where(s => s.Id == id), today).FirstOrDefaultAsync();
        if (student == null) return null;

        var items = await _db.Enrollments
            .Where(e => e.StudentId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new StudentEnrollmentItem
            {
                Id = e.Id,
                ClassId = e.ClassId,
                ClassName = e.Class.Name,
                CourseName = e.Class.Course.Name,
                ClassStartDate = e.Class.StartDate,
                ClassEndDate = e.Class.EndDate,
                TuitionFee = e.TuitionFee,
                DiscountPercent = e.DiscountPercent,
                DiscountAmount = e.DiscountAmount,
                FinalTuitionFee = e.FinalTuitionFee,
                Status = e.Status,
                PaymentId = e.Payment != null ? (int?)e.Payment.Id : null,
                PaidDate = e.Payment != null ? (DateOnly?)e.Payment.PaidDate : null,
                PaymentMethod = e.Payment != null ? e.Payment.Method : null
            })
            .ToListAsync();

        foreach (var item in items)
            item.ClassStatus = ClassStatus(item.ClassStartDate, item.ClassEndDate, today);

        student.Enrollments = items;
        return student;
    }

    // Sinh mã học viên tiếp theo: HV0001, HV0002, ...
    private async Task<string> NextCode()
    {
        var last = await _db.Students
            .OrderByDescending(s => s.Code)
            .Select(s => s.Code)
            .FirstOrDefaultAsync();

        var number = 0;
        if (last != null && last.Length > 2)
            int.TryParse(last.Substring(2), out number);

        return $"HV{number + 1:D4}";
    }

    private Task<Student?> GetWithUser(int id)
    {
        return _db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
    }

    // GET api/admin/students?search=&status=&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? status, int page = 1, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _db.Students.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(s => s.FullName.Contains(kw) || s.Phone.Contains(kw) || s.Code.Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        var total = await query.CountAsync();

        var items = await Project(query.OrderBy(s => s.Code)
                .Skip((page - 1) * pageSize).Take(pageSize), Today())
            .ToListAsync();

        return Ok(new PagedResult<StudentResponse>
        {
            Items = items,
            TotalItems = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET api/admin/students/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var student = await BuildDetail(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        return Ok(student);
    }

    // POST api/admin/students
    [HttpPost]
    public async Task<IActionResult> Create(StudentCreateRequest req)
    {
        var email = req.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email đã được sử dụng" });

        // User + Student lưu trong cùng một SaveChanges (một transaction)
        var student = new Student
        {
            Code = await NextCode(),
            FullName = req.FullName.Trim(),
            DateOfBirth = req.DateOfBirth,
            Phone = req.Phone.Trim(),
            Address = req.Address?.Trim(),
            ParentName = req.ParentName?.Trim(),
            ParentPhone = req.ParentPhone?.Trim(),
            Status = req.Status,
            User = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.TempPassword),
                Role = "student"
            }
        };
        _db.Students.Add(student);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Không thể tạo học viên (trùng email hoặc mã), vui lòng thử lại" });
        }

        var result = await Project(_db.Students.Where(s => s.Id == student.Id), Today()).FirstAsync();
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, result);
    }

    // PUT api/admin/students/5 (không sửa mã, không sửa email)
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, StudentUpdateRequest req)
    {
        var student = await _db.Students.FindAsync(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        student.FullName = req.FullName.Trim();
        student.DateOfBirth = req.DateOfBirth;
        student.Phone = req.Phone.Trim();
        student.Address = req.Address?.Trim();
        student.ParentName = req.ParentName?.Trim();
        student.ParentPhone = req.ParentPhone?.Trim();
        student.Status = req.Status;
        await _db.SaveChangesAsync();

        var result = await Project(_db.Students.Where(s => s.Id == id), Today()).FirstAsync();
        return Ok(result);
    }

    // DELETE api/admin/students/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await GetWithUser(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        // Phiếu thu gắn với ghi danh, nên kiểm tra ghi danh là đủ cho cả hai luật
        if (await _db.Enrollments.AnyAsync(e => e.StudentId == id))
            return Conflict(new { message = "Học viên đã có ghi danh hoặc phiếu thu, không thể xóa. Hãy chuyển trạng thái sang Nghỉ (Dropped)" });

        _db.Students.Remove(student);
        _db.Users.Remove(student.User);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // POST api/admin/students/5/reset-password
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest req)
    {
        var student = await GetWithUser(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        student.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        student.User.TokenVersion++;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đặt lại mật khẩu thành công" });
    }

    // POST api/admin/students/5/lock
    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(int id)
    {
        var student = await GetWithUser(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        student.User.IsLocked = true;
        student.User.TokenVersion++;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã khóa tài khoản" });
    }

    // POST api/admin/students/5/unlock
    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(int id)
    {
        var student = await GetWithUser(id);
        if (student == null)
            return NotFound(new { message = "Không tìm thấy học viên" });

        student.User.IsLocked = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã mở khóa tài khoản" });
    }

    // POST api/admin/students/5/enrollments  { "classId": 1 }
    [HttpPost("{id}/enrollments")]
    public async Task<IActionResult> Enroll(int id, EnrollRequest req)
    {
        var result = await _enrollments.EnrollAsync(id, req.ClassId);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { message = result.Message });

        var detail = await BuildDetail(id);
        return StatusCode(StatusCodes.Status201Created, detail);
    }

    // DELETE api/admin/students/5/enrollments/12
    [HttpDelete("{id}/enrollments/{enrollmentId}")]
    public async Task<IActionResult> Withdraw(int id, int enrollmentId)
    {
        var result = await _enrollments.WithdrawAsync(id, enrollmentId);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new { message = result.Message });
    }
}