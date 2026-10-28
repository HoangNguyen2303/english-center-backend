using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 7 - Kiểm tra & Sổ điểm.
// Phần thi online: tạo bài tập (POST api/teacher/assignments) với examId = id bài kiểm tra.
// Câu trắc nghiệm hệ thống tự chấm, phần Writing/Speaking chấm ở page Chấm bài; điểm tự đổ về bài kiểm tra.
[Route("api/teacher")]
public class ExamsController : TeacherControllerBase
{
    private readonly SubmissionService _submissions;
    private readonly GradebookService _gradebook;

    public ExamsController(AppDbContext db, SubmissionService submissions, GradebookService gradebook) : base(db)
    {
        _submissions = submissions;
        _gradebook = gradebook;
    }

    // GET api/teacher/exams?classId=&type=Periodic|Midterm|Final
    [HttpGet("exams")]
    public async Task<IActionResult> GetAll(int? classId, string? type)
    {
        var q = Db.Exams.Where(x => x.Class.TeacherId == TeacherId);
        if (classId != null) q = q.Where(x => x.ClassId == classId);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);

        var items = await q.OrderByDescending(x => x.ExamDate)
            .Select(x => new
            {
                x.Id, x.ClassId, ClassName = x.Class.Name, x.Title, x.Type, x.Skill, x.ExamDate,
                x.DurationMinutes, x.MaxScore, x.CreatedAt,
                StudentCount = x.Class.Enrollments.Count(e => e.Status != "Dropped"),
                ScoredCount = x.Results.Count(r => r.Score != null),
                AverageScore = x.Results.Where(r => r.Score != null).Average(r => r.Score),
                OnlinePartCount = x.OnlineParts.Count
            })
            .ToListAsync();
        return Ok(items);
    }

    // GET api/teacher/exams/3
    [HttpGet("exams/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var x = await Db.Exams.Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == id && e.Class.TeacherId == TeacherId);
        if (x == null) return Error(404, "Không tìm thấy bài kiểm tra");

        var students = await Db.Enrollments
            .Where(e => e.ClassId == x.ClassId && e.Status != "Dropped")
            .OrderBy(e => e.Student.FullName)
            .Select(e => new { e.StudentId, e.Student.Code, e.Student.FullName, e.Status })
            .ToListAsync();
        var results = await Db.ExamResults.Where(r => r.ExamId == id).ToListAsync();

        var parts = await Db.Assignments.Where(a => a.ExamId == id)
            .Select(a => new
            {
                a.Id, a.Title, a.Skill, a.Type, a.DueAt,
                SubmittedCount = a.Submissions.Select(s => s.StudentId).Distinct().Count(),
                PendingGrading = a.Submissions.Count(s => s.Status == "Submitted" || s.Status == "Draft")
            })
            .ToListAsync();

        return Ok(new
        {
            x.Id, x.ClassId, ClassName = x.Class.Name, x.Title, x.Type, x.Skill, x.ExamDate,
            x.DurationMinutes, x.MaxScore, x.CreatedAt,
            onlineParts = parts,
            results = students.Select(st =>
            {
                var r = results.FirstOrDefault(y => y.StudentId == st.StudentId);
                return new
                {
                    st.StudentId, st.Code, st.FullName, enrollmentStatus = st.Status,
                    score = r?.Score, source = r?.Source, note = r?.Note, updatedAt = r?.UpdatedAt
                };
            })
        });
    }

    // POST api/teacher/exams  (+ Tạo bài kiểm tra)
    [HttpPost("exams")]
    public async Task<IActionResult> Create(ExamRequest req)
    {
        if (!await OwnsClassAsync(req.ClassId)) return Error(400, "Lớp không tồn tại hoặc bạn không phụ trách lớp này");
        if (req.ExamDate == default) return Error(400, "Ngày thi là bắt buộc");

        var x = new Exam
        {
            ClassId = req.ClassId,
            TeacherId = TeacherId,
            Title = req.Title.Trim(),
            Type = req.Type,
            Skill = req.Skill,
            ExamDate = req.ExamDate,
            DurationMinutes = req.DurationMinutes,
            MaxScore = req.MaxScore,
            CreatedAt = AppTime.Now
        };
        Db.Exams.Add(x);
        await Db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = x.Id }, new { x.Id, message = "Đã tạo bài kiểm tra" });
    }

    // PUT api/teacher/exams/3
    [HttpPut("exams/{id}")]
    public async Task<IActionResult> Update(int id, ExamRequest req)
    {
        var x = await Db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.Class.TeacherId == TeacherId);
        if (x == null) return Error(404, "Không tìm thấy bài kiểm tra");

        if (req.ClassId != x.ClassId &&
            (await Db.ExamResults.AnyAsync(r => r.ExamId == id) || await Db.Assignments.AnyAsync(a => a.ExamId == id)))
            return Error(409, "Bài kiểm tra đã có điểm hoặc phần thi online, không thể đổi lớp");
        if (req.ClassId != x.ClassId && !await OwnsClassAsync(req.ClassId))
            return Error(400, "Lớp không tồn tại hoặc bạn không phụ trách lớp này");

        var maxEntered = await Db.ExamResults.Where(r => r.ExamId == id && r.Score != null).MaxAsync(r => r.Score);
        if (maxEntered != null && maxEntered > req.MaxScore)
            return Error(409, $"Đã có điểm {maxEntered:0.##}, điểm tối đa không được nhỏ hơn");

        x.ClassId = req.ClassId;
        x.Title = req.Title.Trim();
        x.Type = req.Type;
        x.Skill = req.Skill;
        x.ExamDate = req.ExamDate;
        x.DurationMinutes = req.DurationMinutes;
        var maxChanged = x.MaxScore != req.MaxScore;
        x.MaxScore = req.MaxScore;
        await Db.SaveChangesAsync();

        if (maxChanged) await SyncAllAsync(x);   // điểm online tính theo điểm tối đa mới
        return Ok(new { x.Id, message = "Đã cập nhật bài kiểm tra" });
    }

    // DELETE api/teacher/exams/3
    [HttpDelete("exams/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var x = await Db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.Class.TeacherId == TeacherId);
        if (x == null) return Error(404, "Không tìm thấy bài kiểm tra");

        if (await Db.ExamResults.AnyAsync(r => r.ExamId == id && r.Score != null))
            return Error(409, "Bài kiểm tra đã có điểm, không thể xóa");
        if (await Db.Assignments.AnyAsync(a => a.ExamId == id))
            return Error(409, "Bài kiểm tra còn phần thi online, hãy xóa phần thi online trước");

        Db.Exams.Remove(x);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // PUT api/teacher/exams/3/results  -> nhập điểm thủ công (bài thi trên giấy)
    [HttpPut("exams/{id}/results")]
    public async Task<IActionResult> SaveResults(int id, ExamResultsRequest req)
    {
        var x = await Db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.Class.TeacherId == TeacherId);
        if (x == null) return Error(404, "Không tìm thấy bài kiểm tra");

        if (req.Items.GroupBy(i => i.StudentId).Any(g => g.Count() > 1))
            return Error(400, "Một học viên xuất hiện nhiều lần trong danh sách");

        var over = req.Items.FirstOrDefault(i => i.Score > x.MaxScore);
        if (over != null) return Error(400, $"Điểm không được vượt quá điểm tối đa {x.MaxScore:0.##}");

        var ids = req.Items.Select(i => i.StudentId).ToList();
        var valid = await Db.Enrollments.Where(e => e.ClassId == x.ClassId && ids.Contains(e.StudentId))
            .Select(e => e.StudentId).ToListAsync();
        var invalid = ids.Except(valid).ToList();
        if (invalid.Count > 0) return Error(400, $"Học viên không thuộc lớp (id: {string.Join(", ", invalid)})");

        var existing = await Db.ExamResults.Where(r => r.ExamId == id).ToListAsync();
        var now = AppTime.Now;
        foreach (var item in req.Items)
        {
            var r = existing.FirstOrDefault(y => y.StudentId == item.StudentId);
            if (r == null)
            {
                Db.ExamResults.Add(new ExamResult
                {
                    ExamId = id,
                    StudentId = item.StudentId,
                    Score = item.Score,
                    Source = "Manual",
                    Note = item.Note?.Trim(),
                    UpdatedAt = now
                });
            }
            else
            {
                r.Score = item.Score;
                r.Note = item.Note?.Trim();
                r.Source = "Manual";
                r.UpdatedAt = now;
            }
        }
        await Db.SaveChangesAsync();
        return Ok(new { message = "Đã lưu điểm" });
    }

    // POST api/teacher/exams/3/sync  -> tính lại điểm từ các phần thi online (bỏ qua điểm đã nhập tay)
    [HttpPost("exams/{id}/sync")]
    public async Task<IActionResult> Sync(int id, bool overwriteManual = false)
    {
        var x = await Db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.Class.TeacherId == TeacherId);
        if (x == null) return Error(404, "Không tìm thấy bài kiểm tra");
        if (!await Db.Assignments.AnyAsync(a => a.ExamId == id)) return Error(409, "Bài kiểm tra chưa có phần thi online");

        if (overwriteManual)
        {
            var manual = await Db.ExamResults.Where(r => r.ExamId == id && r.Source == "Manual").ToListAsync();
            foreach (var r in manual) r.Source = "Online";
            await Db.SaveChangesAsync();
        }

        await SyncAllAsync(x);
        return Ok(new { message = "Đã đồng bộ điểm phần thi online" });
    }

    private async Task SyncAllAsync(Exam x)
    {
        var studentIds = await Db.Enrollments.Where(e => e.ClassId == x.ClassId).Select(e => e.StudentId).ToListAsync();
        foreach (var sid in studentIds)
            await _submissions.SyncExamResultAsync(x.Id, sid);
    }

    // ---------- Sổ điểm ----------

    // GET api/teacher/classes/5/gradebook
    [HttpGet("classes/{classId}/gradebook")]
    public async Task<IActionResult> Gradebook(int classId)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");
        var book = await _gradebook.BuildAsync(classId);
        return Ok(book);
    }

    // GET api/teacher/classes/5/gradebook/export  -> tải file Excel
    [HttpGet("classes/{classId}/gradebook/export")]
    public async Task<IActionResult> Export(int classId)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");
        var book = await _gradebook.BuildAsync(classId);
        var bytes = GradebookService.ToExcel(book!);
        var fileName = $"SoDiem_{Slug(book!.ClassName)}_{AppTime.Today:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static string Slug(string s)
    {
        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        var ascii = new string(chars).Replace('đ', 'd').Replace('Đ', 'D');
        return new string(ascii.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
    }

    // GET api/teacher/classes/5/grade-config
    [HttpGet("classes/{classId}/grade-config")]
    public async Task<IActionResult> GetConfig(int classId)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");
        var c = await _gradebook.GetConfigAsync(classId);
        return Ok(new { c.ClassId, c.AssignmentWeight, c.PeriodicWeight, c.MidtermWeight, c.FinalWeight, c.PassScore });
    }

    // PUT api/teacher/classes/5/grade-config  (ví dụ bài tập 20%, giữa kỳ 30%, cuối kỳ 50%)
    [HttpPut("classes/{classId}/grade-config")]
    public async Task<IActionResult> SaveConfig(int classId, GradeConfigRequest req)
    {
        if (!await OwnsClassAsync(classId)) return Error(404, "Không tìm thấy lớp học");

        var c = await Db.GradeConfigs.FirstOrDefaultAsync(x => x.ClassId == classId);
        if (c == null)
        {
            c = new GradeConfig { ClassId = classId };
            Db.GradeConfigs.Add(c);
        }
        c.AssignmentWeight = req.AssignmentWeight;
        c.PeriodicWeight = req.PeriodicWeight;
        c.MidtermWeight = req.MidtermWeight;
        c.FinalWeight = req.FinalWeight;
        c.PassScore = req.PassScore;
        await Db.SaveChangesAsync();

        return Ok(new { c.ClassId, c.AssignmentWeight, c.PeriodicWeight, c.MidtermWeight, c.FinalWeight, c.PassScore });
    }
}
