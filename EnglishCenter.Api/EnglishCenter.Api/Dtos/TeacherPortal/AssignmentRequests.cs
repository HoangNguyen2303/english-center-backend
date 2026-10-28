using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

public class QuestionRequest : IValidatableObject
{
    [Required]
    [RegularExpression("MultipleChoice|FillBlank", ErrorMessage = "Loại câu hỏi phải là MultipleChoice hoặc FillBlank")]
    public string QuestionType { get; set; } = "MultipleChoice";

    [Required(ErrorMessage = "Nội dung câu hỏi là bắt buộc")]
    [StringLength(2000)]
    public string Content { get; set; } = "";

    // Trắc nghiệm: ["A. ...", "B. ...", ...]
    public List<string>? Options { get; set; }

    // Trắc nghiệm: "B". Điền từ: nhiều đáp án chấp nhận được thì ngăn bằng "|", ví dụ "colour|color"
    [Required(ErrorMessage = "Đáp án đúng là bắt buộc")]
    [StringLength(500)]
    public string CorrectAnswer { get; set; } = "";

    [Range(0.25, 100)]
    public decimal Points { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (QuestionType == "MultipleChoice" && (Options == null || Options.Count < 2))
            yield return new ValidationResult("Câu trắc nghiệm cần ít nhất 2 lựa chọn");
    }
}

public class AssignmentRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn lớp")]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Kỹ năng là bắt buộc")]
    [RegularExpression("Writing|Speaking|Reading|Listening", ErrorMessage = "Kỹ năng phải là Writing, Speaking, Reading hoặc Listening")]
    public string Skill { get; set; } = "";

    // Writing | Speaking (thu âm) | Quiz (trắc nghiệm / điền từ cho Reading, Listening - tự chấm)
    [Required(ErrorMessage = "Loại bài là bắt buộc")]
    [RegularExpression("Writing|Speaking|Quiz", ErrorMessage = "Loại bài phải là Writing, Speaking hoặc Quiz")]
    public string Type { get; set; } = "";

    [Required(ErrorMessage = "Tên bài là bắt buộc")]
    [StringLength(250)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Đề bài là bắt buộc")]
    public string Prompt { get; set; } = "";

    public DateTime DueAt { get; set; }

    [Range(1, 2000, ErrorMessage = "Số từ tối thiểu từ 1 đến 2000")]
    public int? MinWords { get; set; }

    [StringLength(500)]
    public string? AttachmentUrl { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public bool AllowLate { get; set; }

    // true = cả lớp; false = chỉ các học viên trong StudentIds
    public bool AssignToAll { get; set; } = true;
    public List<int> StudentIds { get; set; } = new();

    public List<QuestionRequest> Questions { get; set; } = new();

    // Gắn bài này làm phần thi online của một bài kiểm tra (page Kiểm tra & Sổ điểm)
    public int? ExamId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (DueAt == default)
            yield return new ValidationResult("Hạn nộp là bắt buộc", new[] { nameof(DueAt) });

        if (Type == "Writing" && Skill != "Writing")
            yield return new ValidationResult("Bài loại Writing phải thuộc kỹ năng Writing");
        if (Type == "Speaking" && Skill != "Speaking")
            yield return new ValidationResult("Bài loại Speaking phải thuộc kỹ năng Speaking");
        if (Type == "Quiz" && Skill != "Reading" && Skill != "Listening")
            yield return new ValidationResult("Bài trắc nghiệm / điền từ dùng cho kỹ năng Reading hoặc Listening");

        if (Type == "Quiz" && Questions.Count == 0)
            yield return new ValidationResult("Bài trắc nghiệm / điền từ cần ít nhất 1 câu hỏi");
        if (Type != "Quiz" && Questions.Count > 0)
            yield return new ValidationResult("Chỉ bài Quiz mới có câu hỏi tự chấm");
        if (Type != "Writing" && MinWords != null)
            yield return new ValidationResult("Số từ tối thiểu chỉ dùng cho bài Writing");

        if (!AssignToAll && StudentIds.Count == 0)
            yield return new ValidationResult("Hãy chọn ít nhất 1 học viên hoặc giao cho cả lớp");
    }
}
