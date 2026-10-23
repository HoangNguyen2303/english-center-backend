namespace EnglishCenter.Api.Entities;

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = "Draft";      // Draft | Published
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}