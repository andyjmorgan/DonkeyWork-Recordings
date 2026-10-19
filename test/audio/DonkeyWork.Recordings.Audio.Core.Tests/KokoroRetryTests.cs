using System.Net;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Audio.Core.Options;
using DonkeyWork.Recordings.Audio.Core.Services;
using Microsoft.Extensions.Options;

namespace DonkeyWork.Recordings.Audio.Core.Tests;

public class KokoroRetryTests
{
    private static readonly byte[] Wav = [1, 2, 3, 4];

    [Fact]
    public async Task Retries_A_Dropped_Connection_And_Succeeds()
    {
        var handler = new ScriptedHandler(
            _ => throw new HttpRequestException("Connection reset by peer"),
            _ => Ok());
        var clip = await Provider(handler).SynthesizeAsync("hello", Request());

        Assert.Equal(Wav, clip.Audio);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Retries_Transient_Status_Codes(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(status), _ => Ok());
        await Provider(handler).SynthesizeAsync("hello", Request());
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Does_Not_Retry_Deterministic_Failures(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(status), _ => Ok());
        await Assert.ThrowsAsync<HttpRequestException>(() => Provider(handler).SynthesizeAsync("hello", Request()));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Gives_Up_After_MaxAttempts()
    {
        var handler = new ScriptedHandler(Enumerable.Repeat<Func<HttpRequestMessage, HttpResponseMessage>>(
            _ => throw new HttpRequestException("Connection refused"), 10).ToArray());
        await Assert.ThrowsAsync<HttpRequestException>(() => Provider(handler, maxAttempts: 3).SynthesizeAsync("hello", Request()));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Does_Not_Retry_When_The_Caller_Cancels()
    {
        using var cts = new CancellationTokenSource();
        var handler = new ScriptedHandler(_ => { cts.Cancel(); throw new TaskCanceledException(); }, _ => Ok());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Provider(handler).SynthesizeAsync("hello", Request(), cts.Token));
        Assert.Equal(1, handler.Calls);
    }

    private static TtsProviderRequest Request() => new("af_heart", "en-US");

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Wav) };

    private static KokoroTtsProvider Provider(ScriptedHandler handler, int maxAttempts = 4) =>
        new(new SingleClientFactory(handler), Microsoft.Extensions.Options.Options.Create(new KokoroOptions
        {
            BaseUrl = "http://kokoro.test",
            MaxAttempts = maxAttempts,
            RetryBaseDelay = TimeSpan.Zero,
        }));

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("http://kokoro.test/") };
    }

    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var step = steps[Math.Min(Calls, steps.Length - 1)];
            Calls++;
            return Task.FromResult(step(request));
        }
    }
}
