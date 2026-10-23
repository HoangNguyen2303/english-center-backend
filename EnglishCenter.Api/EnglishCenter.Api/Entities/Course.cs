namespace EnglishCenter.Api.Entities;

public class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal BandFrom { get; set; }
    public decimal BandTo { get; set; }
    public int TotalSessions { get; set; }
    public int SessionsPerWeek { get; set; }
    public decimal Tuition { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<CourseClass> Classes { get; set; } = new();
}