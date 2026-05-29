namespace DonkeyWork.Recordings.Audio.Core.Options;

public sealed class GptOssOptions
{
    public const string SectionName = "GptOss";

    public string BaseUrl { get; set; } = "http://ollama.ollama-gpt-oss.svc.cluster.local:11434/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-oss:20b";

    public double Temperature { get; set; } = 0.7;

    public int MaxTokens { get; set; } = 4096;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
