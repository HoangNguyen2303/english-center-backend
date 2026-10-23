namespace EnglishCenter.Api.Dtos.Admin;

public class StudentEnrollmentItem
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public DateOnly ClassStartDate { get; set; }
    public DateOnly ClassEndDate { get; set; }
    public string ClassStatus { get; set; } = "";   // Upcoming | Ongoing | Finished

    public decimal TuitionFee { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTuitionFee { get; set; }
    public string Status { get; set; } = "";        // Active | OnHold | Dropped

    // Phiếu thu của ghi danh này (null nếu chưa đóng tiền)
    public int? PaymentId { get; set; }
    public DateOnly? PaidDate { get; set; }
    public string? PaymentMethod { get; set; }
}