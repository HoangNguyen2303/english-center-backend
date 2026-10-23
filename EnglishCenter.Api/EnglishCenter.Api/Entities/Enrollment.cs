namespace EnglishCenter.Api.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int ClassId { get; set; }
    public decimal TuitionFee { get; set; }            // học phí gốc (snapshot)
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTuitionFee { get; set; }
    public string Status { get; set; } = "Active";     // Active | OnHold | Dropped
    public DateTime CreatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public CourseClass Class { get; set; } = null!;
    public Payment? Payment { get; set; }
}