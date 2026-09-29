using TimeForYou.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDataModule(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok())
    .WithName("GetHealth");

app.Run();

// Exposed so TimeForYou.Tests can boot the API in-process via WebApplicationFactory<Program>
// (issue #27, Seam A).
public partial class Program;
