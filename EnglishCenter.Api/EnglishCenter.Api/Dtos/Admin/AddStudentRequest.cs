using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Dtos.Admin;

public class AddStudentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Hãy chọn học viên")]
    public int StudentId { get; set; }
}