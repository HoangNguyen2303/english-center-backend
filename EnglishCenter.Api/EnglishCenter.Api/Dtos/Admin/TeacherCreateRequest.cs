using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class TeacherCreateRequest
{
    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(150)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20)]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(255)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Chuyên môn là bắt buộc")]
    [StringLength(100)]
    public string Specialty { get; set; } = "";

    [StringLength(200)]
    public string? Degree { get; set; }

    public DateOnly? HireDate { get; set; }

    [RegularExpression("^(Teaching|OnLeave)$", ErrorMessage = "Trạng thái phải là Teaching hoặc OnLeave")]
    public string Status { get; set; } = "Teaching";

    [Required(ErrorMessage = "Mật khẩu tạm là bắt buộc")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string TempPassword { get; set; } = "";
}