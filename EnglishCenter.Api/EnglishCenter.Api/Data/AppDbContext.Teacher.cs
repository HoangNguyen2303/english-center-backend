using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Data;

// Cấu hình các bảng phục vụ chức năng Teacher.
// Tách file riêng để không đụng nhiều vào AppDbContext.cs của cả nhóm.
// Lưu ý SQL Server: chỉ để Cascade cho quan hệ cha - con rõ ràng, còn lại Restrict
// để tránh lỗi "multiple cascade paths".
public partial class AppDbContext
{
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<SyllabusLesson> SyllabusLessons => Set<SyllabusLesson>();
    public DbSet<SessionMaterial> SessionMaterials => Set<SessionMaterial>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentStudent> AssignmentStudents => Set<AssignmentStudent>();
    public DbSet<AssignmentQuestion> AssignmentQuestions => Set<AssignmentQuestion>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionAnnotation> SubmissionAnnotations => Set<SubmissionAnnotation>();
    public DbSet<CommentTemplate> CommentTemplates => Set<CommentTemplate>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<GradeConfig> GradeConfigs => Set<GradeConfig>();
    public DbSet<StudentReview> StudentReviews => Set<StudentReview>();
    public DbSet<PromotionRecommendation> PromotionRecommendations => Set<PromotionRecommendation>();
    public DbSet<TeacherRating> TeacherRatings => Set<TeacherRating>();
    public DbSet<TeacherRequest> TeacherRequests => Set<TeacherRequest>();
    public DbSet<StudentRequest> StudentRequests => Set<StudentRequest>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<OneOnOneSession> OneOnOneSessions => Set<OneOnOneSession>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    private static void ConfigureTeacherFeatures(ModelBuilder b)
    {
        // ---------- ClassSessions ----------
        b.Entity<ClassSession>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.MeetingUrl).HasMaxLength(500);
            e.Property(x => x.ContentTaught).HasMaxLength(4000);
            e.Property(x => x.Homework).HasMaxLength(2000);
            // mỗi lớp không có 2 buổi cùng ngày, cùng giờ bắt đầu (chặn sinh trùng khi 2 request đồng bộ cùng lúc)
            e.HasIndex(x => new { x.ClassId, x.Date, x.StartTime }).IsUnique();

            e.HasOne(x => x.Class).WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Room).WithMany()
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SyllabusLesson).WithMany()
                .HasForeignKey(x => x.SyllabusLessonId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ClassSessions_Type", "[Type] IN ('Regular','Makeup')");
                t.HasCheckConstraint("CK_ClassSessions_Status", "[Status] IN ('Scheduled','Cancelled')");
                t.HasCheckConstraint("CK_ClassSessions_Time", "[StartTime] < [EndTime]");
            });
        });

        // ---------- SyllabusLessons ----------
        b.Entity<SyllabusLesson>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => new { x.CourseId, x.OrderNo });
            e.HasOne(x => x.Course).WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- SessionMaterials ----------
        b.Entity<SessionMaterial>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Session).WithMany(s => s.Materials)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_SessionMaterials_Kind",
                "[Kind] IN ('Pdf','Audio','Video','Link','Other')"));
        });

        // ---------- Attendances ----------
        b.Entity<Attendance>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Session).WithMany(s => s.Attendances)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Attendances_Status",
                "[Status] IN ('Present','Late','Absent','Excused')"));
        });

        // ---------- Assignments ----------
        b.Entity<Assignment>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Skill).HasMaxLength(20).IsRequired();
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Prompt).IsRequired();
            e.Property(x => x.AttachmentUrl).HasMaxLength(500);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.ClassId, x.DueAt });

            e.HasOne(x => x.Class).WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Exam).WithMany(x => x.OnlineParts)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Assignments_Skill", "[Skill] IN ('Writing','Speaking','Reading','Listening')");
                t.HasCheckConstraint("CK_Assignments_Type", "[Type] IN ('Writing','Speaking','Quiz')");
            });
        });

        b.Entity<AssignmentStudent>(e =>
        {
            e.HasKey(x => new { x.AssignmentId, x.StudentId });
            e.HasOne(x => x.Assignment).WithMany(a => a.Targets)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AssignmentQuestion>(e =>
        {
            e.Property(x => x.QuestionType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            e.Property(x => x.CorrectAnswer).HasMaxLength(500).IsRequired();
            e.Property(x => x.Points).HasPrecision(6, 2);
            e.HasOne(x => x.Assignment).WithMany(a => a.Questions)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_AssignmentQuestions_Type",
                "[QuestionType] IN ('MultipleChoice','FillBlank')"));
        });

        // ---------- Submissions ----------
        b.Entity<Submission>(e =>
        {
            e.Property(x => x.AudioUrl).HasMaxLength(500);
            e.Property(x => x.FeedbackAudioUrl).HasMaxLength(500);
            e.Property(x => x.ReturnNote).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Tr).HasPrecision(3, 1);
            e.Property(x => x.Cc).HasPrecision(3, 1);
            e.Property(x => x.Fc).HasPrecision(3, 1);
            e.Property(x => x.Lr).HasPrecision(3, 1);
            e.Property(x => x.Gra).HasPrecision(3, 1);
            e.Property(x => x.P).HasPrecision(3, 1);
            e.Property(x => x.OverallBand).HasPrecision(3, 1);
            e.Property(x => x.AutoScore).HasPrecision(8, 2);
            e.Property(x => x.AutoMaxScore).HasPrecision(8, 2);
            e.HasIndex(x => new { x.AssignmentId, x.StudentId, x.AttemptNo }).IsUnique();
            e.HasIndex(x => new { x.Status, x.SubmittedAt });

            e.HasOne(x => x.Assignment).WithMany(a => a.Submissions)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Submissions_Status",
                "[Status] IN ('Submitted','Draft','Published','Returned')"));
        });

        b.Entity<SubmissionAnnotation>(e =>
        {
            e.Property(x => x.SelectedText).HasMaxLength(2000).IsRequired();
            e.Property(x => x.ErrorType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Suggestion).HasMaxLength(1000);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.Submission).WithMany(s => s.Annotations)
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_SubmissionAnnotations_Type",
                "[ErrorType] IN ('Grammar','Vocabulary','Coherence')"));
        });

        b.Entity<CommentTemplate>(e =>
        {
            e.Property(x => x.Skill).HasMaxLength(20).IsRequired();
            e.Property(x => x.Criterion).HasMaxLength(10);
            e.Property(x => x.Content).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.Teacher).WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Exams ----------
        b.Entity<Exam>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Skill).HasMaxLength(20);
            e.Property(x => x.MaxScore).HasPrecision(6, 2);
            e.HasOne(x => x.Class).WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Exams_Type", "[Type] IN ('Periodic','Midterm','Final')");
                t.HasCheckConstraint("CK_Exams_MaxScore", "[MaxScore] > 0");
            });
        });

        b.Entity<ExamResult>(e =>
        {
            e.Property(x => x.Score).HasPrecision(6, 2);
            e.Property(x => x.Source).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Exam).WithMany(x => x.Results)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GradeConfig>(e =>
        {
            e.HasKey(x => x.ClassId);
            e.Property(x => x.AssignmentWeight).HasPrecision(5, 2);
            e.Property(x => x.PeriodicWeight).HasPrecision(5, 2);
            e.Property(x => x.MidtermWeight).HasPrecision(5, 2);
            e.Property(x => x.FinalWeight).HasPrecision(5, 2);
            e.Property(x => x.PassScore).HasPrecision(4, 2);
            e.HasOne(x => x.Class).WithOne()
                .HasForeignKey<GradeConfig>(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Nhận xét, đề xuất lên lớp, đánh giá giáo viên ----------
        b.Entity<StudentReview>(e =>
        {
            e.Property(x => x.Period).HasMaxLength(20).IsRequired();
            e.Property(x => x.PeriodLabel).HasMaxLength(50);
            foreach (var p in new[] { nameof(StudentReview.Attitude), nameof(StudentReview.AttendanceComment),
                         nameof(StudentReview.Strengths), nameof(StudentReview.Improvements),
                         nameof(StudentReview.ListeningComment), nameof(StudentReview.ReadingComment),
                         nameof(StudentReview.WritingComment), nameof(StudentReview.SpeakingComment) })
                e.Property(p).HasMaxLength(2000);
            e.Property(x => x.OverallComment).HasMaxLength(4000);

            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_StudentReviews_Period",
                "[Period] IN ('Monthly','MidCourse','EndCourse')"));
        });

        b.Entity<PromotionRecommendation>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromClass).WithMany().HasForeignKey(x => x.FromClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RecommendedCourse).WithMany().HasForeignKey(x => x.RecommendedCourseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TeacherRating>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_TeacherRatings_Score", "[Score] BETWEEN 1 AND 5"));
        });

        // ---------- Đơn / yêu cầu ----------
        b.Entity<TeacherRequest>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.AdminNote).HasMaxLength(1000);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TeacherRequests_Type", "[Type] IN ('Leave','Reschedule','AttendanceUnlock')");
                t.HasCheckConstraint("CK_TeacherRequests_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
            });
        });

        b.Entity<StudentRequest>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.ResponseNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.TeacherId, x.Status });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_StudentRequests_Type", "[Type] IN ('Leave','Reschedule','OneOnOneBooking','OneOnOneCancel')");
                t.HasCheckConstraint("CK_StudentRequests_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
            });
        });

        b.Entity<TeacherAvailability>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.TeacherId, x.Date });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_TeacherAvailabilities_Time", "[StartTime] < [EndTime]"));
        });

        b.Entity<OneOnOneSession>(e =>
        {
            e.Property(x => x.Topic).HasMaxLength(250);
            e.Property(x => x.MeetingUrl).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.TeacherId, x.Date });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_OneOnOneSessions_Status", "[Status] IN ('Scheduled','Cancelled')"));
        });

        // ---------- Thông báo ----------
        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Content).HasMaxLength(4000);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.RecipientUserId, x.IsRead });
            e.HasOne(x => x.Recipient).WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Announcement).WithMany(a => a.Notifications)
                .HasForeignKey(x => x.AnnouncementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Announcement>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
