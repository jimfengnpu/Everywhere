using MessagePack;

namespace Everywhere.Online;

public partial class HttpPayload
{
    [Key("code")]
    public int Code { get; set; } = 1;

    [Key("message")]
    public string? Message { get; set; }

    [Key("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.UtcTicks;

    public void EnsureSuccessStatusCode()
    {
        if (Code != 1) throw new HttpRequestException($"({Code}) {Message}");
    }

    public override string ToString() =>
        $$"""
          {
            {{nameof(Code)}}: {{Code}},
            {{nameof(Message)}}: "{{Message}}",
            {{nameof(Timestamp)}}: "{{Timestamp}}"
          }
          """;
}

public partial class HttpPayload<T> : HttpPayload
{
    [Key("data")]
    public T? Data { get; set; }

    public HttpPayload() { }

    public HttpPayload(T data)
    {
        Data = data;
    }

    public override string ToString() =>
        $$"""
          {
            {{nameof(Data)}}: {{Data}},
            {{nameof(Code)}}: {{Code}},
            {{nameof(Message)}}: "{{Message}}",
            {{nameof(Timestamp)}}: "{{Timestamp}}"
          }
          """;

    public T EnsureData()
    {
        EnsureSuccessStatusCode();
        return Data ?? throw new HttpRequestException(Message ?? $"{nameof(Data)} is null");
    }
}