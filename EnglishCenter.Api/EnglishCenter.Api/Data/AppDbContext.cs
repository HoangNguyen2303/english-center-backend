using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<CourseClass> Classes => Set<CourseClass>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<HomepageImage> HomepageImages => Set<HomepageImage>();
    public DbSet<CenterSetting> CenterSettings => Set<CenterSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- Users ----------
        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.AvatarUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.ToTable(t => t.HasCheckConstraint("CK_Users_Role",
                "[Role] IN ('admin','teacher','student')"));
        });

        // ---------- Teachers ----------
        b.Entity<Teacher>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Specialty).HasMaxLength(100).IsRequired();
            e.Property(x => x.Degree).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();

            e.HasOne(x => x.User).WithOne(u => u.Teacher)
                .HasForeignKey<Teacher>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Teachers_Status",
                "[Status] IN ('Teaching','OnLeave')"));
        });

        // ---------- Students ----------
        b.Entity<Student>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.ParentName).HasMaxLength(150);
            e.Property(x => x.ParentPhone).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.TargetBand).HasPrecision(3, 1);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasOne(x => x.User).WithOne(u => u.Student)
                .HasForeignKey<Student>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Students_Status",
                "[Status] IN ('Studying','OnHold','Dropped')"));
        });

        // ---------- Courses ----------
        b.Entity<Course>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.BandFrom).HasPrecision(3, 1);
            e.Property(x => x.BandTo).HasPrecision(3, 1);
            e.Property(x => x.Tuition).HasPrecision(18, 2);
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Courses_Band", "[BandFrom] >= 0 AND [BandTo] <= 9 AND [BandFrom] <= [BandTo]");
                t.HasCheckConstraint("CK_Courses_Sessions", "[TotalSessions] > 0 AND [SessionsPerWeek] BETWEEN 1 AND 7 AND [SessionsPerWeek] <= [TotalSessions]");
                t.HasCheckConstraint("CK_Courses_Tuition", "[Tuition] >= 0");
                t.HasCheckConstraint("CK_Courses_Discount", "[DiscountPercent] BETWEEN 0 AND 50");
            });
        });

        // ---------- Rooms ----------
        b.Entity<Room>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_Rooms_Capacity", "[Capacity] > 0"));
        });

        // ---------- Classes (entity CourseClass -> bảng Classes) ----------
        b.Entity<CourseClass>(e =>
        {
            e.ToTable("Classes");
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.MeetingUrl).HasMaxLength(500);

            e.HasOne(x => x.Course).WithMany(c => c.Classes)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Teacher).WithMany(t => t.Classes)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable("Classes", t =>
            {
                t.HasCheckConstraint("CK_Classes_Dates", "[StartDate] <= [EndDate]");
                t.HasCheckConstraint("CK_Classes_MaxStudents", "[MaxStudents] > 0");
            });
        });

        // ---------- ClassSchedules ----------
        b.Entity<ClassSchedule>(e =>
        {
            e.HasOne(x => x.Class).WithMany(c => c.Schedules)
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Room).WithMany(r => r.ClassSchedules)
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // không cho trùng một dòng lịch trong cùng lớp
            e.HasIndex(x => new { x.ClassId, x.DayOfWeek, x.StartTime, x.EndTime }).IsUnique();

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ClassSchedules_Day", "[DayOfWeek] BETWEEN 1 AND 7");
                t.HasCheckConstraint("CK_ClassSchedules_Time", "[StartTime] < [EndTime]");
            });
        });

        // ---------- Enrollments ----------
        b.Entity<Enrollment>(e =>
        {
            e.Property(x => x.TuitionFee).HasPrecision(18, 2);
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.FinalTuitionFee).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasIndex(x => new { x.StudentId, x.ClassId }).IsUnique();

            e.HasOne(x => x.Student).WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Class).WithMany(c => c.Enrollments)
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Enrollments_Status",
                "[Status] IN ('Active','OnHold','Dropped')"));
        });

        // ---------- Payments ----------
        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // 1 ghi danh - tối đa 1 phiếu thu (EnrollmentId tự thành unique)
            e.HasOne(x => x.Enrollment).WithOne(en => en.Payment)
                .HasForeignKey<Payment>(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedByUser).WithMany(u => u.Payments)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                t.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Transfer')");
            });
        });

        // ---------- Posts ----------
        b.Entity<Post>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(250).IsRequired();
            e.Property(x => x.ThumbnailUrl).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.ToTable(t => t.HasCheckConstraint("CK_Posts_Status",
                "[Status] IN ('Draft','Published')"));
        });

        // ---------- HomepageImages ----------
        b.Entity<HomepageImage>(e =>
        {
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.Property(x => x.AltText).HasMaxLength(250);
        });

        // ---------- CenterSettings (chỉ 1 dòng, Id = 1) ----------
        b.Entity<CenterSetting>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.ToTable(t => t.HasCheckConstraint("CK_CenterSettings_SingleRow", "[Id] = 1"));
        });

        // ---------- Các bảng cho chức năng Teacher (xem AppDbContext.Teacher.cs) ----------
        ConfigureTeacherFeatures(b);
    }
}