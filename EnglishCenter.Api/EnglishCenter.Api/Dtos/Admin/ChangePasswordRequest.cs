using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = "";

    [Compare(nameof(NewPassword), ErrorMessage = "Nhập lại mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = "";
}