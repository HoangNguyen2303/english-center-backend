namespace EnglishCenter.Api.Dtos.Admin;

public class StudentResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public string Status { get; set; } = "";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }

    // Tên lớp hiện tại (lớp chưa kết thúc mà học viên đang học)
    public string? CurrentClass { get; set; }

    // Chỉ có ở trang chi tiết
    public List<StudentEnrollmentItem>? Enrollments { get; set; }
}