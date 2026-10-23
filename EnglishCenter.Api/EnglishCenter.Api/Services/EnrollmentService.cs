using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

public class EnrollmentResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = "";

    public static EnrollmentResult Ok(string message = "") =>
        new() { Success = true, StatusCode = 200, Message = message };

    public static EnrollmentResult Fail(int statusCode, string message) =>
        new() { Success = false, StatusCode = statusCode, Message = message };
}

public class EnrollmentService
{
    private readonly AppDbContext _db;

    public EnrollmentService(AppDbContext db)
    {
        _db = db;
    }

    // Ghi danh học viên vào lớp. Học phí lấy từ khóa học, KHÔNG nhận từ client.
    public async Task<EnrollmentResult> EnrollAsync(int studentId, int classId)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null)
            return EnrollmentResult.Fail(404, "Không tìm thấy học viên");

        var cls = await _db.Classes.Include(c => c.Course).FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null)
            return EnrollmentResult.Fail(404, "Không tìm thấy lớp học");

        if (student.Status == "Dropped")
            return EnrollmentResult.Fail(409, "Học viên đã nghỉ, không thể ghi danh");

        if (await _db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.ClassId == classId))
            return EnrollmentResult.Fail(409, "Học viên đã có ghi danh trong lớp này");

        var activeCount = await _db.Enrollments
            .CountAsync(e => e.ClassId == classId && e.Status == "Active");
        if (activeCount >= cls.MaxStudents)
            return EnrollmentResult.Fail(409, $"Lớp đã đầy ({activeCount}/{cls.MaxStudents})");

        // Tính học phí từ khóa học tại thời điểm ghi danh (snapshot)
        var tuition = cls.Course.Tuition;
        var percent = cls.Course.DiscountPercent;
        var discountAmount = Math.Round(tuition * percent / 100, 2);

        _db.Enrollments.Add(new Enrollment
        {
            StudentId = studentId,
            ClassId = classId,
            TuitionFee = tuition,
            DiscountPercent = percent,
            DiscountAmount = discountAmount,
            FinalTuitionFee = tuition - discountAmount,
            Status = "Active"
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // unique index (StudentId, ClassId) chặn khi 2 request trùng nhau cùng lúc
            return EnrollmentResult.Fail(409, "Học viên đã có ghi danh trong lớp này");
        }

        return EnrollmentResult.Ok("Ghi danh thành công");
    }

    // Rút học viên khỏi lớp: chưa có phiếu thu thì xóa hẳn, có rồi thì đổi sang Dropped
    public async Task<EnrollmentResult> WithdrawAsync(int studentId, int enrollmentId)
    {
        var enrollment = await _db.Enrollments
            .Include(e => e.Payment)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == studentId);

        if (enrollment == null)
            return EnrollmentResult.Fail(404, "Không tìm thấy ghi danh");

        if (enrollment.Payment == null)
        {
            _db.Enrollments.Remove(enrollment);
            await _db.SaveChangesAsync();
            return EnrollmentResult.Ok("Đã rút học viên khỏi lớp");
        }

        enrollment.Status = "Dropped";
        await _db.SaveChangesAsync();
        return EnrollmentResult.Ok("Học viên đã có phiếu thu nên ghi danh được chuyển sang trạng thái Dropped");
    }
}