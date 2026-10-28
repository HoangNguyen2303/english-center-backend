using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Data;

// Dữ liệu ban đầu cho chức năng Teacher
public static class TeacherSeeder
{
    // Ngân hàng nhận xét mẫu chung (chạy mọi môi trường, chỉ khi bảng còn trống)
    public static async Task SeedCommentTemplatesAsync(AppDbContext db)
    {
        if (await db.CommentTemplates.AnyAsync()) return;

        var items = new (string Skill, string? Criterion, string Content)[]
        {
            ("Writing", "TR", "Bạn trả lời đúng trọng tâm đề, quan điểm rõ ràng. Hãy phát triển thêm ví dụ cụ thể cho mỗi luận điểm."),
            ("Writing", "TR", "Bài chưa trả lời hết các phần của đề. Đọc kỹ đề và gạch chân từ khóa trước khi viết."),
            ("Writing", "TR", "Task 1: cần có câu overview nêu xu hướng chính, tránh liệt kê số liệu từng mục."),
            ("Writing", "CC", "Bố cục rõ ràng, mỗi đoạn một ý chính. Dùng từ nối đa dạng hơn thay vì lặp 'Firstly/Secondly'."),
            ("Writing", "CC", "Các câu trong đoạn chưa liên kết chặt chẽ. Hãy dùng đại từ thay thế và câu chủ đề ở đầu đoạn."),
            ("Writing", "LR", "Từ vựng phong phú, có collocation tốt. Chú ý chính tả của một số từ học thuật."),
            ("Writing", "LR", "Lặp từ khá nhiều. Hãy paraphrase từ khóa của đề và học thêm từ đồng nghĩa theo chủ đề."),
            ("Writing", "GRA", "Có dùng câu phức nhưng còn lỗi chia thì và mạo từ. Hãy kiểm tra lại bài trước khi nộp."),
            ("Writing", "GRA", "Ngữ pháp chính xác, cấu trúc đa dạng. Có thể thêm câu bị động và mệnh đề quan hệ rút gọn."),
            ("Writing", null, "Bài làm tốt! Giữ vững phong độ và luyện thêm tốc độ viết trong 40 phút."),
            ("Speaking", "FC", "Nói khá trôi chảy, nhưng còn ngập ngừng khi tìm ý. Hãy luyện trả lời theo khung: ý chính - lý do - ví dụ."),
            ("Speaking", "FC", "Câu trả lời còn ngắn. Part 2 hãy cố gắng nói đủ 2 phút."),
            ("Speaking", "LR", "Từ vựng phù hợp chủ đề. Thử dùng thêm idioms và cụm động từ tự nhiên."),
            ("Speaking", "GRA", "Còn lỗi thì quá khứ khi kể chuyện. Chú ý động từ bất quy tắc."),
            ("Speaking", "P", "Phát âm rõ ràng. Chú ý âm cuối (-s, -ed) và trọng âm từ nhiều âm tiết."),
            ("Speaking", "P", "Ngữ điệu còn đều, hãy nhấn mạnh từ khóa để câu nói tự nhiên hơn."),
            ("Speaking", null, "Tự tin và giao tiếp tốt. Tiếp tục luyện nói hằng ngày với các chủ đề quen thuộc.")
        };

        db.CommentTemplates.AddRange(items.Select(x => new CommentTemplate
        {
            TeacherId = null,
            Skill = x.Skill,
            Criterion = x.Criterion,
            Content = x.Content
        }));
        await db.SaveChangesAsync();
    }

    // Dữ liệu demo để thử các page Teacher (chỉ chạy ở Development, chỉ khi có tài khoản teacher@demo.com
    // và giáo viên này chưa có bài tập nào). Xóa dữ liệu thì cứ xóa, lần chạy sau sẽ không tạo lại nếu đã có bài tập.
    public static async Task SeedDemoAsync(AppDbContext db)
    {
        var teacher = await db.Teachers.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.Email == "teacher@demo.com");
        if (teacher == null) return;
        if (await db.Assignments.AnyAsync(a => a.TeacherId == teacher.Id)) return;

        var cls = await db.Classes.Include(c => c.Course)
            .Where(c => c.TeacherId == teacher.Id).OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (cls == null) return;

        var student = await db.Enrollments.Where(e => e.ClassId == cls.Id && e.Status == "Active")
            .Select(e => e.Student).FirstOrDefaultAsync();

        var now = DateTime.Now;

        // Syllabus mẫu cho khóa
        if (!await db.SyllabusLessons.AnyAsync(l => l.CourseId == cls.CourseId))
        {
            var titles = new[]
            {
                "Giới thiệu bài thi IELTS & kiểm tra đầu vào", "Writing Task 1: Line graph", "Speaking Part 1: Hometown, Work, Study",
                "Reading: Skimming & Scanning", "Listening Section 1-2: Form completion", "Writing Task 2: Opinion essay",
                "Speaking Part 2: Describe a person", "Reading: True/False/Not Given", "Writing Task 1: Bar chart & Table",
                "Listening Section 3-4: Multiple choice", "Writing Task 2: Discussion essay", "Speaking Part 3: Abstract topics"
            };
            db.SyllabusLessons.AddRange(titles.Select((t, i) => new SyllabusLesson { CourseId = cls.CourseId, OrderNo = i + 1, Title = t }));
        }

        var writing = new Assignment
        {
            ClassId = cls.Id,
            TeacherId = teacher.Id,
            Title = "Writing Task 2 - Technology and children",
            Skill = "Writing",
            Type = "Writing",
            Prompt = "Some people think that children should not be allowed to use smartphones until they are teenagers. " +
                     "To what extent do you agree or disagree? Write at least 250 words.",
            DueAt = now.AddDays(-2),
            MinWords = 250,
            AllowLate = true,
            AssignToAll = true,
            CreatedAt = now.AddDays(-7)
        };
        var quiz = new Assignment
        {
            ClassId = cls.Id,
            TeacherId = teacher.Id,
            Title = "Reading mini test - Vocabulary",
            Skill = "Reading",
            Type = "Quiz",
            Prompt = "Chọn đáp án đúng hoặc điền từ thích hợp.",
            DueAt = now.AddDays(5),
            AllowLate = false,
            AssignToAll = true,
            CreatedAt = now,
            Questions = new()
            {
                new AssignmentQuestion { OrderNo = 1, QuestionType = "MultipleChoice", Content = "'Ubiquitous' is closest in meaning to:",
                    OptionsJson = "[\"A. rare\",\"B. everywhere\",\"C. expensive\",\"D. dangerous\"]", CorrectAnswer = "B", Points = 1 },
                new AssignmentQuestion { OrderNo = 2, QuestionType = "FillBlank", Content = "The opposite of 'increase' is ______.",
                    CorrectAnswer = "decrease|decline|fall", Points = 1 }
            }
        };
        db.Assignments.AddRange(writing, quiz);

        if (student != null)
        {
            var essay = "Nowadays, smartphones have become an essential part of modern life. However, some people believe " +
                        "that children should not be allowed to use them until they become teenagers. I partly agree with this view. " +
                        "On the one hand, smartphones can be harmful for young children because they spend too much time on games " +
                        "and social media instead of playing outside. This can effect their health and their ability to concentrate. " +
                        "On the other hand, smartphones can be a useful tool for learning and communication with parents.";
            db.Submissions.Add(new Submission
            {
                Assignment = writing,
                StudentId = student.Id,
                AttemptNo = 1,
                Content = essay,
                WordCount = Services.SubmissionService.CountWords(essay),
                SubmittedAt = now.AddDays(-3),
                IsLate = false,
                Status = "Submitted"
            });

            db.StudentRequests.Add(new StudentRequest
            {
                StudentId = student.Id,
                TeacherId = teacher.Id,
                Type = "OneOnOneBooking",
                ClassId = cls.Id,
                Date = DateOnly.FromDateTime(now.AddDays(3)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                Reason = "Muốn luyện Speaking Part 2",
                Status = "Pending",
                CreatedAt = now
            });

            db.Notifications.Add(new Notification
            {
                RecipientUserId = teacher.UserId,
                Type = "Submission",
                Title = $"{student.FullName} vừa nộp bài \"{writing.Title}\"",
                Link = "/teacher/grading",
                CreatedAt = now.AddDays(-3)
            });
        }

        await db.SaveChangesAsync();
    }
}
