using System.Net;
using System.Text;
using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.TeacherPortal;
using EnglishCenter.Api.Entities;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Page 8 - Học viên: hồ sơ tiến bộ, nhận xét định kỳ, phiếu cuối khóa, đề xuất lên lớp
[Route("api/teacher/students")]
public class TeacherStudentsController : TeacherControllerBase
{
    private readonly StatsService _stats;
    private readonly GradebookService _gradebook;
    private readonly NotificationService _notify;

    public TeacherStudentsController(AppDbContext db, StatsService stats, GradebookService gradebook,
        NotificationService notify) : base(db)
    {
        _stats = stats;
        _gradebook = gradebook;
        _notify = notify;
    }

    // GET api/teacher/students?search=&classId=
    // Danh sách học viên của tất cả lớp mình dạy; tìm theo tên hoặc mã
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, int? classId, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = Db.Enrollments.Where(e => e.Class.TeacherId == TeacherId);
        if (classId != null) q = q.Where(e => e.ClassId == classId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            q = q.Where(e => e.Student.FullName.Contains(kw) || e.Student.Code.Contains(kw));
        }

        var studentIds = q.Select(e => e.StudentId).Distinct();
        var total = await studentIds.CountAsync();

        var students = await Db.Students
            .Where(s => studentIds.Contains(s.Id))
            .OrderBy(s => s.FullName).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.Code, s.FullName, s.Phone, Email = s.User.Email, s.TargetBand, s.Status,
                Classes = s.Enrollments.Where(e => e.Class.TeacherId == TeacherId)
                    .Select(e => new { e.ClassId, ClassName = e.Class.Name, EnrollmentStatus = e.Status }).ToList()
            })
            .ToListAsync();

        var ids = students.Select(s => s.Id).ToList();
        var classIds = students.SelectMany(s => s.Classes.Select(c => c.ClassId)).Distinct().ToList();
        var attendance = await _stats.AttendanceAsync(classIds);
        var bands = await _stats.LatestBandsAsync(ids);

        return Ok(new
        {
            totalItems = total,
            page,
            pageSize,
            items = students.Select(s =>
            {
                var rates = s.Classes
                    .Select(c => attendance.TryGetValue((c.ClassId, s.Id), out var a) ? a : null)
                    .Where(a => a != null).ToList();
                bands.TryGetValue(s.Id, out var b);
                return new
                {
                    s.Id, s.Code, s.FullName, s.Phone, s.Email, s.TargetBand, s.Status, s.Classes,
                    attendanceRate = rates.Count == 0 ? null : (decimal?)Math.Round(rates.Average(a => a!.AttendanceRate ?? 0), 1),
                    absenceWarning = rates.Any(a => a!.Warning),
                    latestBand = bands.ContainsKey(s.Id) ? b.Latest : (decimal?)null
                };
            })
        });
    }

    // GET api/teacher/students/12  -> hồ sơ tiến bộ
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await TeachesStudentAsync(id)) return Error(404, "Không tìm thấy học viên trong các lớp bạn dạy");

        var s = await Db.Students.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Code, x.FullName, x.Phone, Email = x.User.Email, x.DateOfBirth, x.ParentName,
                x.ParentPhone, x.TargetBand, x.Status, x.User.AvatarUrl
            })
            .FirstAsync();

        var classes = await Db.Enrollments
            .Where(e => e.StudentId == id && e.Class.TeacherId == TeacherId)
            .OrderByDescending(e => e.Class.StartDate)
            .Select(e => new { e.ClassId, ClassName = e.Class.Name, CourseName = e.Class.Course.Name, e.Class.StartDate, e.Class.EndDate, e.Status })
            .ToListAsync();
        var classIds = classes.Select(c => c.ClassId).ToList();

        var attendance = await _stats.AttendanceAsync(classIds, id);
        var missing = await _stats.MissingAssignmentsAsync(classIds);

        // Biểu đồ band theo thời gian (bài Writing / Speaking đã công bố)
        var bandHistory = await Db.Submissions
            .Where(x => x.StudentId == id && classIds.Contains(x.Assignment.ClassId) && x.OverallBand != null
                && (x.Status == "Published" || x.Status == "Returned"))
            .OrderBy(x => x.PublishedAt ?? x.SubmittedAt)
            .Select(x => new
            {
                submissionId = x.Id, date = x.PublishedAt ?? x.SubmittedAt, x.Assignment.Skill,
                title = x.Assignment.Title, band = x.OverallBand, x.AttemptNo
            })
            .ToListAsync();

        var examHistory = await Db.ExamResults
            .Where(r => r.StudentId == id && r.Score != null && classIds.Contains(r.Exam.ClassId))
            .OrderBy(r => r.Exam.ExamDate)
            .Select(r => new
            {
                examId = r.ExamId, r.Exam.Title, r.Exam.Type, r.Exam.Skill, date = r.Exam.ExamDate,
                r.Score, r.Exam.MaxScore, score10 = Math.Round(r.Score!.Value / r.Exam.MaxScore * 10, 2)
            })
            .ToListAsync();

        var submissions = await Db.Submissions
            .Where(x => x.StudentId == id && classIds.Contains(x.Assignment.ClassId))
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => new
            {
                x.Id, x.AssignmentId, x.Assignment.Title, x.Assignment.Skill, x.Assignment.Type,
                ClassName = x.Assignment.Class.Name, x.AttemptNo, x.SubmittedAt, x.IsLate, x.Status,
                x.OverallBand, x.AutoScore, x.AutoMaxScore
            })
            .ToListAsync();

        // Trung bình band theo kỹ năng
        var bySkill = bandHistory.GroupBy(b => b.Skill)
            .Select(g => new { skill = g.Key, average = Math.Round(g.Average(x => x.band!.Value), 1), latest = g.Last().band });

        return Ok(new
        {
            student = s,
            classes = classes.Select(c =>
            {
                attendance.TryGetValue((c.ClassId, id), out var a);
                return new
                {
                    c.ClassId, c.ClassName, c.CourseName, c.StartDate, c.EndDate, enrollmentStatus = c.Status,
                    attendance = a,
                    missingAssignments = missing.GetValueOrDefault((c.ClassId, id))
                };
            }),
            targetBand = s.TargetBand,
            bandHistory,
            bandBySkill = bySkill,
            examHistory,
            submissions
        });
    }

    // PUT api/teacher/students/12/target-band  { "targetBand": 6.5 }
    [HttpPut("{id:int}/target-band")]
    public async Task<IActionResult> SetTargetBand(int id, TargetBandRequest req)
    {
        if (!await TeachesStudentAsync(id)) return Error(404, "Không tìm thấy học viên trong các lớp bạn dạy");
        if (req.TargetBand != null && !IeltsBands.IsValid(req.TargetBand.Value))
            return Error(400, "Band mục tiêu từ 0 đến 9, bước 0.5");

        var s = await Db.Students.FirstAsync(x => x.Id == id);
        s.TargetBand = req.TargetBand;
        await Db.SaveChangesAsync();
        return Ok(new { s.Id, s.TargetBand });
    }

    // ---------- Nhận xét định kỳ ----------

    private static object ReviewDto(StudentReview r, string className) => new
    {
        r.Id, r.StudentId, r.ClassId, className, r.Period, r.PeriodLabel, r.Attitude, r.AttendanceComment,
        r.Strengths, r.Improvements, r.ListeningComment, r.ReadingComment, r.WritingComment, r.SpeakingComment,
        r.OverallComment, r.CreatedAt, r.UpdatedAt
    };

    // GET api/teacher/students/12/reviews?classId=
    [HttpGet("{id:int}/reviews")]
    public async Task<IActionResult> GetReviews(int id, int? classId)
    {
        if (!await TeachesStudentAsync(id)) return Error(404, "Không tìm thấy học viên trong các lớp bạn dạy");

        var q = Db.StudentReviews.Include(r => r.Class)
            .Where(r => r.StudentId == id && r.Class.TeacherId == TeacherId);
        if (classId != null) q = q.Where(r => r.ClassId == classId);

        var items = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(items.Select(r => ReviewDto(r, r.Class.Name)));
    }

    private async Task<string?> CheckEnrollmentAsync(int studentId, int classId) =>
        await Db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.ClassId == classId && e.Class.TeacherId == TeacherId)
            ? null
            : "Học viên không học lớp này hoặc bạn không phụ trách lớp";

    private static void Apply(StudentReview r, StudentReviewRequest req)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        r.ClassId = req.ClassId;
        r.Period = req.Period;
        r.PeriodLabel = T(req.PeriodLabel);
        r.Attitude = T(req.Attitude);
        r.AttendanceComment = T(req.AttendanceComment);
        r.Strengths = T(req.Strengths);
        r.Improvements = T(req.Improvements);
        r.ListeningComment = T(req.ListeningComment);
        r.ReadingComment = T(req.ReadingComment);
        r.WritingComment = T(req.WritingComment);
        r.SpeakingComment = T(req.SpeakingComment);
        r.OverallComment = T(req.OverallComment);
    }

    // POST api/teacher/students/12/reviews
    [HttpPost("{id:int}/reviews")]
    public async Task<IActionResult> CreateReview(int id, StudentReviewRequest req)
    {
        var error = await CheckEnrollmentAsync(id, req.ClassId);
        if (error != null) return Error(400, error);

        if (req.Period == "EndCourse" &&
            await Db.StudentReviews.AnyAsync(r => r.StudentId == id && r.ClassId == req.ClassId && r.Period == "EndCourse"))
            return Error(409, "Học viên đã có phiếu nhận xét cuối khóa cho lớp này, hãy sửa phiếu cũ");

        var r = new StudentReview { StudentId = id, TeacherId = TeacherId, CreatedAt = AppTime.Now };
        Apply(r, req);
        Db.StudentReviews.Add(r);

        var userId = await Db.Students.Where(s => s.Id == id).Select(s => s.UserId).FirstAsync();
        _notify.Add(userId, "Announcement", "Giáo viên vừa gửi nhận xét mới cho bạn",
            req.PeriodLabel, "/student/feedback", UserId);

        await Db.SaveChangesAsync();
        var className = await Db.Classes.Where(c => c.Id == r.ClassId).Select(c => c.Name).FirstAsync();
        return StatusCode(201, ReviewDto(r, className));
    }

    // PUT api/teacher/students/12/reviews/4
    [HttpPut("{id:int}/reviews/{reviewId:int}")]
    public async Task<IActionResult> UpdateReview(int id, int reviewId, StudentReviewRequest req)
    {
        var r = await Db.StudentReviews.FirstOrDefaultAsync(x => x.Id == reviewId && x.StudentId == id && x.TeacherId == TeacherId);
        if (r == null) return Error(404, "Không tìm thấy nhận xét");

        var error = await CheckEnrollmentAsync(id, req.ClassId);
        if (error != null) return Error(400, error);

        Apply(r, req);
        r.UpdatedAt = AppTime.Now;
        await Db.SaveChangesAsync();
        var className = await Db.Classes.Where(c => c.Id == r.ClassId).Select(c => c.Name).FirstAsync();
        return Ok(ReviewDto(r, className));
    }

    // DELETE api/teacher/students/12/reviews/4
    [HttpDelete("{id:int}/reviews/{reviewId:int}")]
    public async Task<IActionResult> DeleteReview(int id, int reviewId)
    {
        var r = await Db.StudentReviews.FirstOrDefaultAsync(x => x.Id == reviewId && x.StudentId == id && x.TeacherId == TeacherId);
        if (r == null) return Error(404, "Không tìm thấy nhận xét");
        Db.StudentReviews.Remove(r);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Phiếu nhận xét cuối khóa ----------

    private async Task<object?> BuildFinalReportAsync(int id, int classId)
    {
        var cls = await Db.Classes.Where(c => c.Id == classId)
            .Select(c => new { c.Id, c.Name, CourseName = c.Course.Name, c.StartDate, c.EndDate, TeacherName = c.Teacher.FullName })
            .FirstOrDefaultAsync();
        if (cls == null) return null;

        var student = await Db.Students.Where(s => s.Id == id)
            .Select(s => new { s.Id, s.Code, s.FullName, s.TargetBand, s.ParentName, s.ParentPhone })
            .FirstAsync();

        var attendance = (await _stats.AttendanceAsync(new[] { classId }, id)).GetValueOrDefault((classId, id));
        var book = await _gradebook.BuildAsync(classId, id);
        var row = book?.Rows.FirstOrDefault();

        var bands = await Db.Submissions
            .Where(x => x.StudentId == id && x.Assignment.ClassId == classId && x.OverallBand != null
                && (x.Status == "Published" || x.Status == "Returned"))
            .Select(x => new { x.Assignment.Skill, Band = x.OverallBand!.Value, Date = x.PublishedAt ?? x.SubmittedAt })
            .ToListAsync();

        var reviews = await Db.StudentReviews
            .Where(r => r.StudentId == id && r.ClassId == classId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        var final = reviews.LastOrDefault(r => r.Period == "EndCourse");

        return new
        {
            generatedAt = AppTime.Now,
            student,
            @class = cls,
            attendance,
            grades = row == null ? null : new
            {
                row.AssignmentAverage, row.PeriodicAverage, row.MidtermAverage, row.FinalAverage,
                row.FinalScore, row.Result,
                exams = book!.Columns.Where(c => c.Kind == "Exam").Select(c => new
                {
                    c.Title, c.Type, c.MaxScore,
                    score = row.Cells.First(x => x.Kind == "Exam" && x.ColumnId == c.Id).Score
                })
            },
            bandBySkill = bands.GroupBy(b => b.Skill).Select(g => new
            {
                skill = g.Key,
                first = g.OrderBy(x => x.Date).First().Band,
                latest = g.OrderBy(x => x.Date).Last().Band,
                average = Math.Round(g.Average(x => x.Band), 1)
            }),
            periodicReviews = reviews.Where(r => r.Period != "EndCourse")
                .Select(r => new { r.Period, r.PeriodLabel, r.Attitude, r.Strengths, r.Improvements, r.CreatedAt }),
            finalReview = final == null ? null : ReviewDto(final, cls.Name)
        };
    }

    // GET api/teacher/students/12/final-report?classId=5  -> tự tổng hợp chuyên cần + điểm + nhận xét
    [HttpGet("{id:int}/final-report")]
    public async Task<IActionResult> FinalReport(int id, int classId)
    {
        var error = await CheckEnrollmentAsync(id, classId);
        if (error != null) return Error(404, error);
        return Ok(await BuildFinalReportAsync(id, classId));
    }

    // GET api/teacher/students/12/final-report/print?classId=5
    // Trả về trang HTML để in / "Lưu thành PDF" từ trình duyệt (window.print()) gửi học viên và phụ huynh
    [HttpGet("{id:int}/final-report/print")]
    public async Task<IActionResult> FinalReportPrint(int id, int classId)
    {
        var error = await CheckEnrollmentAsync(id, classId);
        if (error != null) return Error(404, error);

        var cls = await Db.Classes.Where(c => c.Id == classId)
            .Select(c => new { c.Name, CourseName = c.Course.Name, c.StartDate, c.EndDate }).FirstAsync();
        var student = await Db.Students.FirstAsync(s => s.Id == id);
        var center = await Db.CenterSettings.FindAsync(1);
        var attendance = (await _stats.AttendanceAsync(new[] { classId }, id)).GetValueOrDefault((classId, id));
        var row = (await _gradebook.BuildAsync(classId, id))?.Rows.FirstOrDefault();
        var final = await Db.StudentReviews
            .Where(r => r.StudentId == id && r.ClassId == classId && r.Period == "EndCourse")
            .OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();

        static string H(object? v) => WebUtility.HtmlEncode(v?.ToString() ?? "");
        static string N(decimal? v) => v == null ? "-" : v.Value.ToString("0.##");

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>Phiếu nhận xét cuối khóa</title>");
        sb.Append("<style>body{font-family:Arial,sans-serif;max-width:800px;margin:24px auto;color:#222;line-height:1.5}" +
                  "h1{font-size:22px;text-align:center;margin-bottom:4px}.sub{text-align:center;color:#666;margin-bottom:24px}" +
                  "table{width:100%;border-collapse:collapse;margin:12px 0}td,th{border:1px solid #ccc;padding:6px 10px;text-align:left}" +
                  "th{background:#f3f3f3;width:35%}h2{font-size:16px;border-bottom:2px solid #333;padding-bottom:4px;margin-top:24px}" +
                  ".sign{display:flex;justify-content:space-between;margin-top:48px}.sign div{text-align:center;width:40%}" +
                  "@media print{body{margin:0}}</style></head><body>");
        sb.Append($"<h1>PHIẾU NHẬN XÉT CUỐI KHÓA</h1><div class=\"sub\">{H(center?.Name)}</div>");

        sb.Append("<table>");
        sb.Append($"<tr><th>Học viên</th><td>{H(student.FullName)} ({H(student.Code)})</td></tr>");
        sb.Append($"<tr><th>Lớp / Khóa</th><td>{H(cls.Name)} - {H(cls.CourseName)}</td></tr>");
        sb.Append($"<tr><th>Thời gian</th><td>{cls.StartDate:dd/MM/yyyy} - {cls.EndDate:dd/MM/yyyy}</td></tr>");
        sb.Append($"<tr><th>Giáo viên</th><td>{H(TeacherName)}</td></tr>");
        if (student.TargetBand != null) sb.Append($"<tr><th>Band mục tiêu</th><td>{N(student.TargetBand)}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>Chuyên cần</h2><table>");
        if (attendance == null) sb.Append("<tr><td>Chưa có dữ liệu điểm danh</td></tr>");
        else
        {
            sb.Append($"<tr><th>Số buổi đã điểm danh</th><td>{attendance.TotalMarked}</td></tr>");
            sb.Append($"<tr><th>Có mặt / Muộn</th><td>{attendance.Present} / {attendance.Late}</td></tr>");
            sb.Append($"<tr><th>Vắng / Vắng có phép</th><td>{attendance.Absent} / {attendance.Excused}</td></tr>");
            sb.Append($"<tr><th>Tỉ lệ chuyên cần</th><td>{N(attendance.AttendanceRate)}%</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Kết quả học tập (thang 10)</h2><table>");
        sb.Append($"<tr><th>Điểm bài tập</th><td>{N(row?.AssignmentAverage)}</td></tr>");
        sb.Append($"<tr><th>Kiểm tra định kỳ</th><td>{N(row?.PeriodicAverage)}</td></tr>");
        sb.Append($"<tr><th>Giữa kỳ</th><td>{N(row?.MidtermAverage)}</td></tr>");
        sb.Append($"<tr><th>Cuối kỳ</th><td>{N(row?.FinalAverage)}</td></tr>");
        sb.Append($"<tr><th>Tổng kết</th><td><b>{N(row?.FinalScore)}</b> - {(row?.Result == "Pass" ? "Đạt" : row?.Result == "Fail" ? "Không đạt" : "Chưa đủ dữ liệu")}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>Nhận xét của giáo viên</h2><table>");
        if (final == null) sb.Append("<tr><td>Giáo viên chưa viết nhận xét cuối khóa</td></tr>");
        else
        {
            void Line(string label, string? text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append($"<tr><th>{label}</th><td>{H(text).Replace("\n", "<br>")}</td></tr>");
            }
            Line("Thái độ học tập", final.Attitude);
            Line("Chuyên cần", final.AttendanceComment);
            Line("Điểm mạnh", final.Strengths);
            Line("Cần cải thiện", final.Improvements);
            Line("Listening", final.ListeningComment);
            Line("Reading", final.ReadingComment);
            Line("Writing", final.WritingComment);
            Line("Speaking", final.SpeakingComment);
            Line("Đánh giá chung", final.OverallComment);
        }
        sb.Append("</table>");

        sb.Append($"<div class=\"sign\"><div>Phụ huynh / Học viên<br><br><br><br></div><div>Ngày {AppTime.Today:dd/MM/yyyy}<br>Giáo viên<br><br><br>{H(TeacherName)}</div></div>");
        sb.Append("</body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }

    // ---------- Đề xuất lên lớp ----------

    // POST api/teacher/students/12/promotions
    [HttpPost("{id:int}/promotions")]
    public async Task<IActionResult> Recommend(int id, PromotionCreate req)
    {
        var error = await CheckEnrollmentAsync(id, req.ClassId);
        if (error != null) return Error(400, error);

        if (req.RecommendedCourseId != null && !await Db.Courses.AnyAsync(c => c.Id == req.RecommendedCourseId))
            return Error(400, "Khóa học đề xuất không tồn tại");

        if (await Db.PromotionRecommendations.AnyAsync(p => p.StudentId == id && p.FromClassId == req.ClassId && p.Status == "Pending"))
            return Error(409, "Đã có đề xuất đang chờ Admin xử lý cho học viên này");

        var p = new PromotionRecommendation
        {
            StudentId = id,
            FromClassId = req.ClassId,
            TeacherId = TeacherId,
            RecommendedCourseId = req.RecommendedCourseId,
            Reason = req.Reason?.Trim(),
            Status = "Pending",
            CreatedAt = AppTime.Now
        };
        Db.PromotionRecommendations.Add(p);

        var studentName = await Db.Students.Where(s => s.Id == id).Select(s => s.FullName).FirstAsync();
        var adminIds = await Db.Users.Where(u => u.Role == "admin" && !u.IsLocked).Select(u => u.Id).ToListAsync();
        _notify.Add(adminIds, "Request", $"{TeacherName} đề xuất xếp lớp cho {studentName}", p.Reason, "/admin/students", UserId);

        await Db.SaveChangesAsync();
        return StatusCode(201, new { p.Id, p.Status, message = "Đã gửi đề xuất cho Admin" });
    }

    // GET api/teacher/students/promotions  -> các đề xuất mình đã gửi
    [HttpGet("promotions")]
    public async Task<IActionResult> MyPromotions()
    {
        var items = await Db.PromotionRecommendations
            .Where(p => p.TeacherId == TeacherId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.StudentId, StudentName = p.Student.FullName, StudentCode = p.Student.Code,
                p.FromClassId, FromClassName = p.FromClass.Name,
                p.RecommendedCourseId, RecommendedCourseName = p.RecommendedCourse != null ? p.RecommendedCourse.Name : null,
                p.Reason, p.Status, p.CreatedAt
            })
            .ToListAsync();
        return Ok(items);
    }
}
