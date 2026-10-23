using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class CenterSettingRequest
{
    [Required(ErrorMessage = "Tên trung tâm là bắt buộc")]
    [StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }
}