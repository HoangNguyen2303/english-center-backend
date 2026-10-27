using EnglishCenter.Api.Data;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Controllers.TeacherPortal;

// Upload file dùng chung: tài liệu buổi học, file đề (ảnh biểu đồ, audio), ghi âm nhận xét Speaking
[Route("api/teacher/files")]
public class FilesController : TeacherControllerBase
{
    private readonly FileStorageService _files;

    public FilesController(AppDbContext db, FileStorageService files) : base(db)
    {
        _files = files;
    }

    // POST api/teacher/files?folder=materials|assignments|feedback|misc
    // multipart/form-data, field "file". Trả về url để gửi vào các API khác.
    [HttpPost]
    [RequestSizeLimit(FileStorageService.MaxBytes)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "misc")
    {
        if (folder == "avatars") return Error(400, "Đổi ảnh đại diện dùng POST api/teacher/profile/avatar");

        var error = FileStorageService.Validate(file, folder);
        if (error != null) return Error(400, error);

        var saved = await _files.SaveAsync(file, folder);
        return StatusCode(201, saved);
    }
}
