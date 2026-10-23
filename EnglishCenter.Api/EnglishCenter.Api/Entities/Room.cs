namespace EnglishCenter.Api.Entities;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }

    public List<ClassSchedule> ClassSchedules { get; set; } = new();
}