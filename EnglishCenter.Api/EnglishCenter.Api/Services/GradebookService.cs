using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

public class GradebookColumn
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";        // Assignment | Exam
    public string Title { get; set; } = "";
    public string? Type { get; set; }             // Exam: Periodic | Midterm | Final; Assignment: Writing | Speaking | Quiz
    public string? Skill { get; set; }
    public DateTime Date { get; set; }
    public decimal MaxScore { get; set; }         // thang điểm gốc (bài tập quy về 10)
}

public class GradebookCell
{
    public int ColumnId { get; set; }
    public string Kind { get; set; } = "";
    public decimal? Score { get; set; }           // điểm gốc (exam) hoặc thang 10 (bài tập)
    public decimal? Score10 { get; set; }         // quy về thang 10
    public decimal? Band { get; set; }            // band IELTS nếu là bài Writing/Speaking
    public bool Missing { get; set; }             // quá hạn chưa nộp / chưa có điểm bài thi đã diễn ra
}

public class GradebookRow
{
    public int StudentId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string EnrollmentStatus { get; set; } = "";
    public List<GradebookCell> Cells { get; set; } = new();
    public decimal? AssignmentAverage { get; set; }
    public decimal? PeriodicAverage { get; set; }
    public decimal? MidtermAverage { get; set; }
    public decimal? FinalAverage { get; set; }
    public decimal? FinalScore { get; set; }      // điểm tổng kết thang 10
    public string? Result { get; set; }           // Pass | Fail | null (chưa đủ dữ liệu)
}

public class Gradebook
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public GradeConfig Config { get; set; } = new();
    public List<GradebookColumn> Columns { get; set; } = new();
    public List<GradebookRow> Rows { get; set; } = new();
}

// Sổ điểm theo lớp: điểm bài tập, các bài kiểm tra, điểm tổng kết theo trọng số
public class GradebookService
{
    private readonly AppDbContext _db;

    public GradebookService(AppDbContext db)
    {
        _db = db;
    }

    public static GradeConfig DefaultConfig(int classId) => new()
    {
        ClassId = classId,
        AssignmentWeight = 20,
        PeriodicWeight = 0,
        MidtermWeight = 30,
        FinalWeight = 50,
        PassScore = 5
    };

    public async Task<GradeConfig> GetConfigAsync(int classId) =>
        await _db.GradeConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId) ?? DefaultConfig(classId);

    public async Task<Gradebook?> BuildAsync(int classId, int? onlyStudentId = null)
    {
        var cls = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return null;

        var now = AppTime.Now;
        var config = await GetConfigAsync(classId);

        var studentsQuery = _db.Enrollments.Where(e => e.ClassId == classId && e.Status != "Dropped");
        if (onlyStudentId != null) studentsQuery = studentsQuery.Where(e => e.StudentId == onlyStudentId);
        var students = await studentsQuery
            .OrderBy(e => e.Student.FullName)
            .Select(e => new { e.StudentId, e.Student.Code, e.Student.FullName, e.Status })
            .ToListAsync();
        var studentIds = students.Select(s => s.StudentId).ToList();

        // Bài tập thường (không phải phần thi online của bài kiểm tra)
        var assignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ClassId == classId && a.ExamId == null)
            .OrderBy(a => a.DueAt)
            .Select(a => new { a.Id, a.Title, a.Type, a.Skill, a.DueAt, a.AssignToAll, Targets = a.Targets.Select(t => t.StudentId).ToList() })
            .ToListAsync();
        var assignmentIds = assignments.Select(a => a.Id).ToList();

        var subs = await _db.Submissions.AsNoTracking()
            .Where(s => assignmentIds.Contains(s.AssignmentId) && studentIds.Contains(s.StudentId))
            .ToListAsync();
        var latestSub = subs.GroupBy(s => (s.AssignmentId, s.StudentId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AttemptNo).First());

        var exams = await _db.Exams.AsNoTracking()
            .Where(x => x.ClassId == classId)
            .OrderBy(x => x.ExamDate)
            .ToListAsync();
        var examIds = exams.Select(x => x.Id).ToList();
        var results = await _db.ExamResults.AsNoTracking()
            .Where(r => examIds.Contains(r.ExamId) && studentIds.Contains(r.StudentId))
            .ToListAsync();
        var resultMap = results.ToDictionary(r => (r.ExamId, r.StudentId));

        var book = new Gradebook { ClassId = classId, ClassName = cls.Name, Config = config };

        foreach (var a in assignments)
            book.Columns.Add(new GradebookColumn
            {
                Id = a.Id, Kind = "Assignment", Title = a.Title, Type = a.Type, Skill = a.Skill,
                Date = a.DueAt, MaxScore = 10
            });
        foreach (var x in exams)
            book.Columns.Add(new GradebookColumn
            {
                Id = x.Id, Kind = "Exam", Title = x.Title, Type = x.Type, Skill = x.Skill,
                Date = x.ExamDate.ToDateTime(TimeOnly.MinValue), MaxScore = x.MaxScore
            });

        // Loại điểm nào lớp đã có dữ liệu thì mới tính vào tổng kết (trọng số được chia lại theo tỉ lệ)
        var hasAssignmentData = assignments.Any(a => a.DueAt < now)
            || subs.Any(s => s.Status == "Published" || s.Status == "Returned");
        var examTypesWithData = exams.Where(x => results.Any(r => r.ExamId == x.Id && r.Score != null))
            .Select(x => x.Type).ToHashSet();

        foreach (var st in students)
        {
            var row = new GradebookRow
            {
                StudentId = st.StudentId,
                Code = st.Code,
                FullName = st.FullName,
                EnrollmentStatus = st.Status
            };

            var assignmentScores = new List<decimal>();
            foreach (var a in assignments)
            {
                var targeted = a.AssignToAll || a.Targets.Contains(st.StudentId);
                var cell = new GradebookCell { ColumnId = a.Id, Kind = "Assignment" };
                if (targeted)
                {
                    latestSub.TryGetValue((a.Id, st.StudentId), out var s);
                    var ratio = s == null ? null : SubmissionService.Ratio(s, a.Type);
                    if (ratio != null)
                    {
                        cell.Score10 = cell.Score = Math.Round(ratio.Value * 10, 2);
                        cell.Band = a.Type == "Quiz" ? null : s!.OverallBand;
                        assignmentScores.Add(cell.Score10.Value);
                    }
                    else if (s == null && a.DueAt < now)
                    {
                        cell.Missing = true;
                        cell.Score10 = 0;
                        assignmentScores.Add(0);
                    }
                }
                row.Cells.Add(cell);
            }

            var byType = new Dictionary<string, List<decimal>>
            {
                ["Periodic"] = new(), ["Midterm"] = new(), ["Final"] = new()
            };
            foreach (var x in exams)
            {
                var cell = new GradebookCell { ColumnId = x.Id, Kind = "Exam" };
                if (resultMap.TryGetValue((x.Id, st.StudentId), out var r) && r.Score != null)
                {
                    cell.Score = r.Score;
                    cell.Score10 = Math.Round(r.Score.Value / x.MaxScore * 10, 2);
                    byType[x.Type].Add(cell.Score10.Value);
                }
                else if (examTypesWithData.Contains(x.Type) && x.ExamDate < AppTime.Today)
                {
                    cell.Missing = true;
                    byType[x.Type].Add(0);
                }
                row.Cells.Add(cell);
            }

            row.AssignmentAverage = Avg(assignmentScores);
            row.PeriodicAverage = Avg(byType["Periodic"]);
            row.MidtermAverage = Avg(byType["Midterm"]);
            row.FinalAverage = Avg(byType["Final"]);

            var parts = new List<(decimal? Score, decimal Weight)>
            {
                (hasAssignmentData ? row.AssignmentAverage ?? 0 : null, config.AssignmentWeight),
                (examTypesWithData.Contains("Periodic") ? row.PeriodicAverage ?? 0 : null, config.PeriodicWeight),
                (examTypesWithData.Contains("Midterm") ? row.MidtermAverage ?? 0 : null, config.MidtermWeight),
                (examTypesWithData.Contains("Final") ? row.FinalAverage ?? 0 : null, config.FinalWeight)
            }.Where(p => p.Score != null && p.Weight > 0).ToList();

            var totalWeight = parts.Sum(p => p.Weight);
            if (totalWeight > 0)
            {
                row.FinalScore = Math.Round(parts.Sum(p => p.Score!.Value * p.Weight) / totalWeight, 2);
                row.Result = row.FinalScore >= config.PassScore ? "Pass" : "Fail";
            }

            book.Rows.Add(row);
        }

        return book;
    }

    private static decimal? Avg(List<decimal> list) => list.Count == 0 ? null : Math.Round(list.Average(), 2);

    // Xuất sổ điểm ra file Excel (.xlsx)
    public static byte[] ToExcel(Gradebook book)
    {
        var header = new List<string> { "Mã HV", "Họ tên", "Trạng thái" };
        header.AddRange(book.Columns.Select(c => c.Kind == "Exam" ? $"{c.Title} (/{c.MaxScore:0.##})" : $"{c.Title} (/10)"));
        header.AddRange(new[] { "TB bài tập", "TB định kỳ", "TB giữa kỳ", "TB cuối kỳ", "Tổng kết (/10)", "Kết quả" });

        var rows = new List<List<object?>>();
        foreach (var r in book.Rows)
        {
            var line = new List<object?> { r.Code, r.FullName, r.EnrollmentStatus };
            foreach (var c in r.Cells)
                line.Add(c.Missing ? "Thiếu" : c.Score);
            line.Add(r.AssignmentAverage);
            line.Add(r.PeriodicAverage);
            line.Add(r.MidtermAverage);
            line.Add(r.FinalAverage);
            line.Add(r.FinalScore);
            line.Add(r.Result == "Pass" ? "Đạt" : r.Result == "Fail" ? "Không đạt" : "");
            rows.Add(line);
        }

        var cfg = book.Config;
        rows.Add(new List<object?>());
        rows.Add(new List<object?> { "Trọng số", $"Bài tập {cfg.AssignmentWeight:0.##}%, Định kỳ {cfg.PeriodicWeight:0.##}%, Giữa kỳ {cfg.MidtermWeight:0.##}%, Cuối kỳ {cfg.FinalWeight:0.##}%; Đạt từ {cfg.PassScore:0.##}/10" });

        return XlsxWriter.Build("So diem", header, rows);
    }
}
