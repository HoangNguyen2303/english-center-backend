using System.Text.Json;
using System.Text.RegularExpressions;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

public class ServiceResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = "";
    public int? Id { get; init; }

    public static ServiceResult Ok(string message = "", int? id = null) =>
        new() { Success = true, StatusCode = 200, Message = message, Id = id };

    public static ServiceResult Fail(int statusCode, string message) =>
        new() { Success = false, StatusCode = statusCode, Message = message };
}

// Nghiệp vụ bài nộp dùng chung cho Teacher và Student:
// tự chấm bài Quiz, đếm số từ, tính điểm quy đổi, đồng bộ điểm phần thi online vào bài kiểm tra.
public partial class SubmissionService
{
    public const int GradingDueHours = 24;   // bài chưa chấm sau 24 giờ = trễ hạn chấm

    private readonly AppDbContext _db;
    private readonly NotificationService _notify;

    public SubmissionService(AppDbContext db, NotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(['’\-][\p{L}\p{N}]+)*")]
    private static partial Regex WordRegex();

    public static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : WordRegex().Matches(text).Count;

    // Điểm quy đổi 0..1 của một bài nộp đã công bố (null = chưa có điểm học viên được xem)
    public static decimal? Ratio(Submission s, string assignmentType)
    {
        if (s.Status != "Published" && s.Status != "Returned") return null;
        if (assignmentType == "Quiz")
            return s.AutoScore != null && s.AutoMaxScore > 0 ? s.AutoScore / s.AutoMaxScore : null;
        return s.OverallBand != null ? s.OverallBand / 9m : null;
    }

    // ---------- Tự chấm Quiz ----------

    public class QuizItemResult
    {
        public int QuestionId { get; init; }
        public string? Answer { get; init; }
        public string CorrectAnswer { get; init; } = "";
        public bool IsCorrect { get; init; }
        public decimal Points { get; init; }
    }

    public static Dictionary<string, string> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static string Normalize(string? s) =>
        Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"\s+", " ");

    // Trắc nghiệm: so khớp đáp án (không phân biệt hoa thường).
    // Điền từ: CorrectAnswer có thể có nhiều đáp án đúng, ngăn bằng dấu "|"
    public static List<QuizItemResult> GradeQuiz(IEnumerable<AssignmentQuestion> questions, Dictionary<string, string> answers)
    {
        var results = new List<QuizItemResult>();
        foreach (var q in questions.OrderBy(x => x.OrderNo))
        {
            answers.TryGetValue(q.Id.ToString(), out var answer);
            var accepted = q.CorrectAnswer.Split('|').Select(Normalize).Where(x => x.Length > 0);
            var correct = answer != null && accepted.Contains(Normalize(answer));
            results.Add(new QuizItemResult
            {
                QuestionId = q.Id,
                Answer = answer,
                CorrectAnswer = q.CorrectAnswer,
                IsCorrect = correct,
                Points = correct ? q.Points : 0
            });
        }
        return results;
    }

    // ---------- Học viên nộp bài (dành cho phần Student gọi lại) ----------

    // Danh sách học viên được giao một bài: học viên đang học (Active) của lớp,
    // nếu bài không giao cho cả lớp thì chỉ lấy học viên được chọn
    public async Task<List<int>> AssignedStudentIdsAsync(Assignment a)
    {
        var active = _db.Enrollments
            .Where(e => e.ClassId == a.ClassId && e.Status == "Active")
            .Select(e => e.StudentId);

        if (a.AssignToAll) return await active.ToListAsync();

        return await _db.AssignmentStudents
            .Where(t => t.AssignmentId == a.Id && active.Contains(t.StudentId))
            .Select(t => t.StudentId)
            .ToListAsync();
    }

    public async Task<ServiceResult> SubmitAsync(int studentId, int assignmentId, string? content,
        string? audioUrl, Dictionary<string, string>? answers)
    {
        var a = await _db.Assignments.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a == null) return ServiceResult.Fail(404, "Không tìm thấy bài tập");

        var assigned = await AssignedStudentIdsAsync(a);
        if (!assigned.Contains(studentId)) return ServiceResult.Fail(403, "Bài tập này không được giao cho bạn");

        var now = AppTime.Now;
        var late = now > a.DueAt;
        if (late && !a.AllowLate) return ServiceResult.Fail(409, "Đã hết hạn nộp bài");

        var last = await _db.Submissions
            .Where(s => s.AssignmentId == assignmentId && s.StudentId == studentId)
            .OrderByDescending(s => s.AttemptNo)
            .FirstOrDefaultAsync();
        if (last != null && last.Status != "Returned")
            return ServiceResult.Fail(409, "Bạn đã nộp bài này. Chỉ nộp lại được khi giáo viên trả bài cho sửa");

        var sub = new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            AttemptNo = (last?.AttemptNo ?? 0) + 1,
            SubmittedAt = now,
            IsLate = late,
            Status = "Submitted"
        };

        switch (a.Type)
        {
            case "Writing":
                if (string.IsNullOrWhiteSpace(content)) return ServiceResult.Fail(400, "Bài viết đang trống");
                sub.Content = content;
                sub.WordCount = CountWords(content);
                break;
            case "Speaking":
                if (string.IsNullOrWhiteSpace(audioUrl)) return ServiceResult.Fail(400, "Chưa có bản thu âm");
                sub.AudioUrl = audioUrl;
                break;
            default: // Quiz: chấm ngay, công bố luôn
                answers ??= new();
                var graded = GradeQuiz(a.Questions, answers);
                sub.AnswersJson = JsonSerializer.Serialize(answers);
                sub.AutoScore = graded.Sum(x => x.Points);
                sub.AutoMaxScore = a.Questions.Sum(q => q.Points);
                sub.Status = "Published";
                sub.GradedAt = now;
                sub.PublishedAt = now;
                break;
        }

        _db.Submissions.Add(sub);

        var studentName = await _db.Students.Where(s => s.Id == studentId).Select(s => s.FullName).FirstAsync();
        await _notify.AddToClassTeacherAsync(a.ClassId, "Submission",
            $"{studentName} vừa nộp bài \"{a.Title}\"" + (sub.AttemptNo > 1 ? $" (lần {sub.AttemptNo})" : ""),
            late ? "Nộp trễ hạn" : null, $"/teacher/grading/{a.Id}");

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail(409, "Bài đã được nộp");
        }

        if (a.ExamId != null && sub.Status == "Published")
            await SyncExamResultAsync(a.ExamId.Value, studentId);

        return ServiceResult.Ok("Nộp bài thành công", sub.Id);
    }

    // ---------- Đồng bộ điểm phần thi online -> sổ điểm bài kiểm tra ----------

    // Điểm bài kiểm tra = trung bình điểm quy đổi các phần online đã có điểm x Điểm tối đa.
    // Nếu giáo viên đã nhập tay (Source = Manual) thì không ghi đè.
    public async Task SyncExamResultAsync(int examId, int studentId)
    {
        var exam = await _db.Exams.FindAsync(examId);
        if (exam == null) return;

        var parts = await _db.Assignments.Where(a => a.ExamId == examId)
            .Select(a => new { a.Id, a.Type }).ToListAsync();
        if (parts.Count == 0) return;

        var partIds = parts.Select(p => p.Id).ToList();
        var subs = await _db.Submissions
            .Where(s => partIds.Contains(s.AssignmentId) && s.StudentId == studentId)
            .ToListAsync();

        var ratios = new List<decimal>();
        foreach (var p in parts)
        {
            var latest = subs.Where(s => s.AssignmentId == p.Id).OrderByDescending(s => s.AttemptNo).FirstOrDefault();
            var r = latest == null ? null : Ratio(latest, p.Type);
            if (r != null) ratios.Add(r.Value);
        }
        if (ratios.Count == 0) return;

        var score = Math.Round(ratios.Average() * exam.MaxScore, 2);
        var result = await _db.ExamResults.FirstOrDefaultAsync(r => r.ExamId == examId && r.StudentId == studentId);
        if (result == null)
        {
            _db.ExamResults.Add(new ExamResult
            {
                ExamId = examId,
                StudentId = studentId,
                Score = score,
                Source = "Online",
                UpdatedAt = AppTime.Now
            });
        }
        else if (result.Source == "Online" || result.Score == null)
        {
            result.Score = score;
            result.Source = "Online";
            result.UpdatedAt = AppTime.Now;
        }
        await _db.SaveChangesAsync();
    }
}
