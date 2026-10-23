using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class ClassRequest : IValidatableObject
{
    [Required(ErrorMessage = "Tên lớp là bắt buộc")]
    [StringLength(150)]
    public string Name { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn khóa học")]
    public int CourseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn giáo viên")]
    public int TeacherId { get; set; }

    public DateOnly StartDate { get; set; }

    // Bỏ trống thì hệ thống tự tính
    public DateOnly? EndDate { get; set; }

    [Range(1, 1000, ErrorMessage = "Sĩ số tối đa phải lớn hơn 0")]
    public int MaxStudents { get; set; }

    [Url(ErrorMessage = "Link học online không hợp lệ")]
    [StringLength(500)]
    public string? MeetingUrl { get; set; }

    [MinLength(1, ErrorMessage = "Lớp phải có ít nhất một buổi học mỗi tuần")]
    public List<ClassScheduleRequest> Schedules { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (EndDate != null && EndDate < StartDate)
            yield return new ValidationResult("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu", new[] { nameof(EndDate) });

        for (var i = 0; i < Schedules.Count; i++)
        {
            if (Schedules[i].StartTime >= Schedules[i].EndTime)
                yield return new ValidationResult($"Lịch học dòng {i + 1}: giờ bắt đầu phải trước giờ kết thúc", new[] { nameof(Schedules) });
        }

        // Hai dòng lịch cùng thứ mà giờ chồng nhau (gồm cả dòng trùng hệt nhau)
        for (var i = 0; i < Schedules.Count; i++)
        {
            for (var j = i + 1; j < Schedules.Count; j++)
            {
                var a = Schedules[i];
                var b = Schedules[j];
                if (a.DayOfWeek == b.DayOfWeek && a.StartTime < b.EndTime && b.StartTime < a.EndTime)
                    yield return new ValidationResult($"Lịch học dòng {i + 1} và dòng {j + 1} bị trùng giờ", new[] { nameof(Schedules) });
            }
        }
    }
}