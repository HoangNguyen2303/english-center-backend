using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

// Giờ hệ thống dùng chung cho các chức năng Teacher (giờ địa phương của server, giống ClassesController)
public static class AppTime
{
    public static DateTime Now => DateTime.Now;
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static DateTime At(DateOnly date, TimeOnly time) => date.ToDateTime(time);

    // Thứ Hai = 1 ... Chủ nhật = 7 (giống quy ước ClassSchedules.DayOfWeek)
    public static int DayNo(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    // Ngày Thứ Hai của tuần chứa date
    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(1 - DayNo(date));
}

// Quản lý các buổi học cụ thể (ClassSessions) sinh ra từ lịch tuần ClassSchedules
public class SessionService
{
    public const int AttendanceLockHours = 48;
    public const int AttendanceOpenMinutesBefore = 30;   // được điểm danh trước giờ học 30 phút

    private readonly AppDbContext _db;

    public SessionService(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Trạng thái buổi học ----------

    // Upcoming (sắp tới) | Ongoing (đang dạy) | Done (đã xong) | Cancelled (đã hủy)
    public static string StatusOf(ClassSession s, DateTime now)
    {
        if (s.Status == "Cancelled") return "Cancelled";
        return StatusOf(s.Date, s.StartTime, s.EndTime, now);
    }

    public static string StatusOf(DateOnly date, TimeOnly start, TimeOnly end, DateTime now)
    {
        var startAt = AppTime.At(date, start);
        var endAt = AppTime.At(date, end);
        if (now < startAt) return "Upcoming";
        if (now <= endAt) return "Ongoing";
        return "Done";
    }

    // Điểm danh bị khóa sau 48 giờ kể từ lúc kết thúc buổi, trừ khi Admin mở lại
    public static DateTime AttendanceLockAt(ClassSession s) => AppTime.At(s.Date, s.EndTime).AddHours(AttendanceLockHours);

    public static bool IsAttendanceLocked(ClassSession s, DateTime now)
    {
        if (s.AttendanceUnlockedUntil != null && s.AttendanceUnlockedUntil > now) return false;
        return now > AttendanceLockAt(s);
    }

    public static bool CanTakeAttendanceYet(ClassSession s, DateTime now) =>
        now >= AppTime.At(s.Date, s.StartTime).AddMinutes(-AttendanceOpenMinutesBefore);

    // ---------- Sinh / đồng bộ buổi học ----------

    private record Slot(DateOnly Date, TimeOnly Start, TimeOnly End, int RoomId);

    // Danh sách buổi theo lịch tuần: từ ngày bắt đầu, đủ tổng số buổi của khóa hoặc đến ngày kết thúc
    private static List<Slot> Generate(CourseClass cls)
    {
        var result = new List<Slot>();
        if (cls.Schedules.Count == 0) return result;

        var total = cls.Course.TotalSessions;
        var date = cls.StartDate;
        while (date <= cls.EndDate && result.Count < total)
        {
            var dayNo = AppTime.DayNo(date);
            foreach (var s in cls.Schedules.Where(x => x.DayOfWeek == dayNo).OrderBy(x => x.StartTime))
            {
                if (result.Count >= total) break;
                result.Add(new Slot(date, s.StartTime, s.EndTime, s.RoomId));
            }
            date = date.AddDays(1);
        }
        return result;
    }

    // Đồng bộ ClassSessions với lịch tuần hiện tại của lớp.
    // - Thêm các buổi còn thiếu.
    // - Buổi thường ở tương lai không còn trong lịch (Admin đổi lịch) và chưa có dữ liệu gì thì xóa.
    // - Buổi đã qua, đã điểm danh, đã có tài liệu / nhật ký, buổi bù, buổi bị hủy: giữ nguyên.
    // Gọi mỗi khi giáo viên xem lịch / lớp nên Admin không cần làm gì thêm.
    public async Task SyncAsync(int classId)
    {
        var cls = await _db.Classes
            .Include(c => c.Course)
            .Include(c => c.Schedules)
            .FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null) return;

        var expected = Generate(cls);
        var sessions = await _db.ClassSessions.Where(s => s.ClassId == classId).ToListAsync();

        var usedIds = new HashSet<int>(await _db.Attendances
            .Where(a => a.Session.ClassId == classId).Select(a => a.SessionId).Distinct().ToListAsync());
        usedIds.UnionWith(await _db.SessionMaterials
            .Where(m => m.Session.ClassId == classId).Select(m => m.SessionId).Distinct().ToListAsync());
        usedIds.UnionWith(await _db.TeacherRequests
            .Where(r => r.Session.ClassId == classId).Select(r => r.SessionId).Distinct().ToListAsync());
        usedIds.UnionWith(await _db.StudentRequests
            .Where(r => r.SessionId != null && r.ClassId == classId).Select(r => r.SessionId!.Value).Distinct().ToListAsync());

        var today = AppTime.Today;
        var expectedByKey = expected.ToDictionary(x => (x.Date, x.Start));
        var changed = false;

        foreach (var s in sessions.ToList())
        {
            if (s.Type != "Regular" || s.Date < today || usedIds.Contains(s.Id)) continue;

            if (expectedByKey.TryGetValue((s.Date, s.StartTime), out var slot))
            {
                // Admin có thể đổi phòng / giờ kết thúc
                if (s.Status == "Scheduled" && (s.RoomId != slot.RoomId || s.EndTime != slot.End))
                {
                    s.RoomId = slot.RoomId;
                    s.EndTime = slot.End;
                    changed = true;
                }
                continue;
            }

            var hasData = s.IsTaught || s.SyllabusLessonId != null
                || !string.IsNullOrEmpty(s.ContentTaught) || !string.IsNullOrEmpty(s.Homework)
                || !string.IsNullOrEmpty(s.MeetingUrl);
            if (!hasData && s.Status == "Scheduled")
            {
                _db.ClassSessions.Remove(s);
                sessions.Remove(s);
                changed = true;
            }
        }

        var existingKeys = sessions.Select(s => (s.Date, s.StartTime)).ToHashSet();
        foreach (var slot in expected)
        {
            if (existingKeys.Contains((slot.Date, slot.Start))) continue;
            var s = new ClassSession
            {
                ClassId = classId,
                RoomId = slot.RoomId,
                Date = slot.Date,
                StartTime = slot.Start,
                EndTime = slot.End,
                Type = "Regular",
                Status = "Scheduled"
            };
            _db.ClassSessions.Add(s);
            sessions.Add(s);
            changed = true;
        }

        // Đánh số buổi lại theo thứ tự thời gian (buổi bị hủy không tính)
        var no = 1;
        foreach (var s in sessions.Where(x => x.Status != "Cancelled").OrderBy(x => x.Date).ThenBy(x => x.StartTime))
        {
            if (s.SessionNo != no) { s.SessionNo = no; changed = true; }
            no++;
        }

        if (!changed) return;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai request đồng bộ cùng lúc: unique index (ClassId, Date, StartTime) chặn bản trùng.
            // Bỏ các thay đổi đang chờ, request sau sẽ đọc lại dữ liệu đúng.
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }
    }

    // Đồng bộ tất cả các lớp chưa kết thúc của một giáo viên
    public async Task SyncTeacherAsync(int teacherId)
    {
        var today = AppTime.Today;
        var classIds = await _db.Classes
            .Where(c => c.TeacherId == teacherId && c.EndDate >= today.AddDays(-7))
            .Select(c => c.Id)
            .ToListAsync();
        foreach (var id in classIds)
            await SyncAsync(id);
    }

    // Buổi mặc định để điểm danh: buổi gần nhất đã bắt đầu, nếu chưa có thì buổi sắp tới gần nhất
    public async Task<ClassSession?> DefaultSessionAsync(int classId)
    {
        var now = AppTime.Now;
        var today = AppTime.Today;
        var nowTime = TimeOnly.FromDateTime(now).AddMinutes(AttendanceOpenMinutesBefore);

        var started = await _db.ClassSessions
            .Where(s => s.ClassId == classId && s.Status == "Scheduled"
                && (s.Date < today || (s.Date == today && s.StartTime <= nowTime)))
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
        if (started != null) return started;

        return await _db.ClassSessions
            .Where(s => s.ClassId == classId && s.Status == "Scheduled")
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .FirstOrDefaultAsync();
    }

    // Link học online hiệu lực: link riêng của buổi, không có thì dùng link của lớp
    public static string? EffectiveMeetingUrl(ClassSession s, CourseClass cls) =>
        string.IsNullOrWhiteSpace(s.MeetingUrl) ? cls.MeetingUrl : s.MeetingUrl;
}
