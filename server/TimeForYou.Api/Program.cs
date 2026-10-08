using TimeForYou.Api.Features.Auth;
using TimeForYou.Api.Features.Calendar;
using TimeForYou.Api.Features.Devices;
using TimeForYou.Api.Features.Notes;
using TimeForYou.Api.Features.Timetable;
using TimeForYou.Data;
using TimeForYou.WebUntis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDataModule(builder.Configuration);
builder.Services.AddWebUntisClient(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
// Prototype: the web app is a different origin from the API. Bearer tokens, no cookies, so open is fine.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Apply pending EF migrations before serving (#62). A failure throws here, so the process exits
// and the container never reports healthy against a half-migrated schema.
await app.Services.ApplyMigrationsAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok())
    .WithName("GetHealth");

app.MapAuthEndpoints();
app.MapTimetableEndpoints();
app.MapDeviceEndpoints();
app.MapCalendarEndpoints();
app.MapNoteEndpoints();

app.Run();

// Exposed so TimeForYou.Tests can boot the API in-process via WebApplicationFactory<Program>
// (issue #27, Seam A).
public partial class Program;
