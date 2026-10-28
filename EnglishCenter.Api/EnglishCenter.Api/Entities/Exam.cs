namespace EnglishCenter.Api.Entities;

// Bài kiểm tra định kỳ / giữa kỳ / cuối kỳ
public class Exam
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int TeacherId { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "Periodic";     // Periodic | Midterm | Final
    public string? Skill { get; set; }                 // Writing | Speaking | Reading | Listening | null = tổng hợp
    public DateOnly ExamDate { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal MaxScore { get; set; } = 10;
    public DateTime CreatedAt { get; set; }

    public CourseClass Class { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public List<ExamResult> Results { get; set; } = new();
    public List<Assignment> OnlineParts { get; set; } = new();
}

public class ExamResult
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int StudentId { get; set; }
    public decimal? Score { get; set; }
    public string Source { get; set; } = "Manual";     // Manual (nhập tay, thi giấy) | Online (lấy từ phần thi online)
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Exam Exam { get; set; } = null!;
    public Student Student { get; set; } = null!;
}

// Trọng số sổ điểm của từng lớp
public class GradeConfig
{
    public int ClassId { get; set; }                   // khóa chính, 1 lớp - 1 cấu hình
    public decimal AssignmentWeight { get; set; } = 20;
    public decimal PeriodicWeight { get; set; } = 0;
    public decimal MidtermWeight { get; set; } = 30;
    public decimal FinalWeight { get; set; } = 50;
    public decimal PassScore { get; set; } = 5;        // điểm tổng kết (thang 10) tối thiểu để Đạt

    public CourseClass Class { get; set; } = null!;
}
