using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.TeacherPortal;

// Đơn xin nghỉ / đổi buổi gửi Admin
public class LeaveRequestCreate : IValidatableObject
{
    [Required]
    [RegularExpression("Leave|Reschedule", ErrorMessage = "Loại đơn phải là Leave (xin nghỉ) hoặc Reschedule (đổi buổi)")]
    public string Type { get; set; } = "Leave";

    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn buổi học")]
    public int SessionId { get; set; }

    [Required(ErrorMessage = "Lý do là bắt buộc")]
    [StringLength(1000)]
    public string Reason { get; set; } = "";

    // Đề xuất buổi bù (bắt buộc với đơn đổi buổi)
    public DateOnly? ProposedDate { get; set; }
    public TimeOnly? ProposedStartTime { get; set; }
    public TimeOnly? ProposedEndTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        var any = ProposedDate != null || ProposedStartTime != null || ProposedEndTime != null;
        var all = ProposedDate != null && ProposedStartTime != null && ProposedEndTime != null;

        if (Type == "Reschedule" && !all)
            yield return new ValidationResult("Đơn đổi buổi phải có ngày, giờ bắt đầu và giờ kết thúc buổi bù");
        else if (any && !all)
            yield return new ValidationResult("Buổi bù đề xuất phải có đủ ngày, giờ bắt đầu và giờ kết thúc");

        if (all && ProposedStartTime >= ProposedEndTime)
            yield return new ValidationResult("Giờ bắt đầu buổi bù phải trước giờ kết thúc");
    }
}

// Khai báo khung giờ rảnh cho học viên đặt lịch 1 kèm 1
public class AvailabilityCreate : IValidatableObject
{
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    // Lặp lại hằng tuần trong N tuần (1 = chỉ ngày này)
    [Range(1, 12, ErrorMessage = "Số tuần lặp từ 1 đến 12")]
    public int RepeatWeeks { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (StartTime >= EndTime)
            yield return new ValidationResult("Giờ bắt đầu phải trước giờ kết thúc");
        if ((EndTime - StartTime).TotalMinutes < 30)
            yield return new ValidationResult("Khung giờ rảnh tối thiểu 30 phút");
    }
}
