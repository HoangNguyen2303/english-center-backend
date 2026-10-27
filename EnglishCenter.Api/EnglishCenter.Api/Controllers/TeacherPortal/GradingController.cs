using System.Text.Json;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 6 - Chấm bài. Bài chưa chấm sau 24 giờ tính là trễ hạn chấm.
[Route("api/teacher/grading")]
public class GradingController : TeacherControllerBase
{
    private readonly SubmissionService _submissions;
    private readonly NotificationService _notify;

    public GradingController(AppDbContext db, SubmissionService submissions, NotificationService notify) : base(db)
    {
        _submissions = submissions;
        _notify = notify;
    }

    // Chỉ lấy lần nộp mới nhất của mỗi học viên cho mỗi bài
    private IQueryable<Submission> LatestSubmissions() =>
        Db.Submissions.Where(s => s.Assignment.Class.TeacherId == TeacherId
            && !Db.Submissions.Any(o => o.AssignmentId == s.AssignmentId && o.StudentId == s.StudentId && o.AttemptNo > s.AttemptNo));

    // GET api/teacher/grading/submissions?status=pending|overdue|graded|returned&classId=&skill=&assignmentId=&includeQuiz=false
    // Sắp xếp: bài nộp sớm nhất lên đầu
    [HttpGet("submissions")]
    public async Task<IActionResult> GetAll(string? status, int? classId, string? skill, int? assignmentId,
        bool includeQuiz = false, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var now = AppTime.Now;
        var overdueBefore = now.AddHours(-SubmissionService.GradingDueHours);
        var query = LatestSubmissions();

        if (classId != null) query = query.Where(s => s.Assignment.ClassId == classId);
        if (!string.IsNullOrWhiteSpace(skill)) query = query.Where(s => s.Assignment.Skill == skill);
        if (assignmentId != null) query = query.Where(s => s.AssignmentId == assignmentId);
        if (!includeQuiz) query = query.Where(s => s.Assignment.Type != "Quiz");

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.Trim().ToLower())
            {
                case "pending": query = query.Where(s => s.Status == "Submitted" || s.Status == "Draft"); break;
                case "overdue":
                    query = query.Where(s => (s.Status == "Submitted" || s.Status == "Draft") && s.SubmittedAt < overdueBefore);
                    break;
                case "graded": query = query.Where(s => s.Status == "Published"); break;
                case "returned": query = query.Where(s => s.Status == "Returned"); break;
                default: return Error(400, "status phải là pending, overdue, graded hoặc returned");
            }
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.AssignmentId, AssignmentTitle = s.Assignment.Title, s.Assignment.Skill, s.Assignment.Type,
                s.Assignment.ClassId, ClassName = s.Assignment.Class.Name,
                s.StudentId, StudentCode = s.Student.Code, StudentName = s.Student.FullName,
                s.AttemptNo, s.SubmittedAt, s.IsLate, s.Status, s.OverallBand, s.AutoScore, s.AutoMaxScore, s.WordCount
            })
            .ToListAsync();

        return Ok(new
        {
            totalItems = total,
            page,
            pageSize,
            items = items.Select(s => new
            {
                s.Id, s.AssignmentId, s.AssignmentTitle, s.Skill, s.Type, s.ClassId, s.ClassName,
                s.StudentId, s.StudentCode, s.StudentName, s.AttemptNo, s.SubmittedAt, s.IsLate, s.Status,
                s.OverallBand, s.AutoScore, s.AutoMaxScore, s.WordCount,
                gradingOverdue = (s.Status == "Submitted" || s.Status == "Draft") && s.SubmittedAt < overdueBefore,
                gradingDueAt = s.SubmittedAt.AddHours(SubmissionService.GradingDueHours)
            })
        });
    }

    // GET api/teacher/grading/summary -> số bài chờ chấm, số bài trễ hạn 24h
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var overdueBefore = AppTime.Now.AddHours(-SubmissionService.GradingDueHours);
        var pending = LatestSubmissions().Where(s => s.Assignment.Type != "Quiz" && (s.Status == "Submitted" || s.Status == "Draft"));
        return Ok(new
        {
            pending = await pending.CountAsync(),
            overdue = await pending.CountAsync(s => s.SubmittedAt < overdueBefore),
            writing = await pending.CountAsync(s => s.Assignment.Type == "Writing"),
            speaking = await pending.CountAsync(s => s.Assignment.Type == "Speaking")
        });
    }

    // GET api/teacher/grading/submissions/21  -> trang chấm
    [HttpGet("submissions/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await Db.Submissions
            .Include(x => x.Assignment).ThenInclude(a => a.Class)
            .Include(x => x.Assignment).ThenInclude(a => a.Questions)
            .Include(x => x.Student)
            .Include(x => x.Annotations)
            .FirstOrDefaultAsync(x => x.Id == id && x.Assignment.Class.TeacherId == TeacherId);
        if (s == null) return Error(404, "Không tìm thấy bài nộp");

        var a = s.Assignment;
        var history = await Db.Submissions
            .Where(x => x.AssignmentId == s.AssignmentId && x.StudentId == s.StudentId)
            .OrderBy(x => x.AttemptNo)
            .Select(x => new { x.Id, x.AttemptNo, x.SubmittedAt, x.Status, x.OverallBand, x.IsLate, x.ReturnNote })
            .ToListAsync();

        var next = await NextPendingIdAsync(id, null, null);

        object? quiz = null;
        if (a.Type == "Quiz")
        {
            var answers = SubmissionService.ParseAnswers(s.AnswersJson);
            quiz = SubmissionService.GradeQuiz(a.Questions, answers).Select(r => new
            {
                r.QuestionId,
                content = a.Questions.First(q => q.Id == r.QuestionId).Content,
                r.Answer, r.CorrectAnswer, r.IsCorrect, r.Points
            });
        }

        var minWordsWarning = a.Type == "Writing" && a.MinWords != null && s.WordCount < a.MinWords;

        return Ok(new
        {
            s.Id, s.Status, s.AttemptNo, s.SubmittedAt, s.IsLate,
            gradingOverdue = (s.Status == "Submitted" || s.Status == "Draft")
                && s.SubmittedAt < AppTime.Now.AddHours(-SubmissionService.GradingDueHours),
            student = new { s.Student.Id, s.Student.Code, s.Student.FullName, s.Student.TargetBand },
            assignment = new
            {
                a.Id, a.Title, a.Skill, a.Type, a.Prompt, a.AttachmentUrl, a.MinWords, a.DueAt,
                a.ClassId, ClassName = a.Class.Name
            },
            content = s.Content,
            wordCount = s.WordCount,
            minWordsWarning,
            minWordsMessage = minWordsWarning ? $"Bài viết có {s.WordCount} từ, ít hơn yêu cầu {a.MinWords} từ" : null,
            audioUrl = s.AudioUrl,
            quiz,
            criteria = a.Type == "Quiz" ? Array.Empty<string>() : IeltsBands.CriteriaOf(a.Skill),
            scores = new { s.Tr, s.Cc, s.Fc, s.Lr, s.Gra, s.P },
            s.OverallBand, s.AutoScore, s.AutoMaxScore,
            s.Feedback, s.FeedbackAudioUrl, s.ReturnNote, s.GradedAt, s.PublishedAt,
            annotations = s.Annotations.OrderBy(x => x.StartOffset).Select(x => new
            {
                x.Id, x.StartOffset, x.EndOffset, x.SelectedText, x.ErrorType, x.Suggestion, x.Comment
            }),
            history,
            nextSubmissionId = next
        });
    }

    // GET api/teacher/grading/next?currentId=21&classId=&skill=  -> nút "Bài tiếp theo"
    [HttpGet("next")]
    public async Task<IActionResult> Next(int? currentId, int? classId, string? skill)
    {
        var id = await NextPendingIdAsync(currentId, classId, skill);
        if (id == null) return Ok(new { nextSubmissionId = (int?)null, message = "Đã chấm hết bài" });
        return Ok(new { nextSubmissionId = id });
    }

    private Task<int?> NextPendingIdAsync(int? currentId, int? classId, string? skill)
    {
        var q = LatestSubmissions().Where(s => s.Assignment.Type != "Quiz"
            && (s.Status == "Submitted" || s.Status == "Draft"));
        if (currentId != null) q = q.Where(s => s.Id != currentId);
        if (classId != null) q = q.Where(s => s.Assignment.ClassId == classId);
        if (!string.IsNullOrWhiteSpace(skill)) q = q.Where(s => s.Assignment.Skill == skill);
        return q.OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync();
    }

    private async Task<Submission?> LoadForGradingAsync(int id) =>
        await Db.Submissions
            .Include(x => x.Assignment).ThenInclude(a => a.Class)
            .Include(x => x.Student)
            .Include(x => x.Annotations)
            .FirstOrDefaultAsync(x => x.Id == id && x.Assignment.Class.TeacherId == TeacherId);

    private async Task<bool> IsLatestAttemptAsync(Submission s) =>
        !await Db.Submissions.AnyAsync(o => o.AssignmentId == s.AssignmentId && o.StudentId == s.StudentId && o.AttemptNo > s.AttemptNo);

    // Áp điểm từ request vào bài nộp, trả về lỗi nếu có
    private static string? ApplyScores(Submission s, GradeRequest req)
    {
        var skill = s.Assignment.Skill;
        var map = new Dictionary<string, decimal?>
        {
            ["TR"] = req.Tr, ["CC"] = req.Cc, ["FC"] = req.Fc, ["LR"] = req.Lr, ["GRA"] = req.Gra, ["P"] = req.P
        };
        var criteria = IeltsBands.CriteriaOf(skill);

        foreach (var (key, value) in map)
        {
            if (value == null) continue;
            if (!criteria.Contains(key))
                return $"Bài {skill} không chấm tiêu chí {key} (chỉ dùng {string.Join(", ", criteria)})";
            if (!IeltsBands.IsValid(value.Value))
                return $"Điểm {key} phải từ 0 đến 9, bước 0.5";
        }

        s.Tr = req.Tr; s.Cc = req.Cc; s.Fc = req.Fc; s.Lr = req.Lr; s.Gra = req.Gra; s.P = req.P;

        var values = criteria.Select(c => map[c]).ToList();
        s.OverallBand = values.All(v => v != null) ? IeltsBands.Overall(values.Select(v => v!.Value)) : null;
        return null;
    }

    // PUT api/teacher/grading/submissions/21  -> lưu nháp (học viên chưa thấy điểm)
    [HttpPut("submissions/{id}")]
    public async Task<IActionResult> SaveDraft(int id, GradeRequest req)
    {
        var s = await LoadForGradingAsync(id);
        if (s == null) return Error(404, "Không tìm thấy bài nộp");
        if (s.Assignment.Type == "Quiz") return Error(409, "Bài trắc nghiệm / điền từ được hệ thống tự chấm");
        if (!await IsLatestAttemptAsync(s)) return Error(409, "Học viên đã nộp lại, hãy chấm lần nộp mới nhất");

        var error = ApplyScores(s, req);
        if (error != null) return Error(400, error);

        if (req.Annotations != null)
        {
            if (s.Assignment.Type != "Writing" && req.Annotations.Count > 0)
                return Error(400, "Chỉ highlight lỗi được trên bài Writing");

            var text = s.Content ?? "";
            foreach (var x in req.Annotations)
                if (x.EndOffset > text.Length)
                    return Error(400, "Vị trí highlight vượt quá độ dài bài viết");

            Db.SubmissionAnnotations.RemoveRange(s.Annotations);
            s.Annotations = req.Annotations.OrderBy(x => x.StartOffset).Select(x => new SubmissionAnnotation
            {
                StartOffset = x.StartOffset,
                EndOffset = x.EndOffset,
                SelectedText = text.Substring(x.StartOffset, x.EndOffset - x.StartOffset),
                ErrorType = x.ErrorType,
                Suggestion = x.Suggestion?.Trim(),
                Comment = x.Comment?.Trim()
            }).ToList();
        }

        s.Feedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim();
        s.FeedbackAudioUrl = string.IsNullOrWhiteSpace(req.FeedbackAudioUrl) ? null : req.FeedbackAudioUrl.Trim();
        s.GradedAt = AppTime.Now;
        if (s.Status == "Submitted") s.Status = "Draft";   // đã công bố / đã trả bài thì giữ trạng thái, chỉ cập nhật điểm

        await Db.SaveChangesAsync();

        if (s.Status == "Published" && s.Assignment.ExamId != null)
            await _submissions.SyncExamResultAsync(s.Assignment.ExamId.Value, s.StudentId);

        return Ok(new
        {
            s.Id, s.Status, s.OverallBand, scores = new { s.Tr, s.Cc, s.Fc, s.Lr, s.Gra, s.P },
            message = s.Status == "Draft" ? "Đã lưu nháp" : "Đã cập nhật điểm"
        });
    }

    // POST api/teacher/grading/submissions/21/publish  -> Công bố: học viên mới thấy điểm
    [HttpPost("submissions/{id}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        var s = await LoadForGradingAsync(id);
        if (s == null) return Error(404, "Không tìm thấy bài nộp");
        if (s.Assignment.Type == "Quiz") return Error(409, "Bài trắc nghiệm / điền từ đã được công bố tự động");
        if (s.Status == "Published") return Error(409, "Bài đã được công bố");
        if (!await IsLatestAttemptAsync(s)) return Error(409, "Học viên đã nộp lại, hãy chấm lần nộp mới nhất");
        if (s.OverallBand == null)
            return Error(400, $"Hãy chấm đủ 4 tiêu chí ({string.Join(", ", IeltsBands.CriteriaOf(s.Assignment.Skill))}) trước khi công bố");

        var now = AppTime.Now;
        s.Status = "Published";
        s.PublishedAt = now;
        s.GradedAt ??= now;

        _notify.Add(s.Student.UserId, "Grade", $"Bài \"{s.Assignment.Title}\" đã có điểm",
            $"Band tổng: {s.OverallBand:0.0}", $"/student/assignments/{s.AssignmentId}", UserId);

        await Db.SaveChangesAsync();

        if (s.Assignment.ExamId != null)
            await _submissions.SyncExamResultAsync(s.Assignment.ExamId.Value, s.StudentId);

        return Ok(new { s.Id, s.Status, s.OverallBand, s.PublishedAt, nextSubmissionId = await NextPendingIdAsync(id, null, null) });
    }

    // POST api/teacher/grading/submissions/21/return  -> trả bài cho học viên sửa và nộp lại (lần 2, lần 3...)
    [HttpPost("submissions/{id}/return")]
    public async Task<IActionResult> Return(int id, ReturnSubmissionRequest req)
    {
        var s = await LoadForGradingAsync(id);
        if (s == null) return Error(404, "Không tìm thấy bài nộp");
        if (s.Assignment.Type == "Quiz") return Error(409, "Không trả bài trắc nghiệm / điền từ");
        if (s.Status == "Returned") return Error(409, "Bài đã được trả cho học viên sửa");
        if (!await IsLatestAttemptAsync(s)) return Error(409, "Học viên đã nộp lại, hãy chấm lần nộp mới nhất");

        var now = AppTime.Now;
        s.Status = "Returned";
        s.ReturnNote = req.Message.Trim();
        s.GradedAt ??= now;
        s.PublishedAt ??= now;   // học viên thấy nhận xét + điểm (nếu có) của lần nộp này

        _notify.Add(s.Student.UserId, "Grade", $"Bài \"{s.Assignment.Title}\" được trả lại để sửa",
            s.ReturnNote, $"/student/assignments/{s.AssignmentId}", UserId);

        await Db.SaveChangesAsync();
        return Ok(new { s.Id, s.Status, s.ReturnNote, nextSubmissionId = await NextPendingIdAsync(id, null, null) });
    }

    // GET api/teacher/grading/band-descriptors?skill=Writing  -> mô tả band khi chọn điểm
    [HttpGet("band-descriptors")]
    public IActionResult BandDescriptors(string skill = "Writing")
    {
        if (skill != "Writing" && skill != "Speaking") return Error(400, "skill phải là Writing hoặc Speaking");
        return Ok(IeltsBands.DescriptorTable(skill));
    }

    // ---------- Ngân hàng nhận xét mẫu ----------

    // GET api/teacher/grading/comment-templates?skill=Writing&criterion=TR
    [HttpGet("comment-templates")]
    public async Task<IActionResult> GetTemplates(string? skill, string? criterion)
    {
        var q = Db.CommentTemplates.Where(t => t.TeacherId == null || t.TeacherId == TeacherId);
        if (!string.IsNullOrWhiteSpace(skill)) q = q.Where(t => t.Skill == skill);
        if (!string.IsNullOrWhiteSpace(criterion)) q = q.Where(t => t.Criterion == criterion || t.Criterion == null);

        var items = await q.OrderBy(t => t.Skill).ThenBy(t => t.Criterion).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.Skill, t.Criterion, t.Content, isMine = t.TeacherId != null })
            .ToListAsync();
        return Ok(items);
    }

    // POST api/teacher/grading/comment-templates
    [HttpPost("comment-templates")]
    public async Task<IActionResult> CreateTemplate(CommentTemplateRequest req)
    {
        if (req.Criterion != null && !IeltsBands.CriteriaOf(req.Skill).Contains(req.Criterion))
            return Error(400, $"Tiêu chí {req.Criterion} không thuộc kỹ năng {req.Skill}");

        var t = new CommentTemplate
        {
            TeacherId = TeacherId,
            Skill = req.Skill,
            Criterion = req.Criterion,
            Content = req.Content.Trim()
        };
        Db.CommentTemplates.Add(t);
        await Db.SaveChangesAsync();
        return StatusCode(201, new { t.Id, t.Skill, t.Criterion, t.Content, isMine = true });
    }

    // DELETE api/teacher/grading/comment-templates/4 (chỉ xóa mẫu của mình)
    [HttpDelete("comment-templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var t = await Db.CommentTemplates.FirstOrDefaultAsync(x => x.Id == id && x.TeacherId == TeacherId);
        if (t == null) return Error(404, "Không tìm thấy nhận xét mẫu (mẫu chung của hệ thống không xóa được)");
        Db.CommentTemplates.Remove(t);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
