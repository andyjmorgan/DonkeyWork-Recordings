using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class GptOssPreprocessor : IGptOssPreprocessor
{
    private const string SystemPromptTemplate = """
        You are a text-to-speech pre-processor for a podcast service. Rewrite the user's text into
        natural paragraphs suitable for spoken audio in {LANGUAGE}.

        Tone for this channel: {TONE}.

        Rules:
        - Output ONLY valid JSON: {"paragraphs": [string, string, ...]}.
        - One paragraph per natural breath-pause unit (typically 1-4 sentences).
        - Use inline tokens, sparingly, for emphasis:
            [PAUSE=Nms]                              — insert a silence of N milliseconds.
            [EMPHASIS=strong|moderate|reduced]...[/EMPHASIS]  — emphasise the wrapped phrase.
        - Do NOT emit any SSML or HTML tags. Do NOT include the raw markdown of the input.
        - Strip URLs, headers, code blocks, table syntax, and anything else that would not sound natural read aloud.
        - Preserve the meaning and order of the source material.
        """;

    private readonly HttpClient _httpClient;
    private readonly GptOssOptions _options;

    public GptOssPreprocessor(HttpClient httpClient, IOptions<GptOssOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> PreprocessAsync(GptOssPreprocessRequest request, CancellationToken cancellationToken = default)
    {
        var tone = string.IsNullOrWhiteSpace(request.ChannelTone) ? "natural conversational delivery" : request.ChannelTone;
        var systemPrompt = SystemPromptTemplate
            .Replace("{LANGUAGE}", request.Language)
            .Replace("{TONE}", tone);

        var chatRequest = new ChatRequest
        {
            Model = _options.Model,
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            ResponseFormat = new ResponseFormat("json_object"),
            Messages =
            [
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", request.RawText),
            ],
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", chatRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("gpt-oss returned an empty body.");

        var content = chatResponse.Choices.FirstOrDefault()?.Message?.Content
            ?? throw new InvalidOperationException("gpt-oss response had no message content.");

        var parsed = JsonSerializer.Deserialize<PreprocessPayload>(content)
            ?? throw new InvalidOperationException("gpt-oss response did not parse as JSON.");

        return parsed.Paragraphs ?? Array.Empty<string>();
    }

    private sealed record ChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<ChatMessage> Messages { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; init; }
    }

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type);

    private sealed record ChatResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<Choice> Choices { get; init; } = Array.Empty<Choice>();
    }

    private sealed record Choice
    {
        [JsonPropertyName("message")]
        public ChatResponseMessage? Message { get; init; }
    }

    private sealed record ChatResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }
    }

    private sealed record PreprocessPayload
    {
        [JsonPropertyName("paragraphs")]
        public IReadOnlyList<string>? Paragraphs { get; init; }
    }
}
