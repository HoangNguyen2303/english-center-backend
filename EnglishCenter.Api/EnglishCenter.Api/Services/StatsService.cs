using EnglishCenter.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

public class AttendanceStat
{
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public int TotalMarked { get; set; }          // số buổi đã điểm danh
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Excused { get; set; }
    public decimal? AttendanceRate { get; set; }  // % (Có mặt + Muộn) / số buổi đã điểm danh
    public decimal? AbsenceRate { get; set; }     // % (Vắng + Vắng có phép) / số buổi đã điểm danh
    public int ConsecutiveAbsences { get; set; }  // số buổi vắng liên tiếp tính đến buổi gần nhất
    public bool Warning { get; set; }             // vắng quá 20% số buổi
}

// Các số liệu thống kê dùng ở nhiều page: chuyên cần, bài chưa nộp, band gần nhất
public class StatsService
{
    public const decimal AbsenceWarningPercent = 20;

    private readonly AppDbContext _db;

    public StatsService(AppDbContext db)
    {
        _db = db;
    }

    // Thống kê chuyên cần theo (lớp, học viên)
    public async Task<Dictionary<(int ClassId, int StudentId), AttendanceStat>> AttendanceAsync(
        IEnumerable<int> classIds, int? studentId = null)
    {
        var ids = classIds.Distinct().ToList();
        var query = _db.Attendances.Where(a => ids.Contains(a.Session.ClassId) && a.Session.Status == "Scheduled");
        if (studentId != null) query = query.Where(a => a.StudentId == studentId);

        var rows = await query
            .Select(a => new { a.Session.ClassId, a.StudentId, a.Status, a.Session.Date, a.Session.StartTime })
            .ToListAsync();

        var result = new Dictionary<(int, int), AttendanceStat>();
        foreach (var g in rows.GroupBy(r => (r.ClassId, r.StudentId)))
        {
            var stat = new AttendanceStat
            {
                ClassId = g.Key.ClassId,
                StudentId = g.Key.StudentId,
                TotalMarked = g.Count(),
                Present = g.Count(x => x.Status == "Present"),
                Late = g.Count(x => x.Status == "Late"),
                Absent = g.Count(x => x.Status == "Absent"),
                Excused = g.Count(x => x.Status == "Excused")
            };
            stat.AttendanceRate = Math.Round((stat.Present + stat.Late) * 100m / stat.TotalMarked, 1);
            stat.AbsenceRate = Math.Round((stat.Absent + stat.Excused) * 100m / stat.TotalMarked, 1);
            stat.Warning = stat.AbsenceRate > AbsenceWarningPercent;

            foreach (var r in g.OrderByDescending(x => x.Date).ThenByDescending(x => x.StartTime))
            {
                if (r.Status != "Absent" && r.Status != "Excused") break;
                stat.ConsecutiveAbsences++;
            }
            result[g.Key] = stat;
        }
        return result;
    }

    // Số bài đã hết hạn mà học viên chưa nộp, theo (lớp, học viên)
    public async Task<Dictionary<(int ClassId, int StudentId), int>> MissingAssignmentsAsync(IEnumerable<int> classIds)
    {
        var ids = classIds.Distinct().ToList();
        var now = AppTime.Now;

        var assignments = await _db.Assignments
            .Where(a => ids.Contains(a.ClassId) && a.DueAt < now)
            .Select(a => new
            {
                a.Id,
                a.ClassId,
                a.AssignToAll,
                Targets = a.Targets.Select(t => t.StudentId).ToList(),
                // không dùng Distinct trong collection projection (EF Core không dịch được), Contains vẫn đúng
                Submitted = a.Submissions.Select(s => s.StudentId).ToList()
            })
            .ToListAsync();

        var enrollments = await _db.Enrollments
            .Where(e => ids.Contains(e.ClassId) && e.Status == "Active")
            .Select(e => new { e.ClassId, e.StudentId })
            .ToListAsync();

        var result = new Dictionary<(int, int), int>();
        foreach (var a in assignments)
        {
            var students = enrollments.Where(e => e.ClassId == a.ClassId).Select(e => e.StudentId);
            if (!a.AssignToAll) students = students.Where(a.Targets.Contains);

            foreach (var sid in students.Where(s => !a.Submitted.Contains(s)))
            {
                var key = (a.ClassId, sid);
                result[key] = result.GetValueOrDefault(key) + 1;
            }
        }
        return result;
    }

    // 2 band gần nhất (đã công bố) của mỗi học viên để phát hiện band giảm
    public async Task<Dictionary<int, (decimal Latest, decimal? Previous)>> LatestBandsAsync(IEnumerable<int> studentIds)
    {
        var ids = studentIds.Distinct().ToList();
        var rows = await _db.Submissions
            .Where(s => ids.Contains(s.StudentId) && s.OverallBand != null
                && (s.Status == "Published" || s.Status == "Returned"))
            .Select(s => new { s.StudentId, Band = s.OverallBand!.Value, s.PublishedAt, s.SubmittedAt })
            .ToListAsync();

        return rows.GroupBy(r => r.StudentId).ToDictionary(
            g => g.Key,
            g =>
            {
                var ordered = g.OrderByDescending(x => x.PublishedAt ?? x.SubmittedAt).ToList();
                return (ordered[0].Band, ordered.Count > 1 ? ordered[1].Band : (decimal?)null);
            });
    }
}
