namespace EnglishCenter.Api.Entities;

public class Payment
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public int CreatedByUserId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";       // Cash | Transfer
    public DateOnly PaidDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Enrollment Enrollment { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}