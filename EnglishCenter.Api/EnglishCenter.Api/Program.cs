using System.Text;
using EnglishCenter.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using EnglishCenter.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<ScheduleService>();

// Chức năng Teacher
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<GradebookService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var idStr = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var verStr = ctx.Principal?.FindFirst("tokenVersion")?.Value;

                if (!int.TryParse(idStr, out var id) || !int.TryParse(verStr, out var ver))
                {
                    ctx.Fail("Token không hợp lệ");
                    return;
                }

                var u = await db.Users.AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new { x.TokenVersion, x.IsLocked })
                    .FirstOrDefaultAsync();

                if (u == null || u.IsLocked || u.TokenVersion != ver)
                    ctx.Fail("Token đã bị thu hồi");
            }
        };
    });

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    await TeacherSeeder.SeedCommentTemplatesAsync(db);
    if (app.Environment.IsDevelopment())
        await TeacherSeeder.SeedDemoAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseHttpsRedirection();

// Phục vụ file upload (tài liệu, ảnh đề, audio) tại /uploads/...
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAuthorization();

app.MapControllers();

app.Run();
