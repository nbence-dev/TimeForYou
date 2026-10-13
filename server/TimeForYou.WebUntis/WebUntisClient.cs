using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TimeForYou.Domain;

namespace TimeForYou.WebUntis;

/// <summary>Default <see cref="IWebUntisClient"/>, talking to the mock's plain REST surface over
/// an injected <see cref="HttpClient"/> (base address set by <see cref="ServiceCollectionExtensions.AddWebUntisClient"/>).</summary>
public sealed class WebUntisClient(HttpClient httpClient) : IWebUntisClient
{
    public async Task<WebUntisSession> LoginAsync(WebUntisCredentials credentials, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync("/login", new LoginRequestWire(credentials.Server, credentials.School, credentials.User, credentials.Key), cancellationToken);
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadFromJsonAsync<LoginResponseWire>(cancellationToken)
            ?? throw new WebUntisUnavailableException("WebUntis returned an empty login response.");
        return new WebUntisSession(body.SessionId);
    }

    public async Task<IReadOnlyList<WebUntisLesson>> GetTimetableAsync(
        WebUntisSession session,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(
            $"/timetable?start={from:yyyy-MM-dd}&end={to:yyyy-MM-dd}",
            session,
            cancellationToken);
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadFromJsonAsync<List<LessonOutWire>>(cancellationToken)
            ?? throw new WebUntisUnavailableException("WebUntis returned an empty timetable response.");
        return body.ConvertAll(ToDomain);
    }

        public async Task<WebUntisProfile?> GetProfileAsync(WebUntisSession session, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync("/profile", session, cancellationToken);
        // A WebUntis without /profile means no profile, not a failed poll.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadFromJsonAsync<ProfileWire>(cancellationToken)
            ?? throw new WebUntisUnavailableException("WebUntis returned an empty profile response.");
        if (string.IsNullOrWhiteSpace(body.DisplayName))
        {
            return null;
        }

        var className = string.IsNullOrWhiteSpace(body.ClassName) ? null : body.ClassName.Trim();
        return new WebUntisProfile(body.DisplayName.Trim(), className);
    }

    private async Task<HttpResponseMessage> PostAsync<TBody>(string requestUri, TBody body, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
        }
        catch (Exception exception) when (IsConnectivityFailure(exception, cancellationToken))
        {
            throw new WebUntisUnavailableException("Could not reach WebUntis.", exception);
        }
    }

    private async Task<HttpResponseMessage> GetAsync(string requestUri, WebUntisSession session, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.SessionId);
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsConnectivityFailure(exception, cancellationToken))
        {
            throw new WebUntisUnavailableException("Could not reach WebUntis.", exception);
        }
    }

    /// <summary>
    /// True for a network failure or an HttpClient-side timeout (both surface as
    /// <see cref="TaskCanceledException"/>, since HttpClient.Timeout cancels its own internal
    /// token) — false when the caller's own <paramref name="cancellationToken"/> was cancelled, so
    /// that case still propagates as an ordinary <see cref="OperationCanceledException"/>.
    /// </summary>
    private static bool IsConnectivityFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new WebUntisCredentialsRejectedException("WebUntis rejected the credentials or session.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new WebUntisUnavailableException($"WebUntis returned {(int)response.StatusCode} {response.StatusCode}.");
        }
    }

    private static WebUntisLesson ToDomain(LessonOutWire wire)
    {
        LessonStatus status;
        try
        {
            status = LessonStatusExtensions.ParseWireValue(wire.Status);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            // Not a credentials problem, and IWebUntisClient only exposes two exception types —
            // a status outside {scheduled, cancelled, irregular} is a backend contract break, so
            // it's classified the same as any other malformed response from WebUntis.
            throw new WebUntisUnavailableException($"WebUntis returned an unrecognized lesson status '{wire.Status}'.", exception);
        }

        return new WebUntisLesson(
            wire.UntisLessonId,
            wire.SubjectCode,
            wire.SubjectName,
            Lecturer: null,
            wire.Room,
            status,
            wire.StartTs,
            wire.EndTs);
        
        
    }

    private sealed record LoginRequestWire(
        [property: JsonPropertyName("server")] string Server,
        [property: JsonPropertyName("school")] string School,
        [property: JsonPropertyName("user")] string User,
        [property: JsonPropertyName("key")] string Key);

    private sealed record LoginResponseWire(
        [property: JsonPropertyName("session_id")] string SessionId);

    private sealed record LessonOutWire(
        [property: JsonPropertyName("untis_lesson_id")] string UntisLessonId,
        [property: JsonPropertyName("subject_code")] string SubjectCode,
        [property: JsonPropertyName("subject_name")] string SubjectName,
        [property: JsonPropertyName("room")] string Room,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("start_ts")] DateTimeOffset StartTs,
        [property: JsonPropertyName("end_ts")] DateTimeOffset EndTs);
        private sealed record ProfileWire(
            [property: JsonPropertyName("display_name")] string? DisplayName,
            [property: JsonPropertyName("class_name")] string? ClassName);
}
