namespace EnglishCenter.Api.Services;

public class StoredFile
{
    public string Url { get; init; } = "";
    public string FileName { get; init; } = "";
    public long Size { get; init; }
    public string ContentType { get; init; } = "";
    public string Kind { get; init; } = "Other";   // Pdf | Audio | Image | Video | Document | Other
}

// Lưu file upload vào wwwroot/uploads/{folder}/ và trả về đường dẫn tương đối, ví dụ
// "/uploads/materials/3f2a...pdf". Program.cs đã bật UseStaticFiles nên frontend mở thẳng được:
// http://localhost:5220/uploads/materials/3f2a...pdf
public class FileStorageService
{
    public const long MaxBytes = 30 * 1024 * 1024;   // 30 MB

    private static readonly Dictionary<string, string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "Pdf",
        [".mp3"] = "Audio", [".wav"] = "Audio", [".m4a"] = "Audio", [".ogg"] = "Audio", [".webm"] = "Audio",
        [".jpg"] = "Image", [".jpeg"] = "Image", [".png"] = "Image", [".gif"] = "Image", [".webp"] = "Image",
        [".mp4"] = "Video",
        [".doc"] = "Document", [".docx"] = "Document", [".ppt"] = "Document", [".pptx"] = "Document",
        [".xls"] = "Document", [".xlsx"] = "Document", [".txt"] = "Document"
    };

    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "materials", "assignments", "feedback", "avatars", "submissions", "misc"
    };

    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public static string? Validate(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return "Chưa chọn file";
        if (file.Length > MaxBytes) return "File tối đa 30 MB";
        if (!AllowedFolders.Contains(folder)) return "Thư mục lưu không hợp lệ";
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExt.ContainsKey(ext)) return $"Không hỗ trợ định dạng {ext}";
        return null;
    }

    public async Task<StoredFile> SaveAsync(IFormFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(root, "uploads", folder.ToLowerInvariant());
        Directory.CreateDirectory(dir);

        // Tên file ngẫu nhiên để không trùng và không lộ tên gốc trên đường dẫn
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var stream = File.Create(Path.Combine(dir, name)))
        {
            await file.CopyToAsync(stream);
        }

        return new StoredFile
        {
            Url = $"/uploads/{folder.ToLowerInvariant()}/{name}",
            FileName = Path.GetFileName(file.FileName),
            Size = file.Length,
            ContentType = file.ContentType,
            Kind = AllowedExt[ext]
        };
    }
}
