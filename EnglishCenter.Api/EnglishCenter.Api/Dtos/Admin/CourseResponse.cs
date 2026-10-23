namespace EnglishCenter.Api.Dtos.Admin;

public class CourseResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal BandFrom { get; set; }
    public decimal BandTo { get; set; }
    public int TotalSessions { get; set; }
    public int SessionsPerWeek { get; set; }
    public decimal Tuition { get; set; }
    public decimal DiscountPercent { get; set; }

    // Học phí sau giảm (tính tự động, FE dùng để hiển thị)
    public decimal FinalPrice => Tuition - Tuition * DiscountPercent / 100;

    public int OpenClasses { get; set; }     // số lớp chưa kết thúc
    public int ActiveStudents { get; set; }  // số học viên đang học
}