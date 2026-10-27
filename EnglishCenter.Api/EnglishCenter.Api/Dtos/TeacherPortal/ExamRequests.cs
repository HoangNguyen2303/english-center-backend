using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

public class ExamRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn lớp")]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Tên bài kiểm tra là bắt buộc")]
    [StringLength(250)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Loại bài kiểm tra là bắt buộc")]
    [RegularExpression("Periodic|Midterm|Final", ErrorMessage = "Loại phải là Periodic (định kỳ), Midterm (giữa kỳ) hoặc Final (cuối kỳ)")]
    public string Type { get; set; } = "Periodic";

    [RegularExpression("Writing|Speaking|Reading|Listening", ErrorMessage = "Kỹ năng phải là Writing, Speaking, Reading hoặc Listening")]
    public string? Skill { get; set; }

    public DateOnly ExamDate { get; set; }

    [Range(5, 600, ErrorMessage = "Thời gian làm bài từ 5 đến 600 phút")]
    public int? DurationMinutes { get; set; }

    [Range(1, 1000, ErrorMessage = "Điểm tối đa phải lớn hơn 0")]
    public decimal MaxScore { get; set; } = 10;
}

public class ExamResultItem
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    // null = xóa điểm
    [Range(0, 1000)]
    public decimal? Score { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class ExamResultsRequest
{
    [MinLength(1, ErrorMessage = "Danh sách điểm đang trống")]
    public List<ExamResultItem> Items { get; set; } = new();
}

public class GradeConfigRequest : IValidatableObject
{
    [Range(0, 100)] public decimal AssignmentWeight { get; set; }
    [Range(0, 100)] public decimal PeriodicWeight { get; set; }
    [Range(0, 100)] public decimal MidtermWeight { get; set; }
    [Range(0, 100)] public decimal FinalWeight { get; set; }

    // Điểm tổng kết (thang 10) tối thiểu để Đạt
    [Range(0, 10)] public decimal PassScore { get; set; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (AssignmentWeight + PeriodicWeight + MidtermWeight + FinalWeight != 100)
            yield return new ValidationResult("Tổng các trọng số phải bằng 100%");
    }
}
