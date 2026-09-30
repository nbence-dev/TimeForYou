using TimeForYou.Tests.Infrastructure;
using TimeForYou.WebUntis;

namespace TimeForYou.Tests;

/// <summary>Exercises WebUntisClient against the real mock container (issue #30 AC).</summary>
[Collection(SeamACollection.Name)]
public sealed class WebUntisClientTests(SeamAFixture fixture)
{
    private static readonly WebUntisCredentials ValidCredentials =
        new("mock.timeforyou.local", "eduvos-demo", "demo.student", "MOCK-QR-SECRET-0001");

    [Fact]
    public async Task Login_with_the_fixture_qr_payload_returns_a_session()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var client = new WebUntisClient(httpClient);

        var session = await client.LoginAsync(ValidCredentials);

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
    }

    [Fact]
    public async Task Login_with_the_wrong_key_throws_credentials_rejected()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var client = new WebUntisClient(httpClient);
        var wrongCredentials = ValidCredentials with { Key = "WRONG-KEY" };

        await Assert.ThrowsAsync<WebUntisCredentialsRejectedException>(() => client.LoginAsync(wrongCredentials));
    }

    [Fact]
    public async Task Login_against_an_unreachable_host_throws_unavailable()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:1"),
            Timeout = TimeSpan.FromSeconds(3),
        };
        var client = new WebUntisClient(httpClient);

        await Assert.ThrowsAsync<WebUntisUnavailableException>(() => client.LoginAsync(ValidCredentials));
    }

    [Fact]
    public async Task Timetable_round_trip_returns_lessons_with_unique_ids_and_no_lecturer()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var client = new WebUntisClient(httpClient);
        var session = await client.LoginAsync(ValidCredentials);
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(14);

        var lessons = await client.GetTimetableAsync(session, from, to);

        Assert.NotEmpty(lessons);
        Assert.Equal(lessons.Count, lessons.Select(lesson => lesson.UntisLessonId).Distinct().Count());
        Assert.All(lessons, lesson =>
        {
            Assert.False(string.IsNullOrWhiteSpace(lesson.SubjectName));
            Assert.Null(lesson.Lecturer);
        });
    }

    [Fact]
    public async Task Timetable_with_an_unknown_session_throws_credentials_rejected()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var client = new WebUntisClient(httpClient);
        var bogusSession = new WebUntisSession("not-a-real-session");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<WebUntisCredentialsRejectedException>(() =>
            client.GetTimetableAsync(bogusSession, today, today.AddDays(7)));
    }
}
