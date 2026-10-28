namespace EnglishCenter.Api.Entities;

// Một buổi học cụ thể (có ngày). Sinh tự động từ ClassSchedules (lịch tuần do Admin tạo),
// cộng thêm các buổi dạy bù khi Admin duyệt đơn xin nghỉ / đổi buổi của giáo viên.
public class ClassSession
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int? RoomId { get; set; }
    public int SessionNo { get; set; }                 // buổi thứ mấy của lớp (1, 2, 3...)
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Type { get; set; } = "Regular";      // Regular | Makeup
    public string Status { get; set; } = "Scheduled";  // Scheduled | Cancelled
    public int? ReplacesSessionId { get; set; }        // buổi bù thay cho buổi nào

    // Giáo án
    public int? SyllabusLessonId { get; set; }
    public bool IsTaught { get; set; }
    public DateTime? TaughtAt { get; set; }

    // Link học online riêng cho buổi (để trống thì dùng link của lớp)
    public string? MeetingUrl { get; set; }

    // Nhật ký buổi học
    public string? ContentTaught { get; set; }
    public string? Homework { get; set; }

    // Điểm danh
    public DateTime? AttendanceTakenAt { get; set; }
    public DateTime? AttendanceUnlockedUntil { get; set; }   // Admin mở lại cho sửa sau 48h

    public CourseClass Class { get; set; } = null!;
    public Room? Room { get; set; }
    public SyllabusLesson? SyllabusLesson { get; set; }
    public List<SessionMaterial> Materials { get; set; } = new();
    public List<Attendance> Attendances { get; set; } = new();
}
