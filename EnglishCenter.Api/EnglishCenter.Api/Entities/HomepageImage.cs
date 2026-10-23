namespace EnglishCenter.Api.Entities;

public class HomepageImage
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
}