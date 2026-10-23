namespace EnglishCenter.Api.Dtos.Admin;

public class ClassResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int MaxStudents { get; set; }
    public int EnrolledCount { get; set; }      // số ghi danh đang Active, hiển thị "12/20"
    public string? MeetingUrl { get; set; }
    public string Status { get; set; } = "";    // Upcoming | Ongoing | Finished

    public List<ClassScheduleItem> Schedules { get; set; } = new();

    // Chỉ có ở trang chi tiết
    public List<ClassStudentItem>? Students { get; set; }
}

public class ClassScheduleItem
{
    public int Id { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
}

public class ClassStudentItem
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Status { get; set; } = "";    // Active | OnHold | Dropped
    public decimal FinalTuitionFee { get; set; }
    public bool IsPaid { get; set; }
}