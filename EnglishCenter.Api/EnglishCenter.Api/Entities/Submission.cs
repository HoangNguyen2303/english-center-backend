namespace EnglishCenter.Api.Entities;

// Một lần nộp bài của học viên. Bị trả bài để sửa thì lần nộp sau có AttemptNo + 1.
public class Submission
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int StudentId { get; set; }
    public int AttemptNo { get; set; } = 1;
    public string? Content { get; set; }               // bài viết Writing
    public string? AudioUrl { get; set; }              // bản thu âm Speaking
    public string? AnswersJson { get; set; }           // đáp án bài Quiz: {"12":"B","13":"photosynthesis"}
    public int WordCount { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool IsLate { get; set; }

    // Submitted (chờ chấm) | Draft (đã chấm nháp) | Published (đã công bố) | Returned (trả bài cho sửa)
    public string Status { get; set; } = "Submitted";

    // Điểm IELTS 0-9, bước 0.5. Writing: TR, CC, LR, GRA. Speaking: FC, LR, GRA, P
    public decimal? Tr { get; set; }
    public decimal? Cc { get; set; }
    public decimal? Fc { get; set; }
    public decimal? Lr { get; set; }
    public decimal? Gra { get; set; }
    public decimal? P { get; set; }
    public decimal? OverallBand { get; set; }

    // Bài Quiz tự chấm
    public decimal? AutoScore { get; set; }
    public decimal? AutoMaxScore { get; set; }

    public string? Feedback { get; set; }
    public string? FeedbackAudioUrl { get; set; }      // ghi âm nhận xét Speaking
    public string? ReturnNote { get; set; }            // lời dặn khi trả bài cho học viên sửa
    public DateTime? GradedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public List<SubmissionAnnotation> Annotations { get; set; } = new();
}

// Lỗi được highlight trên bài viết
public class SubmissionAnnotation
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public int StartOffset { get; set; }               // vị trí ký tự bắt đầu trong Content
    public int EndOffset { get; set; }                 // vị trí kết thúc (không bao gồm)
    public string SelectedText { get; set; } = "";
    public string ErrorType { get; set; } = "Grammar"; // Grammar | Vocabulary | Coherence
    public string? Suggestion { get; set; }
    public string? Comment { get; set; }

    public Submission Submission { get; set; } = null!;
}

// Ngân hàng nhận xét mẫu. TeacherId = null là mẫu chung của hệ thống
public class CommentTemplate
{
    public int Id { get; set; }
    public int? TeacherId { get; set; }
    public string Skill { get; set; } = "Writing";     // Writing | Speaking
    public string? Criterion { get; set; }             // TR | CC | FC | LR | GRA | P | null = chung
    public string Content { get; set; } = "";

    public Teacher? Teacher { get; set; }
}
