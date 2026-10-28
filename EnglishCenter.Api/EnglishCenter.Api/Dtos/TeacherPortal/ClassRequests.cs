using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

public class MeetingUrlRequest
{
    // Để trống = bỏ link
    [Url(ErrorMessage = "Link học online không hợp lệ")]
    [StringLength(500)]
    public string? MeetingUrl { get; set; }
}

// Gắn buổi với một bài trong syllabus và đánh dấu đã dạy
public class SessionLessonRequest
{
    public int? SyllabusLessonId { get; set; }
    public bool IsTaught { get; set; }
}

// Nhật ký buổi học
public class SessionLogRequest
{
    [StringLength(4000)]
    public string? ContentTaught { get; set; }

    [StringLength(2000)]
    public string? Homework { get; set; }
}

public class MaterialCreate
{
    [Required(ErrorMessage = "Tên tài liệu là bắt buộc")]
    [StringLength(250)]
    public string Title { get; set; } = "";

    [Required]
    [RegularExpression("Pdf|Audio|Video|Link|Other", ErrorMessage = "Loại tài liệu phải là Pdf, Audio, Video, Link hoặc Other")]
    public string Kind { get; set; } = "Pdf";

    // Link video / link ngoài, hoặc url trả về từ POST api/teacher/files
    [Required(ErrorMessage = "Đường dẫn tài liệu là bắt buộc")]
    [StringLength(500)]
    public string Url { get; set; } = "";
}

public class SyllabusLessonRequest
{
    // Bỏ trống = thêm vào cuối danh sách
    [Range(1, 1000)]
    public int? OrderNo { get; set; }

    [Required(ErrorMessage = "Tên bài là bắt buộc")]
    [StringLength(250)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }
}

// ---------- Điểm danh ----------

public class AttendanceItem
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required]
    [RegularExpression("Present|Late|Absent|Excused",
        ErrorMessage = "Trạng thái phải là Present (có mặt), Late (muộn), Absent (vắng) hoặc Excused (vắng có phép)")]
    public string Status { get; set; } = "";

    [StringLength(500)]
    public string? Note { get; set; }
}

public class AttendanceSaveRequest
{
    [MinLength(1, ErrorMessage = "Danh sách điểm danh đang trống")]
    public List<AttendanceItem> Items { get; set; } = new();
}

public class ReasonRequest
{
    [Required(ErrorMessage = "Lý do là bắt buộc")]
    [StringLength(1000)]
    public string Reason { get; set; } = "";
}

public class NoteRequest
{
    [StringLength(1000)]
    public string? Note { get; set; }
}
