namespace EnglishCenter.Api.Entities;

// Thông báo gửi đến một tài khoản (giáo viên, học viên...). Dùng cho chuông thông báo.
public class Notification
{
    public int Id { get; set; }
    public int RecipientUserId { get; set; }
    public int? SenderUserId { get; set; }
    public int? AnnouncementId { get; set; }

    // Announcement | Submission | ScheduleChange | Request | Reminder | Grade | System
    public string Type { get; set; } = "System";
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public string? Link { get; set; }                  // gợi ý để frontend điều hướng, ví dụ "/teacher/grading/15"
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Recipient { get; set; } = null!;
    public Announcement? Announcement { get; set; }
}

// Thông báo giáo viên gửi cho cả lớp hoặc một số học viên (lưu lại để xem lịch sử đã gửi)
public class Announcement
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int? ClassId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public CourseClass? Class { get; set; }
    public List<Notification> Notifications { get; set; } = new();
}
