using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class ClassScheduleRequest
{
    [Range(1, 7, ErrorMessage = "Thứ phải từ 1 (Thứ Hai) đến 7 (Chủ nhật)")]
    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn phòng học")]
    public int RoomId { get; set; }
}