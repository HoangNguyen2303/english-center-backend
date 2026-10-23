namespace EnglishCenter.Api.Dtos.Admin;

public class TeacherResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string? Degree { get; set; }
    public DateOnly? HireDate { get; set; }
    public string Status { get; set; } = "";
    public bool IsLocked { get; set; }
    public int ClassCount { get; set; }

    // Chỉ có ở trang chi tiết, danh sách thì để null
    public List<TeacherClassItem>? Classes { get; set; }
}

public class TeacherClassItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CourseName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "";   // Upcoming | Ongoing | Finished
}