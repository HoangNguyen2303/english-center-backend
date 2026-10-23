using EnglishCenter.Api.Data;
using EnglishCenter.Api.Dtos;
using EnglishCenter.Api.Dtos.Admin;
using EnglishCenter.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/rooms")]
[Authorize(Roles = "admin")]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoomsController(AppDbContext db)
    {
        _db = db;
    }

    // GET api/admin/rooms
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rooms = await _db.Rooms
            .OrderBy(r => r.Name)
            .Select(r => new RoomResponse { Id = r.Id, Name = r.Name, Capacity = r.Capacity })
            .ToListAsync();

        return Ok(rooms);
    }

    // GET api/admin/rooms/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null)
            return NotFound(new { message = "Không tìm thấy phòng học" });

        return Ok(new RoomResponse { Id = room.Id, Name = room.Name, Capacity = room.Capacity });
    }

    // POST api/admin/rooms
    [HttpPost]
    public async Task<IActionResult> Create(RoomRequest req)
    {
        var name = req.Name.Trim();

        if (await _db.Rooms.AnyAsync(r => r.Name == name))
            return Conflict(new { message = "Tên phòng đã tồn tại" });

        var room = new Room { Name = name, Capacity = req.Capacity };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = room.Id },
            new RoomResponse { Id = room.Id, Name = room.Name, Capacity = room.Capacity });
    }

    // PUT api/admin/rooms/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, RoomRequest req)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null)
            return NotFound(new { message = "Không tìm thấy phòng học" });

        var name = req.Name.Trim();

        if (await _db.Rooms.AnyAsync(r => r.Name == name && r.Id != id))
            return Conflict(new { message = "Tên phòng đã tồn tại" });

        room.Name = name;
        room.Capacity = req.Capacity;
        await _db.SaveChangesAsync();

        return Ok(new RoomResponse { Id = room.Id, Name = room.Name, Capacity = room.Capacity });
    }

    // DELETE api/admin/rooms/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null)
            return NotFound(new { message = "Không tìm thấy phòng học" });

        // Luật: phòng đang dùng trong lịch học thì không được xóa
        if (await _db.ClassSchedules.AnyAsync(s => s.RoomId == id))
            return Conflict(new { message = "Phòng đang được dùng trong lịch học, không thể xóa" });

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}