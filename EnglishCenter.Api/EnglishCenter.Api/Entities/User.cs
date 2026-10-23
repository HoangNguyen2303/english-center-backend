namespace EnglishCenter.Api.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";          // admin | teacher | student
    public string? AvatarUrl { get; set; }
    public bool IsLocked { get; set; }
    public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; }

    // Quan hệ (thêm sau khi có Teacher, Student, Payment)
    public Teacher? Teacher { get; set; }
    public Student? Student { get; set; }
    public List<Payment> Payments { get; set; } = new();
}