using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

// Asserts the /openai/v1 compatibility surface matches api.openai.com's live behaviour: the
// models list/single shapes, the six response_format content types, and the error envelope
// (error.message/type/param/code) for every failure mode. The expected shapes below were captured
// live from api.openai.com.
public class OpenAiCompatTests : IClassFixture<OpenAiCompatTestFixture>
{
    // ffmpeg is a service runtime dependency (AudioConverter shells out to it), but not every test
    // environment carries it. Conversion tests that need it no-op when it's absent — the
    // format→content-type mapping itself is covered by OpenAiCompatibilityTests unit tests.
    private static readonly bool FfmpegAvailable = ProbeFfmpeg();

    private readonly OpenAiCompatTestFixture _fixture;

    public OpenAiCompatTests(OpenAiCompatTestFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient(string? bearer = OpenAiCompatTestFixture.ValidApiKey)
    {
        var client = _fixture.Factory.CreateClient();
        if (bearer is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return client;
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static void AssertExactProperties(JsonElement element, params string[] expected)
    {
        var actual = element.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), actual);
    }

    private static void AssertModelObject(JsonElement model)
    {
        AssertExactProperties(model, "id", "object", "created", "owned_by");
        Assert.Equal("kokoro", model.GetProperty("id").GetString());
        Assert.Equal("model", model.GetProperty("object").GetString());
        Assert.Equal(JsonValueKind.Number, model.GetProperty("created").ValueKind);
        Assert.Equal(1780005101, model.GetProperty("created").GetInt64());
        Assert.Equal("donkeywork", model.GetProperty("owned_by").GetString());
    }

    private static void AssertErrorEnvelope(JsonElement root, string expectedType, string? expectedCode)
    {
        AssertExactProperties(root, "error");
        var error = root.GetProperty("error");
        AssertExactProperties(error, "message", "type", "param", "code");
        Assert.Equal(JsonValueKind.String, error.GetProperty("message").ValueKind);
        Assert.Equal(expectedType, error.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("param").ValueKind);

        if (expectedCode is null)
        {
            Assert.Equal(JsonValueKind.Null, error.GetProperty("code").ValueKind);
        }
        else
        {
            Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        }
    }

    private static JsonContent SpeechBody(
        string? model = "kokoro",
        string? input = "Hello from the compatibility surface.",
        string? voice = null,
        string? responseFormat = null,
        double? speed = null,
        string? streamFormat = null)
    {
        var body = new Dictionary<string, object?>();
        if (model is not null) body["model"] = model;
        if (input is not null) body["input"] = input;
        if (voice is not null) body["voice"] = voice;
        if (responseFormat is not null) body["response_format"] = responseFormat;
        if (speed is not null) body["speed"] = speed;
        if (streamFormat is not null) body["stream_format"] = streamFormat;
        return JsonContent.Create(body);
    }

    [Fact]
    public async Task Models_List_Matches_OpenAi_Shape()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/openai/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = await ReadJson(response);

        AssertExactProperties(root, "object", "data");
        Assert.Equal("list", root.GetProperty("object").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(1, data.GetArrayLength());
        AssertModelObject(data[0]);
    }

    [Fact]
    public async Task Models_Get_Returns_Bare_Model_Object()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/openai/v1/models/kokoro");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertModelObject(await ReadJson(response));
    }

    [Fact]
    public async Task Models_Get_Unknown_Returns_Model_Not_Found()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/openai/v1/models/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var root = await ReadJson(response);
        AssertErrorEnvelope(root, "invalid_request_error", "model_not_found");
        Assert.Equal(
            "The model `nope` does not exist or you do not have access to it.",
            root.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Speech_Unknown_Model_Returns_Model_Not_Found()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(model: "tts-1"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var root = await ReadJson(response);
        AssertErrorEnvelope(root, "invalid_request_error", "model_not_found");
        Assert.Equal(
            "The model `tts-1` does not exist or you do not have access to it.",
            root.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Speech_Unknown_Voice_Returns_400()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(voice: "marilyn"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var root = await ReadJson(response);
        AssertErrorEnvelope(root, "invalid_request_error", expectedCode: null);
        Assert.Contains("'alloy'", root.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Speech_Unknown_Format_Returns_400()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(responseFormat: "ogg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var root = await ReadJson(response);
        AssertErrorEnvelope(root, "invalid_request_error", expectedCode: null);
        Assert.Contains(
            "'mp3', 'aac', 'opus', 'flac', 'pcm' or 'wav'",
            root.GetProperty("error").GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(5.0)]
    public async Task Speech_Out_Of_Range_Speed_Returns_400(double speed)
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(speed: speed));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", expectedCode: null);
    }

    [Fact]
    public async Task Speech_Sse_Streaming_Returns_400()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(streamFormat: "sse"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", expectedCode: null);
    }

    [Fact]
    public async Task Speech_Input_Over_4096_Chars_Returns_400()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(input: new string('a', 4097)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", expectedCode: null);
    }

    [Fact]
    public async Task Speech_Missing_Input_Returns_400()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody(input: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", expectedCode: null);
    }

    [Fact]
    public async Task Missing_Bearer_Returns_401_With_OpenAi_Envelope()
    {
        using var client = CreateClient(bearer: null);
        using var response = await client.GetAsync("/openai/v1/models");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", "invalid_api_key");
    }

    [Fact]
    public async Task Invalid_Bearer_Returns_401_With_OpenAi_Envelope()
    {
        using var client = CreateClient(bearer: "sk-invalid");
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertErrorEnvelope(await ReadJson(response), "invalid_request_error", "invalid_api_key");
    }

    [Fact]
    public async Task X_Api_Key_Header_Also_Works_On_OpenAi_Surface()
    {
        using var client = CreateClient(bearer: null);
        client.DefaultRequestHeaders.Add("X-Api-Key", OpenAiCompatTestFixture.ValidApiKey);
        using var response = await client.GetAsync("/openai/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bearer_On_Non_OpenAi_Path_Is_Not_Treated_As_An_Api_Key()
    {
        // The Bearer-as-api-key rewrite must not weaken the normal REST surface: a valid api key
        // presented as a Bearer token outside /openai/* goes to the JwtBearer handler (where it is
        // not a valid JWT), never to the api-key handler.
        using var client = CreateClient();
        try
        {
            using var response = await client.GetAsync("/api/v1/voices");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            // The JwtBearer handler tried (and failed) to fetch metadata from the unreachable
            // test authority — proof the token was routed to JWT validation, not the api-key path.
        }
    }

    [Fact]
    public async Task Speech_Maps_OpenAi_Voice_Alias_To_Kokoro_Voice()
    {
        _fixture.TtsProvider.ClearRequests();

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(voice: "alloy", responseFormat: "wav"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_fixture.TtsProvider.Requests);
        Assert.Equal("af_alloy", request.Voice);
    }

    [Fact]
    public async Task Speech_Accepts_Native_Kokoro_Voice_Id()
    {
        _fixture.TtsProvider.ClearRequests();

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(voice: "bm_fable", responseFormat: "wav"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_fixture.TtsProvider.Requests);
        Assert.Equal("bm_fable", request.Voice);
    }

    [Fact]
    public async Task Speech_Defaults_To_Provider_Voice_When_Voice_Omitted()
    {
        _fixture.TtsProvider.ClearRequests();

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(responseFormat: "wav"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_fixture.TtsProvider.Requests);
        Assert.Equal("af_heart", request.Voice);
    }

    [Fact]
    public async Task Speech_Forwards_Speed_To_The_Provider()
    {
        _fixture.TtsProvider.ClearRequests();

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(speed: 2.0, responseFormat: "wav"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_fixture.TtsProvider.Requests);
        Assert.Equal(2.0, request.Speed);
    }

    [Theory]
    [InlineData("mp3", "audio/mpeg")]
    [InlineData("opus", "audio/opus")]
    [InlineData("aac", "audio/aac")]
    [InlineData("flac", "audio/flac")]
    [InlineData("wav", "audio/wav")]
    [InlineData("pcm", "audio/pcm")]
    public async Task Speech_Formats_Return_Audio_With_Exact_Content_Type(string format, string expectedContentType)
    {
        if (!FfmpegAvailable && format != "wav")
        {
            return; // No ffmpeg in this environment — mapping covered by OpenAiCompatibilityTests.
        }

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(responseFormat: format));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Speech_Defaults_To_Mp3_When_Format_Omitted()
    {
        if (!FfmpegAvailable)
        {
            return; // No ffmpeg in this environment — mapping covered by OpenAiCompatibilityTests.
        }

        using var client = CreateClient();
        using var response = await client.PostAsync("/openai/v1/audio/speech", SpeechBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Speech_Chunks_Long_Input_And_Concatenates()
    {
        if (!FfmpegAvailable)
        {
            return; // Multi-chunk stitching shells out to ffmpeg's concat demuxer.
        }

        _fixture.TtsProvider.ClearRequests();

        // ~3000 chars of sentences: over the chunker's 1500-char target (so it splits) but under
        // OpenAI's 4096 input limit (so the request is accepted).
        var input = string.Concat(Enumerable.Repeat("This is a perfectly ordinary sentence. ", 75)).TrimEnd();

        using var client = CreateClient();
        using var response = await client.PostAsync(
            "/openai/v1/audio/speech",
            SpeechBody(input: input, responseFormat: "wav"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(_fixture.TtsProvider.Requests.Count > 1, "Expected the input to be split across multiple synthesis calls.");
    }

    private static bool ProbeFfmpeg()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList = { "-version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
