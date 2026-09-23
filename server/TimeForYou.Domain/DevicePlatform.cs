namespace TimeForYou.Domain;

/// <summary>Matches the `platform` enum in docs/openapi.yaml's Device schema.</summary>
public enum DevicePlatform
{
    AndroidFcm,
    WebPush,
}

public static class DevicePlatformExtensions
{
    public static string ToWireValue(this DevicePlatform platform) => platform switch
    {
        DevicePlatform.AndroidFcm => "android_fcm",
        DevicePlatform.WebPush => "web_push",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
    };

    public static DevicePlatform ParseWireValue(string value) => value switch
    {
        "android_fcm" => DevicePlatform.AndroidFcm,
        "web_push" => DevicePlatform.WebPush,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
