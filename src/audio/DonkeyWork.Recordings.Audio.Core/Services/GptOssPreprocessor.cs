using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed class GptOssPreprocessor : IGptOssPreprocessor
{
    private const string SystemPromptTemplate = """
        You are a text-to-speech pre-processor for a podcast service. Rewrite the user's text into
        natural paragraphs suitable for spoken audio in {LANGUAGE}.

        Tone for this channel: {TONE}.

        Rules:
        - Return an ordered list of spoken paragraphs (the response schema enforces the shape).
        - One paragraph per natural breath-pause unit (typically 1-4 sentences).
        - Convey pacing and emphasis using ordinary punctuation only — commas, periods, question
          marks, dashes, and ellipses. The voice renders pauses from punctuation and paragraph breaks.
        - Output plain spoken words ONLY. Do NOT emit SSML, HTML, markdown, or any bracketed control
          tokens such as [PAUSE=500] or [EMPHASIS=...]; the voice cannot interpret them and would read
          them aloud verbatim.
        - Strip URLs, headers, code blocks, table syntax, and anything else that would not sound natural read aloud.
        - Preserve the meaning and order of the source material.
        """;

    // Strict structured-output schema — the model is forced to return exactly this shape.
    private const string ParagraphsSchema = """
        {
          "type": "object",
          "properties": {
            "paragraphs": {
              "type": "array",
              "description": "The rewritten spoken paragraphs, in order.",
              "items": { "type": "string" }
            }
          },
          "required": ["paragraphs"],
          "additionalProperties": false
        }
        """;

    private readonly ChatClient _chatClient;
    private readonly GptOssOptions _options;

    public GptOssPreprocessor(IOptions<GptOssOptions> options)
    {
        _options = options.Value;

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_options.BaseUrl),
            NetworkTimeout = _options.RequestTimeout,
        };

        // Ollama (the default backend) ignores auth; sluice requires a Bearer key. ApiKeyCredential
        // rejects an empty string, so fall back to a placeholder when no key is configured.
        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? "no-key" : _options.ApiKey;

        _chatClient = new ChatClient(_options.Model, new ApiKeyCredential(apiKey), clientOptions);
    }

    public async Task<IReadOnlyList<string>> PreprocessAsync(GptOssPreprocessRequest request, CancellationToken cancellationToken = default)
    {
        var tone = string.IsNullOrWhiteSpace(request.ChannelTone) ? "natural conversational delivery" : request.ChannelTone;
        var systemPrompt = SystemPromptTemplate
            .Replace("{LANGUAGE}", request.Language)
            .Replace("{TONE}", tone);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(request.RawText),
        };

        var completionOptions = new ChatCompletionOptions
        {
            Temperature = (float)_options.Temperature,
            MaxOutputTokenCount = _options.MaxTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "tts_paragraphs",
                BinaryData.FromString(ParagraphsSchema),
                jsonSchemaIsStrict: true),
        };

        var completion = await _chatClient.CompleteChatAsync(messages, completionOptions, cancellationToken);

        var content = completion.Value.Content.FirstOrDefault(part => part.Kind == ChatMessageContentPartKind.Text)?.Text
            ?? throw new InvalidOperationException("gpt-oss response had no message content.");

        var parsed = JsonSerializer.Deserialize<PreprocessPayload>(content)
            ?? throw new InvalidOperationException("gpt-oss response did not parse as JSON.");

        return parsed.Paragraphs ?? Array.Empty<string>();
    }

    private sealed record PreprocessPayload
    {
        [JsonPropertyName("paragraphs")]
        public IReadOnlyList<string>? Paragraphs { get; init; }
    }
}
