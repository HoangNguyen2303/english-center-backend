namespace EnglishCenter.Api.Entities;

public class Assignment
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int TeacherId { get; set; }
    public int? ExamId { get; set; }                   // khác null = phần thi online của một bài kiểm tra
    public string Title { get; set; } = "";
    public string Skill { get; set; } = "Writing";     // Writing | Speaking | Reading | Listening
    public string Type { get; set; } = "Writing";      // Writing | Speaking | Quiz (trắc nghiệm / điền từ, tự chấm)
    public string Prompt { get; set; } = "";           // đề bài
    public DateTime DueAt { get; set; }
    public int? MinWords { get; set; }                 // chỉ dùng cho Writing
    public string? AttachmentUrl { get; set; }         // ảnh biểu đồ Task 1, file audio Listening...
    public string? Note { get; set; }
    public bool AllowLate { get; set; }                // false = chặn nộp sau hạn; true = cho nộp, đánh dấu "Trễ"
    public bool AssignToAll { get; set; } = true;      // false = chỉ giao cho các học viên trong AssignmentStudents
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public CourseClass Class { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Exam? Exam { get; set; }
    public List<AssignmentStudent> Targets { get; set; } = new();
    public List<AssignmentQuestion> Questions { get; set; } = new();
    public List<Submission> Submissions { get; set; } = new();
}

// Học viên được giao bài (khi không giao cho cả lớp)
public class AssignmentStudent
{
    public int AssignmentId { get; set; }
    public int StudentId { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public Student Student { get; set; } = null!;
}

// Câu hỏi trắc nghiệm / điền từ (Reading, Listening) - hệ thống tự chấm theo đáp án
public class AssignmentQuestion
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int OrderNo { get; set; }
    public string QuestionType { get; set; } = "MultipleChoice";   // MultipleChoice | FillBlank
    public string Content { get; set; } = "";
    public string? OptionsJson { get; set; }           // ["A. ...","B. ..."] cho trắc nghiệm
    public string CorrectAnswer { get; set; } = "";    // trắc nghiệm: "B"; điền từ: nhiều đáp án đúng ngăn bằng "|"
    public decimal Points { get; set; } = 1;

    public Assignment Assignment { get; set; } = null!;
}
