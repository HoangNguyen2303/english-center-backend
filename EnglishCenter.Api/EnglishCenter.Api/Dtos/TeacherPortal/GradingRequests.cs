using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

public class AnnotationRequest : IValidatableObject
{
    [Range(0, int.MaxValue)]
    public int StartOffset { get; set; }

    [Range(1, int.MaxValue)]
    public int EndOffset { get; set; }

    [Required]
    [StringLength(2000)]
    public string SelectedText { get; set; } = "";

    [Required]
    [RegularExpression("Grammar|Vocabulary|Coherence", ErrorMessage = "Loại lỗi phải là Grammar (ngữ pháp), Vocabulary (từ vựng) hoặc Coherence (mạch lạc)")]
    public string ErrorType { get; set; } = "Grammar";

    [StringLength(1000)]
    public string? Suggestion { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (EndOffset <= StartOffset)
            yield return new ValidationResult("Vị trí highlight không hợp lệ");
    }
}

// Lưu nháp điểm + nhận xét. Writing dùng TR, CC, LR, GRA; Speaking dùng FC, LR, GRA, P
public class GradeRequest
{
    public decimal? Tr { get; set; }
    public decimal? Cc { get; set; }
    public decimal? Fc { get; set; }
    public decimal? Lr { get; set; }
    public decimal? Gra { get; set; }
    public decimal? P { get; set; }

    [StringLength(8000)]
    public string? Feedback { get; set; }

    [StringLength(500)]
    public string? FeedbackAudioUrl { get; set; }

    // Gửi null = giữ nguyên các highlight cũ; gửi danh sách (kể cả rỗng) = thay toàn bộ
    public List<AnnotationRequest>? Annotations { get; set; }
}

public class ReturnSubmissionRequest
{
    [Required(ErrorMessage = "Hãy ghi rõ học viên cần sửa gì")]
    [StringLength(2000)]
    public string Message { get; set; } = "";
}

public class CommentTemplateRequest
{
    [Required]
    [RegularExpression("Writing|Speaking", ErrorMessage = "Kỹ năng phải là Writing hoặc Speaking")]
    public string Skill { get; set; } = "Writing";

    [RegularExpression("TR|CC|FC|LR|GRA|P", ErrorMessage = "Tiêu chí phải là TR, CC, FC, LR, GRA hoặc P")]
    public string? Criterion { get; set; }

    [Required(ErrorMessage = "Nội dung nhận xét là bắt buộc")]
    [StringLength(1000)]
    public string Content { get; set; } = "";
}
