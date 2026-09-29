using System.Net;
using System.Net.Http.Json;
using System.Text;
using TimeForYou.WebUntis;

namespace TimeForYou.Tests;

/// <summary>
/// Exercises WebUntisClient's exception mapping against a fake HttpMessageHandler — no real
/// WebUntis backend needed, so this runs without Docker (issue #30 code review: a timeout or an
/// unrecognized wire status must not escape as something other than WebUntisException).
/// </summary>
public sealed class WebUntisClientFailureTests
{
    private static readonly WebUntisCredentials Credentials = new("server", "school", "user", "key");

    [Fact]
    public async Task An_http_timeout_throws_unavailable_not_the_raw_cancellation()
    {
        using var httpClient = new HttpClient(new HangingHandler())
        {
            BaseAddress = new Uri("http://fake"),
            Timeout = TimeSpan.FromMilliseconds(50),
        };
        var client = new WebUntisClient(httpClient);

        await Assert.ThrowsAsync<WebUntisUnavailableException>(() => client.LoginAsync(Credentials));
    }

    [Fact]
    public async Task Caller_cancellation_still_propagates_as_a_cancellation()
    {
        using var httpClient = new HttpClient(new HangingHandler()) { BaseAddress = new Uri("http://fake") };
        var client = new WebUntisClient(httpClient);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LoginAsync(Credentials, cts.Token));
    }

    [Fact]
    public async Task An_unrecognized_lesson_status_throws_unavailable_not_ArgumentOutOfRangeException()
    {
        using var httpClient = new HttpClient(new SequencedHandler(
            LoginOkResponse(),
            JsonResponse(HttpStatusCode.OK, """
                [{"untis_lesson_id":"1-2026-10-06","subject_code":"X","subject_name":"X",
                  "room":"B1","status":"pending_review",
                  "start_ts":"2026-10-06T08:00:00+02:00","end_ts":"2026-10-06T09:00:00+02:00"}]
                """)))
        {
            BaseAddress = new Uri("http://fake"),
        };
        var client = new WebUntisClient(httpClient);
        var session = await client.LoginAsync(Credentials);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<WebUntisUnavailableException>(() => client.GetTimetableAsync(session, today, today));
    }

    private static HttpResponseMessage LoginOkResponse() =>
        JsonResponse(HttpStatusCode.OK, """{"session_id":"session-1"}""");

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed class SequencedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responses[_index++]);
    }

    private sealed class UnreachableException : Exception;
}
