using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// Seam A coverage for the multi-student demo (#129): a developer student and a guest created at
/// runtime are each linked through POST /qr-scan to their own device, and a change made for one
/// reaches only that student's device.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class MultiStudentTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_change_for_one_student_pushes_to_that_students_device_only()
    {
        var developer = await OnboardWithDeviceAsync("eduvos-demo:nbence-dev", "token-developer");
        var guest = await OnboardWithDeviceAsync(await CreateGuestAsync("Jane", "Doe"), "token-guest");
        await RunPollCycleAsync();
        Assert.Empty(fixture.PushSender.Sent);

        await CancelInMockAsync(await FirstSnapshotLessonIdAsync(guest));
        await RunPollCycleAsync();

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal("token-guest", payload.PushToken);
        await using var db = fixture.CreateDbContext();
        Assert.All(await db.ChangeLog.ToListAsync(), entry => Assert.Equal(guest, entry.UserId));
        Assert.DoesNotContain(await db.ChangeLog.ToListAsync(), entry => entry.UserId == developer);
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record GuestWire(string Key);

    private sealed record QrScanResponseWire(string Token, [property: JsonPropertyName("user_id")] Guid UserId);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    private async Task<string> CreateGuestAsync(string firstName, string lastName)
    {
        using var mock = CreateMockClient();
        var response = await mock.PostAsJsonAsync("/mock/guests", new { first_name = firstName, last_name = lastName });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GuestWire>())!.Key;
    }

    private async Task<Guid> OnboardWithDeviceAsync(string studentKey, string pushToken)
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>($"/qr-payload?student={Uri.EscapeDataString(studentKey)}")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        var scan = await response.Content.ReadFromJsonAsync<QrScanResponseWire>()
            ?? throw new InvalidOperationException("qr-scan returned no body");

        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync(d => d.UserId == scan.UserId);
        device.PushToken = pushToken;
        device.Platform = Enum.GetValues<DevicePlatform>()[0];
        await db.SaveChangesAsync();
        return scan.UserId;
    }

    private async Task RunPollCycleAsync()
    {
        using var host = fixture.CreateWorkerHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }

    private async Task<string> FirstSnapshotLessonIdAsync(Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        var snapshot = await db.LessonSnapshots
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.StartTs)
            .FirstAsync();
        return snapshot.UntisLessonId;
    }

    private async Task CancelInMockAsync(string lessonId)
    {
        using var mock = CreateMockClient();
        (await mock.PostAsync($"/mock/cancel-lesson/{lessonId}", null)).EnsureSuccessStatusCode();
    }
}
