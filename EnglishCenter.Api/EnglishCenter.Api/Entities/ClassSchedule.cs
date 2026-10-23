namespace EnglishCenter.Api.Entities;

public class ClassSchedule
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int RoomId { get; set; }
    public byte DayOfWeek { get; set; }                // 1 = Thứ Hai ... 7 = Chủ nhật
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public CourseClass Class { get; set; } = null!;
    public Room Room { get; set; } = null!;
}