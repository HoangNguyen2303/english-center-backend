using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class StudentUpdateRequest
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(150)]
    public string FullName { get; set; } = "";

    public DateOnly? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20)]
    public string Phone { get; set; } = "";

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(150)]
    public string? ParentName { get; set; }

    [StringLength(20)]
    public string? ParentPhone { get; set; }

    [RegularExpression("^(Studying|OnHold|Dropped)$", ErrorMessage = "Trạng thái phải là Studying, OnHold hoặc Dropped")]
    public string Status { get; set; } = "Studying";
}