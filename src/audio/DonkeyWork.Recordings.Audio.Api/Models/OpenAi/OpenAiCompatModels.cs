using System.Text.Json.Serialization;

namespace DonkeyWork.Recordings.Audio.Api.Models.OpenAi;

// Wire shapes for the OpenAI-compatible surface. Property names are pinned with
// [JsonPropertyName] so the JSON matches api.openai.com exactly regardless of the host's
// serializer conventions.

public sealed class OpenAiModelObject
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public string Object { get; init; } = "model";

    [JsonPropertyName("created")]
    public required long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; init; }
}

public sealed class OpenAiModelList
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "list";

    [JsonPropertyName("data")]
    public required IReadOnlyList<OpenAiModelObject> Data { get; init; }
}

// All properties are nullable and unvalidated at binding time: the controller validates by hand so
// failures produce OpenAI's error envelope instead of ASP.NET ProblemDetails.
public sealed class OpenAiSpeechRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("input")]
    public string? Input { get; init; }

    [JsonPropertyName("voice")]
    public string? Voice { get; init; }

    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; init; }

    [JsonPropertyName("speed")]
    public double? Speed { get; init; }

    // "audio" (the default) is a plain audio response; "sse" is server-sent-events streaming,
    // which this service does not support.
    [JsonPropertyName("stream_format")]
    public string? StreamFormat { get; init; }

    // Accepted for wire compatibility (gpt-4o-mini-tts takes it); Kokoro has no equivalent, so it
    // is ignored.
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }
}

public sealed class OpenAiErrorEnvelope
{
    [JsonPropertyName("error")]
    public required OpenAiErrorBody Error { get; init; }

    public static OpenAiErrorEnvelope InvalidRequest(string message, string? code = null)
        => new()
        {
            Error = new OpenAiErrorBody
            {
                Message = message,
                Type = "invalid_request_error",
                Code = code,
            },
        };

    public static OpenAiErrorEnvelope ModelNotFound(string model)
        => InvalidRequest(
            $"The model `{model}` does not exist or you do not have access to it.",
            code: "model_not_found");

    public static OpenAiErrorEnvelope ServerError()
        => new()
        {
            Error = new OpenAiErrorBody
            {
                Message = "The server had an error while processing your request. Sorry about that!",
                Type = "server_error",
                Code = null,
            },
        };
}

public sealed class OpenAiErrorBody
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("param")]
    public string? Param { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }
}
