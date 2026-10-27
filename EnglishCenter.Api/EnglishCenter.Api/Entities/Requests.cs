namespace EnglishCenter.Api.Entities;

// Đơn giáo viên gửi Admin: xin nghỉ, đổi buổi, xin mở lại điểm danh đã khóa
public class TeacherRequest
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string Type { get; set; } = "Leave";        // Leave | Reschedule | AttendanceUnlock
    public int SessionId { get; set; }
    public string Reason { get; set; } = "";
    public DateOnly? ProposedDate { get; set; }        // đề xuất buổi bù
    public TimeOnly? ProposedStartTime { get; set; }
    public TimeOnly? ProposedEndTime { get; set; }
    public string Status { get; set; } = "Pending";    // Pending | Approved | Rejected | Cancelled
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public ClassSession Session { get; set; } = null!;
}

// Yêu cầu học viên gửi giáo viên (từ page Schedule bên Student)
public class StudentRequest
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int TeacherId { get; set; }
    public string Type { get; set; } = "Leave";        // Leave | Reschedule | OneOnOneBooking | OneOnOneCancel
    public int? ClassId { get; set; }
    public int? SessionId { get; set; }                // Leave / Reschedule: buổi muốn nghỉ / đổi
    public int? AvailabilityId { get; set; }           // OneOnOneBooking: khung giờ rảnh học viên chọn
    public int? OneOnOneSessionId { get; set; }        // OneOnOneCancel: buổi 1 kèm 1 muốn hủy
    public DateOnly? Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";    // Pending | Approved | Rejected | Cancelled
    public string? ResponseNote { get; set; }          // lý do từ chối / ghi chú khi duyệt
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public CourseClass? Class { get; set; }
    public ClassSession? Session { get; set; }
}

// Khung giờ rảnh giáo viên khai báo để học viên đặt lịch 1 kèm 1
public class TeacherAvailability
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Note { get; set; }
    public bool IsBooked { get; set; }

    public Teacher Teacher { get; set; } = null!;
}

// Buổi học 1 kèm 1 (tạo ra khi giáo viên duyệt yêu cầu đặt lịch)
public class OneOnOneSession
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int StudentId { get; set; }
    public int? AvailabilityId { get; set; }
    public int? RequestId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Topic { get; set; }
    public string? MeetingUrl { get; set; }
    public string Status { get; set; } = "Scheduled";  // Scheduled | Cancelled
    public DateTime CreatedAt { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
