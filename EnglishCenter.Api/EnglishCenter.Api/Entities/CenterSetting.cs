namespace EnglishCenter.Api.Entities;

public class CenterSetting
{
    public int Id { get; set; }          // luôn = 1
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
}