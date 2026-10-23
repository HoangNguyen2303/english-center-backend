using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class RoomRequest
{
    [Required(ErrorMessage = "Tên phòng là bắt buộc")]
    [StringLength(50)]
    public string Name { get; set; } = "";

    [Range(1, 1000, ErrorMessage = "Sức chứa phải lớn hơn 0")]
    public int Capacity { get; set; }
}