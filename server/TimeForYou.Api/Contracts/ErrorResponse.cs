namespace TimeForYou.Api.Contracts;

/// <summary>The `Error` schema in docs/openapi.yaml — every non-2xx response body.</summary>
public sealed record ErrorResponse(string Message);
