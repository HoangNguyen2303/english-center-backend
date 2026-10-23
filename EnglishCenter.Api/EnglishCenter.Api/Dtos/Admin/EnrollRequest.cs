using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class EnrollRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn lớp")]
    public int ClassId { get; set; }
}