namespace EnglishCenter.Api.Entities;

// Tài liệu đính kèm theo buổi: PDF, audio, link video...
public class SessionMaterial
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Pdf";          // Pdf | Audio | Video | Link | Other
    public string Url { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public ClassSession Session { get; set; } = null!;
}
