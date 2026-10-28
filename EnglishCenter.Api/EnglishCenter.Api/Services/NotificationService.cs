using EnglishCenter.Api.Data;
using EnglishCenter.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Services;

// Tạo thông báo (chuông) cho người dùng. Các hàm chỉ Add vào DbContext,
// người gọi tự SaveChangesAsync để lưu cùng một lần với thay đổi chính.
public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public void Add(IEnumerable<int> userIds, string type, string title, string? content = null,
        string? link = null, int? senderUserId = null, Announcement? announcement = null)
    {
        var now = AppTime.Now;
        foreach (var userId in userIds.Distinct())
        {
            var n = new Notification
            {
                RecipientUserId = userId,
                SenderUserId = senderUserId,
                Type = type,
                Title = title,
                Content = content,
                Link = link,
                CreatedAt = now
            };
            if (announcement != null) n.Announcement = announcement;
            _db.Notifications.Add(n);
        }
    }

    public void Add(int userId, string type, string title, string? content = null,
        string? link = null, int? senderUserId = null) =>
        Add(new[] { userId }, type, title, content, link, senderUserId);

    // Thông báo cho giáo viên phụ trách lớp
    public async Task AddToClassTeacherAsync(int classId, string type, string title, string? content = null, string? link = null)
    {
        var userId = await _db.Classes.Where(c => c.Id == classId)
            .Select(c => (int?)c.Teacher.UserId).FirstOrDefaultAsync();
        if (userId != null) Add(userId.Value, type, title, content, link);
    }

    // Thông báo cho tất cả học viên đang học (Active) của lớp
    public async Task AddToClassStudentsAsync(int classId, string type, string title, string? content = null,
        string? link = null, int? senderUserId = null)
    {
        var userIds = await _db.Enrollments
            .Where(e => e.ClassId == classId && e.Status == "Active")
            .Select(e => e.Student.UserId)
            .ToListAsync();
        Add(userIds, type, title, content, link, senderUserId);
    }
}
