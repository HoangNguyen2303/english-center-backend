namespace EnglishCenter.Api.Dtos.Admin;

public class CenterSettingResponse
{
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
}