using System.Text.Json;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 5 - Bài tập. Bài học viên nộp chuyển sang page Chấm bài.
[Route("api/teacher/assignments")]
public class AssignmentsController : TeacherControllerBase
{
    private readonly SubmissionService _submissions;
    private readonly NotificationService _notify;

    public AssignmentsController(AppDbContext db, SubmissionService submissions, NotificationService notify) : base(db)
    {
        _submissions = submissions;
        _notify = notify;
    }

    // GET api/teacher/assignments?classId=&skill=&state=open|closed&includeExamParts=false
    [HttpGet]
    public async Task<IActionResult> GetAll(int? classId, string? skill, string? state, bool includeExamParts = false)
    {
        var now = AppTime.Now;
        var query = Db.Assignments.Where(a => a.Class.TeacherId == TeacherId);

        if (classId != null) query = query.Where(a => a.ClassId == classId);
        if (!string.IsNullOrWhiteSpace(skill)) query = query.Where(a => a.Skill == skill);
        if (!includeExamParts) query = query.Where(a => a.ExamId == null);
        if (!string.IsNullOrWhiteSpace(state))
        {
            switch (state.Trim().ToLower())
            {
                case "open": query = query.Where(a => a.DueAt >= now); break;      // còn hạn
                case "closed": query = query.Where(a => a.DueAt < now); break;     // hết hạn
                default: return Error(400, "state phải là open hoặc closed");
            }
        }

        var items = await query
            .OrderByDescending(a => a.DueAt)
            .Select(a => new
            {
                a.Id, a.Title, a.ClassId, ClassName = a.Class.Name, a.Skill, a.Type, a.DueAt,
                a.AllowLate, a.AssignToAll, a.ExamId, a.CreatedAt,
                ActiveCount = a.Class.Enrollments.Count(e => e.Status == "Active"),
                TargetCount = a.Targets.Count(t => a.Class.Enrollments.Any(e => e.StudentId == t.StudentId && e.Status == "Active")),
                SubmittedCount = a.Submissions.Select(s => s.StudentId).Distinct().Count(),
                PendingGrading = a.Submissions.Count(s => s.Status == "Submitted" || s.Status == "Draft")
            })
            .ToListAsync();

        return Ok(items.Select(a => new
        {
            a.Id, a.Title, a.ClassId, a.ClassName, a.Skill, a.Type, a.DueAt, a.AllowLate, a.AssignToAll,
            a.ExamId, a.CreatedAt,
            isOpen = a.DueAt >= now,
            assignedCount = a.AssignToAll ? a.ActiveCount : a.TargetCount,
            a.SubmittedCount,
            a.PendingGrading
        }));
    }

    // GET api/teacher/assignments/7
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var a = await Db.Assignments
            .Include(x => x.Class)
            .Include(x => x.Questions)
            .Include(x => x.Targets).ThenInclude(t => t.Student)
            .FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (a == null) return Error(404, "Không tìm thấy bài tập");

        var assigned = await _submissions.AssignedStudentIdsAsync(a);
        var submitted = await Db.Submissions.Where(s => s.AssignmentId == id)
            .Select(s => s.StudentId).Distinct().CountAsync();

        return Ok(new
        {
            a.Id, a.ClassId, ClassName = a.Class.Name, a.Title, a.Skill, a.Type, a.Prompt, a.DueAt,
            a.MinWords, a.AttachmentUrl, a.Note, a.AllowLate, a.AssignToAll, a.ExamId, a.CreatedAt, a.UpdatedAt,
            isOpen = a.DueAt >= AppTime.Now,
            assignedCount = assigned.Count,
            submittedCount = submitted,
            students = a.AssignToAll ? null : a.Targets.Select(t => new { t.StudentId, t.Student.Code, t.Student.FullName }),
            questions = a.Questions.OrderBy(q => q.OrderNo).Select(q => new
            {
                q.Id, q.OrderNo, q.QuestionType, q.Content,
                options = string.IsNullOrEmpty(q.OptionsJson) ? null : JsonSerializer.Deserialize<List<string>>(q.OptionsJson),
                q.CorrectAnswer, q.Points
            })
        });
    }

    // Kiểm tra lớp, học viên được chọn, bài kiểm tra được gắn
    private async Task<string?> ValidateRefsAsync(AssignmentRequest req)
    {
        if (!await OwnsClassAsync(req.ClassId)) return "Lớp không tồn tại hoặc bạn không phụ trách lớp này";

        if (!req.AssignToAll)
        {
            var ids = req.StudentIds.Distinct().ToList();
            var valid = await Db.Enrollments.CountAsync(e => e.ClassId == req.ClassId && e.Status == "Active" && ids.Contains(e.StudentId));
            if (valid != ids.Count) return "Có học viên được chọn không thuộc lớp hoặc không còn đang học";
        }

        if (req.ExamId != null && !await Db.Exams.AnyAsync(x => x.Id == req.ExamId && x.ClassId == req.ClassId))
            return "Bài kiểm tra được gắn không thuộc lớp này";

        return null;
    }

    private static List<AssignmentQuestion> BuildQuestions(AssignmentRequest req) =>
        req.Questions.Select((q, i) => new AssignmentQuestion
        {
            OrderNo = i + 1,
            QuestionType = q.QuestionType,
            Content = q.Content.Trim(),
            OptionsJson = q.QuestionType == "MultipleChoice" && q.Options != null ? JsonSerializer.Serialize(q.Options) : null,
            CorrectAnswer = q.CorrectAnswer.Trim(),
            Points = q.Points
        }).ToList();

    // POST api/teacher/assignments  (+ Giao bài tập)
    [HttpPost]
    public async Task<IActionResult> Create(AssignmentRequest req)
    {
        var error = await ValidateRefsAsync(req);
        if (error != null) return Error(400, error);
        if (req.DueAt <= AppTime.Now) return Error(400, "Hạn nộp phải sau thời điểm hiện tại");

        var a = new Assignment
        {
            ClassId = req.ClassId,
            TeacherId = TeacherId,
            ExamId = req.ExamId,
            Title = req.Title.Trim(),
            Skill = req.Skill,
            Type = req.Type,
            Prompt = req.Prompt.Trim(),
            DueAt = req.DueAt,
            MinWords = req.MinWords,
            AttachmentUrl = req.AttachmentUrl?.Trim(),
            Note = req.Note?.Trim(),
            AllowLate = req.AllowLate,
            AssignToAll = req.AssignToAll,
            CreatedAt = AppTime.Now,
            Questions = req.Type == "Quiz" ? BuildQuestions(req) : new(),
            Targets = req.AssignToAll ? new() : req.StudentIds.Distinct().Select(id => new AssignmentStudent { StudentId = id }).ToList()
        };
        Db.Assignments.Add(a);

        // Thông báo cho học viên được giao
        var studentIds = req.AssignToAll
            ? await Db.Enrollments.Where(e => e.ClassId == req.ClassId && e.Status == "Active").Select(e => e.StudentId).ToListAsync()
            : req.StudentIds.Distinct().ToList();
        var userIds = await Db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.UserId).ToListAsync();
        _notify.Add(userIds, "Announcement", $"Bài tập mới: {a.Title}",
            $"Hạn nộp {a.DueAt:HH:mm dd/MM/yyyy}", "/student/assignments", UserId);

        await Db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = a.Id }, new { a.Id, message = "Đã giao bài tập" });
    }

    // PUT api/teacher/assignments/7
    // Đã có học viên nộp thì không đổi được lớp, kỹ năng, loại bài, câu hỏi
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, AssignmentRequest req)
    {
        var a = await Db.Assignments
            .Include(x => x.Questions)
            .Include(x => x.Targets)
            .FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (a == null) return Error(404, "Không tìm thấy bài tập");

        var error = await ValidateRefsAsync(req);
        if (error != null) return Error(400, error);

        var hasSubmissions = await Db.Submissions.AnyAsync(s => s.AssignmentId == id);
        if (hasSubmissions)
        {
            if (a.ClassId != req.ClassId || a.Skill != req.Skill || a.Type != req.Type)
                return Error(409, "Đã có học viên nộp bài, không thể đổi lớp, kỹ năng hoặc loại bài");
            if (a.Type == "Quiz" && QuestionsChanged(a.Questions, req.Questions))
                return Error(409, "Đã có học viên nộp bài, không thể sửa câu hỏi / đáp án");
        }

        a.ClassId = req.ClassId;
        a.ExamId = req.ExamId;
        a.Title = req.Title.Trim();
        a.Skill = req.Skill;
        a.Type = req.Type;
        a.Prompt = req.Prompt.Trim();
        a.DueAt = req.DueAt;
        a.MinWords = req.MinWords;
        a.AttachmentUrl = req.AttachmentUrl?.Trim();
        a.Note = req.Note?.Trim();
        a.AllowLate = req.AllowLate;
        a.AssignToAll = req.AssignToAll;
        a.UpdatedAt = AppTime.Now;

        if (!hasSubmissions)
        {
            Db.AssignmentQuestions.RemoveRange(a.Questions);
            a.Questions = req.Type == "Quiz" ? BuildQuestions(req) : new();
        }

        // Cập nhật danh sách học viên được giao (so sánh cũ - mới để tránh trùng khóa khi xóa rồi thêm lại)
        var newIds = req.AssignToAll ? new HashSet<int>() : req.StudentIds.ToHashSet();
        foreach (var t in a.Targets.Where(t => !newIds.Contains(t.StudentId)).ToList())
            Db.AssignmentStudents.Remove(t);
        foreach (var sid in newIds.Where(sid => a.Targets.All(t => t.StudentId != sid)))
            Db.AssignmentStudents.Add(new AssignmentStudent { AssignmentId = a.Id, StudentId = sid });

        await Db.SaveChangesAsync();
        return Ok(new { a.Id, message = "Đã cập nhật bài tập" });
    }

    private static bool QuestionsChanged(List<AssignmentQuestion> old, List<QuestionRequest> next)
    {
        if (old.Count != next.Count) return true;
        var ordered = old.OrderBy(q => q.OrderNo).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var o = ordered[i];
            var n = next[i];
            if (o.QuestionType != n.QuestionType || o.Content != n.Content.Trim()
                || o.CorrectAnswer != n.CorrectAnswer.Trim() || o.Points != n.Points)
                return true;
        }
        return false;
    }

    // DELETE api/teacher/assignments/7 (chặn xóa nếu đã có học viên nộp)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await Db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (a == null) return Error(404, "Không tìm thấy bài tập");

        if (await Db.Submissions.AnyAsync(s => s.AssignmentId == id))
            return Error(409, "Đã có học viên nộp bài, không thể xóa");

        Db.Assignments.Remove(a);   // câu hỏi, danh sách học viên được giao tự xóa theo (cascade)
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // GET api/teacher/assignments/7/missing  -> học viên chưa nộp
    [HttpGet("{id}/missing")]
    public async Task<IActionResult> Missing(int id)
    {
        var a = await Db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (a == null) return Error(404, "Không tìm thấy bài tập");

        var students = await MissingStudentsAsync(a);
        return Ok(new
        {
            assignmentId = id,
            a.DueAt,
            isOverdue = a.DueAt < AppTime.Now,
            count = students.Count,
            students = students.Select(s => new { s.Id, s.Code, s.FullName, s.Phone })
        });
    }

    private async Task<List<Student>> MissingStudentsAsync(Assignment a)
    {
        var assigned = await _submissions.AssignedStudentIdsAsync(a);
        var submitted = await Db.Submissions.Where(s => s.AssignmentId == a.Id).Select(s => s.StudentId).ToListAsync();
        var missingIds = assigned.Except(submitted).ToList();
        return await Db.Students.AsNoTracking().Where(s => missingIds.Contains(s.Id)).OrderBy(s => s.FullName).ToListAsync();
    }

    // POST api/teacher/assignments/7/remind  -> nhắc học viên chưa nộp (Student nhận nhắc nhở deadline)
    [HttpPost("{id}/remind")]
    public async Task<IActionResult> Remind(int id)
    {
        var a = await Db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.Class.TeacherId == TeacherId);
        if (a == null) return Error(404, "Không tìm thấy bài tập");

        var students = await MissingStudentsAsync(a);
        if (students.Count == 0) return Ok(new { message = "Tất cả học viên đã nộp bài", count = 0 });

        if (a.DueAt < AppTime.Now && !a.AllowLate)
            return Error(409, "Bài đã hết hạn và không cho nộp trễ, không cần nhắc");

        var overdue = a.DueAt < AppTime.Now;
        _notify.Add(students.Select(s => s.UserId), "Reminder",
            overdue ? $"Bài \"{a.Title}\" đã quá hạn, bạn vẫn có thể nộp trễ" : $"Nhắc nộp bài \"{a.Title}\"",
            $"Hạn nộp {a.DueAt:HH:mm dd/MM/yyyy}", "/student/assignments", UserId);
        await Db.SaveChangesAsync();

        return Ok(new { message = $"Đã nhắc {students.Count} học viên", count = students.Count });
    }
}
