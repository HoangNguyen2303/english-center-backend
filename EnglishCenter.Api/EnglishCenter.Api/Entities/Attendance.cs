namespace EnglishCenter.Api.Entities;

public class Attendance
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present";    // Present | Late | Absent | Excused
    public string? Note { get; set; }
    public bool IsAuto { get; set; }                   // true = tự đánh "Vắng có phép" khi duyệt đơn xin nghỉ
    public DateTime MarkedAt { get; set; }

    public ClassSession Session { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
