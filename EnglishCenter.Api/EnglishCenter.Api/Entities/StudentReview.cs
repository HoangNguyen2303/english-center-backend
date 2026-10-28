namespace EnglishCenter.Api.Entities;

// Nhận xét định kỳ của giáo viên về học viên (Student xem ở mục Feedback)
public class StudentReview
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public int TeacherId { get; set; }
    public string Period { get; set; } = "Monthly";    // Monthly | MidCourse | EndCourse
    public string? PeriodLabel { get; set; }           // ví dụ "11/2026"
    public string? Attitude { get; set; }              // thái độ học tập
    public string? AttendanceComment { get; set; }     // chuyên cần
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public string? ListeningComment { get; set; }
    public string? ReadingComment { get; set; }
    public string? WritingComment { get; set; }
    public string? SpeakingComment { get; set; }
    public string? OverallComment { get; set; }        // đánh giá chung (phiếu cuối khóa)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public CourseClass Class { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
}

// Giáo viên đề xuất học viên lên lớp / khóa tiếp theo, Admin xếp lớp
public class PromotionRecommendation
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int FromClassId { get; set; }
    public int TeacherId { get; set; }
    public int? RecommendedCourseId { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";    // Pending | Accepted | Rejected
    public DateTime CreatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public CourseClass FromClass { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Course? RecommendedCourse { get; set; }
}

// Đánh giá của học viên về giáo viên (bên Student tạo qua Rating/Survey)
public class TeacherRating
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int StudentId { get; set; }
    public int? ClassId { get; set; }
    public int Score { get; set; }                     // 1 - 5 sao
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public CourseClass? Class { get; set; }
}
