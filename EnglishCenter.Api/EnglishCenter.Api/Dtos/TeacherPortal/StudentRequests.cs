using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

public class StudentReviewRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn lớp")]
    public int ClassId { get; set; }

    [Required]
    [RegularExpression("Monthly|MidCourse|EndCourse", ErrorMessage = "Kỳ nhận xét phải là Monthly (theo tháng), MidCourse (giữa khóa) hoặc EndCourse (cuối khóa)")]
    public string Period { get; set; } = "Monthly";

    [StringLength(50)]
    public string? PeriodLabel { get; set; }     // ví dụ "11/2026"

    [StringLength(2000)] public string? Attitude { get; set; }
    [StringLength(2000)] public string? AttendanceComment { get; set; }
    [StringLength(2000)] public string? Strengths { get; set; }
    [StringLength(2000)] public string? Improvements { get; set; }
    [StringLength(2000)] public string? ListeningComment { get; set; }
    [StringLength(2000)] public string? ReadingComment { get; set; }
    [StringLength(2000)] public string? WritingComment { get; set; }
    [StringLength(2000)] public string? SpeakingComment { get; set; }
    [StringLength(4000)] public string? OverallComment { get; set; }
}

public class PromotionCreate
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn lớp hiện tại của học viên")]
    public int ClassId { get; set; }

    // Khóa đề xuất học tiếp (bỏ trống = để Admin tự chọn)
    public int? RecommendedCourseId { get; set; }

    [StringLength(1000)]
    public string? Reason { get; set; }
}

public class TargetBandRequest
{
    [Range(0, 9, ErrorMessage = "Band mục tiêu từ 0 đến 9")]
    public decimal? TargetBand { get; set; }
}

public class AnnouncementCreate : IValidatableObject
{
    // Gửi cho cả lớp: chỉ cần ClassId. Gửi cho từng học viên: StudentIds (có thể kèm ClassId)
    public int? ClassId { get; set; }
    public List<int> StudentIds { get; set; } = new();

    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    [StringLength(250)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Nội dung là bắt buộc")]
    [StringLength(4000)]
    public string Content { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (ClassId == null && StudentIds.Count == 0)
            yield return new ValidationResult("Hãy chọn lớp hoặc học viên nhận thông báo");
    }
}

public class TeacherChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Hãy nhập lại mật khẩu mới")]
    [Compare(nameof(NewPassword), ErrorMessage = "Nhập lại mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = "";
}
