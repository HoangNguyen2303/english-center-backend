namespace EnglishCenter.Api.Entities;

public class CourseClass
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int TeacherId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int MaxStudents { get; set; }
    public string? MeetingUrl { get; set; }

    public Course Course { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public List<ClassSchedule> Schedules { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();
}