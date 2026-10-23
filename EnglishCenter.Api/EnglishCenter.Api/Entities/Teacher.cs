namespace EnglishCenter.Api.Entities;

public class Teacher
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string? Degree { get; set; }
    public DateOnly? HireDate { get; set; }
    public string Status { get; set; } = "Teaching";   // Teaching | OnLeave

    public User User { get; set; } = null!;
    public List<CourseClass> Classes { get; set; } = new();
}