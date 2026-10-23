using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Chỉ seed khi chưa có user nào
        if (await db.Users.AnyAsync()) return;

        // 1. Thông tin trung tâm (Id = 1)
        db.CenterSettings.Add(new CenterSetting { Id = 1, Name = "English Center" });

        // 2. Tài khoản admin / teacher / student
        var admin = new User
        {
            Email = "admin@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "admin"
        };
        var teacherUser = new User
        {
            Email = "teacher@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "teacher"
        };
        var studentUser = new User
        {
            Email = "student@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "student"
        };
        db.Users.AddRange(admin, teacherUser, studentUser);

        // 3. Hồ sơ giáo viên, học viên
        var teacher = new Teacher
        {
            User = teacherUser,
            FullName = "Nguyen Van A",
            Phone = "0900000001",
            Specialty = "IELTS",
            Status = "Teaching"
        };
        var student = new Student
        {
            User = studentUser,
            Code = "HV0001",
            FullName = "Tran Thi B",
            Phone = "0900000002",
            Status = "Studying"
        };
        db.Teachers.Add(teacher);
        db.Students.Add(student);

        // 4. Phòng, khóa học
        var room = new Room { Name = "P101", Capacity = 20 };
        var course = new Course
        {
            Name = "IELTS 6.5",
            BandFrom = 5.0m,
            BandTo = 6.5m,
            TotalSessions = 24,
            SessionsPerWeek = 3,
            Tuition = 5000000m,
            DiscountPercent = 10m
        };
        db.Rooms.Add(room);
        db.Courses.Add(course);

        // 5. Lớp + lịch học
        var cls = new CourseClass
        {
            Course = course,
            Teacher = teacher,
            Name = "IELTS 6.5 - Lớp tối 2-4-6",
            StartDate = new DateOnly(2026, 11, 2),
            EndDate = new DateOnly(2026, 12, 30),
            MaxStudents = 20
        };
        cls.Schedules.Add(new ClassSchedule { Room = room, DayOfWeek = 1, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) });
        cls.Schedules.Add(new ClassSchedule { Room = room, DayOfWeek = 3, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) });
        cls.Schedules.Add(new ClassSchedule { Room = room, DayOfWeek = 5, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) });
        db.Classes.Add(cls);

        // 6. Ghi danh (snapshot học phí tính từ khóa học)
        var discountAmount = course.Tuition * course.DiscountPercent / 100;
        db.Enrollments.Add(new Enrollment
        {
            Student = student,
            Class = cls,
            TuitionFee = course.Tuition,
            DiscountPercent = course.DiscountPercent,
            DiscountAmount = discountAmount,
            FinalTuitionFee = course.Tuition - discountAmount,
            Status = "Active"
        });

        await db.SaveChangesAsync();
    }
}