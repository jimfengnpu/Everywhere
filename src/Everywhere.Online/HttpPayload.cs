using Everywhere.Extensions;
using MessagePack;

namespace Everywhere.Online;

public partial class HttpPayload
{
    [Key("code")]
    public int Code { get; set; } = 1;

    [Key("message")]
    public string? Message { get; set; }

    [Key("timestamp")]
    public long Timestamp { get; set; } = TimeExtension.CurrentTimestamp;

    public void EnsureSuccessStatusCode()
    {
        if (Code != 1) throw new HttpRequestException($"({Code}) {Message}");
    }

    public override string ToString() =>
        $"{{\n" +
        $"	{nameof(Code)}: {Code},\n" +
        $"	{nameof(Message)}: \"{Message}\",\n" +
        $"	Time: \"{Timestamp.ToLocalTime():s}\"\n" +
        $"}}\n";
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
        $"{{\n" +
        $"	{nameof(Data)}: {Data},\n" +
        $"	{nameof(Code)}: {Code},\n" +
        $"	{nameof(Message)}: \"{Message}\",\n" +
        $"	Time: \"{Timestamp.ToLocalTime():s}\"\n" +
        $"}}\n";

    public T EnsureData()
    {
        EnsureSuccessStatusCode();
        return Data ?? throw new HttpRequestException(Message ?? $"{nameof(Data)} is null");
    }
}