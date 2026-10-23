using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Common;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = "";
}