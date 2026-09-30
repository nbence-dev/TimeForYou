using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;
using TimeForYou.WebUntis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDataModule(builder.Configuration);
builder.Services.AddWebUntisClient(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok())
    .WithName("GetHealth");

app.MapAuthEndpoints();

app.Run();

// Exposed so TimeForYou.Tests can boot the API in-process via WebApplicationFactory<Program>
// (issue #27, Seam A).
public partial class Program;
