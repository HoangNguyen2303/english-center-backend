using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

public class ScheduleCheck
{
    public string? Error { get; init; }
    public int StatusCode { get; init; }
    public DateOnly EndDate { get; init; }
}

public class ScheduleService
{
    private readonly AppDbContext _db;

    public ScheduleService(AppDbContext db)
    {
        _db = db;
    }

    private static string DayName(int d) => d == 7 ? "Chủ nhật" : $"Thứ {d + 1}";

    // Tự tính ngày kết thúc: đếm các buổi theo lịch tuần cho đến khi đủ tổng số buổi
    public static DateOnly ComputeEndDate(DateOnly start, int totalSessions, IEnumerable<int> days)
    {
        var perDay = days.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
        var count = 0;
        var date = start;

        while (true)
        {
            // .NET: Chủ nhật = 0. Quy ước của mình: Thứ Hai = 1 ... Chủ nhật = 7
            var n = date.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            if (perDay.TryGetValue(n, out var sessions))
            {
                count += sessions;
                if (count >= totalSessions) return date;
            }
            date = date.AddDays(1);
        }
    }

    // Kiểm tra toàn bộ luật lịch. excludeClassId = lớp đang sửa (để không tự trùng với chính nó)
    public async Task<ScheduleCheck> CheckAsync(int? excludeClassId, ClassRequest req, Course course)
    {
        // 1. Phòng phải tồn tại và đủ chỗ cho sĩ số tối đa
        var roomIds = req.Schedules.Select(s => s.RoomId).Distinct().ToList();
        var rooms = await _db.Rooms.Where(r => roomIds.Contains(r.Id)).ToListAsync();

        if (rooms.Count != roomIds.Count)
            return new ScheduleCheck { StatusCode = 400, Error = "Có phòng học không tồn tại" };

        var small = rooms.FirstOrDefault(r => r.Capacity < req.MaxStudents);
        if (small != null)
            return new ScheduleCheck
            {
                StatusCode = 400,
                Error = $"Sĩ số tối đa ({req.MaxStudents}) vượt sức chứa của phòng {small.Name} ({small.Capacity})"
            };

        // 2. Ngày kết thúc
        var endDate = req.EndDate
            ?? ComputeEndDate(req.StartDate, course.TotalSessions, req.Schedules.Select(s => s.DayOfWeek));

        // 3. Trùng lịch giáo viên hoặc phòng với lớp khác
        var excludeId = excludeClassId ?? 0;

        foreach (var s in req.Schedules)
        {
            var day = (byte)s.DayOfWeek;
            var startTime = s.StartTime;
            var endTime = s.EndTime;
            var roomId = s.RoomId;
            var teacherId = req.TeacherId;
            var start = req.StartDate;

            var clash = await _db.ClassSchedules
                .Include(x => x.Class)
                .Include(x => x.Room)
                .Where(x => x.ClassId != excludeId
                    && x.DayOfWeek == day
                    && x.Class.StartDate <= endDate && x.Class.EndDate >= start   // khoảng ngày chồng nhau
                    && x.StartTime < endTime && x.EndTime > startTime            // khoảng giờ chồng nhau
                    && (x.Class.TeacherId == teacherId || x.RoomId == roomId))
                .FirstOrDefaultAsync();

            if (clash != null)
            {
                var who = clash.Class.TeacherId == teacherId
                    ? "Giáo viên"
                    : $"Phòng {clash.Room.Name}";

                return new ScheduleCheck
                {
                    StatusCode = 409,
                    Error = $"{who} bị trùng lịch với lớp \"{clash.Class.Name}\" vào {DayName(clash.DayOfWeek)} " +
                            $"({clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm})"
                };
            }
        }

        return new ScheduleCheck { EndDate = endDate };
    }
}