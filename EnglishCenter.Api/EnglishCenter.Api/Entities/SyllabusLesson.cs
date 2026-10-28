namespace EnglishCenter.Api.Entities;

// Một bài trong syllabus của khóa học
public class SyllabusLesson
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int OrderNo { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public Course Course { get; set; } = null!;
}
