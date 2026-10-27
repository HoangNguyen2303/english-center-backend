namespace EnglishCenter.Api.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = "";             // HV0001
    public string FullName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public string Status { get; set; } = "Studying";   // Studying | OnHold | Dropped
    public decimal? TargetBand { get; set; }           // band mục tiêu (dùng cho biểu đồ tiến bộ)
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public List<Enrollment> Enrollments { get; set; } = new();
}