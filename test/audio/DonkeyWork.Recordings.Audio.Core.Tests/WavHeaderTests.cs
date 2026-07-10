using System.Text;
using DonkeyWork.Recordings.Audio.Core.Helpers;

namespace DonkeyWork.Recordings.Audio.Core.Tests;

public class WavHeaderTests
{
    [Fact]
    public void Parses_Duration_From_A_Pcm_Wav()
    {
        // 1 second of 24kHz 16-bit mono: byteRate = 48000, dataSize = 48000.
        var wav = BuildWav(sampleRate: 24_000, bitsPerSample: 16, channels: 1, dataSeconds: 1.0);

        var duration = WavHeader.TryGetDurationSeconds(wav);

        Assert.NotNull(duration);
        Assert.Equal(1.0, duration.Value, precision: 3);
    }

    [Fact]
    public void Parses_Fractional_Durations()
    {
        var wav = BuildWav(sampleRate: 22_050, bitsPerSample: 16, channels: 2, dataSeconds: 2.5);

        var duration = WavHeader.TryGetDurationSeconds(wav);

        Assert.NotNull(duration);
        Assert.Equal(2.5, duration.Value, precision: 3);
    }

    [Fact]
    public void Non_Wav_Bytes_Return_Null()
    {
        Assert.Null(WavHeader.TryGetDurationSeconds("this is definitely not a riff file"u8.ToArray()));
        Assert.Null(WavHeader.TryGetDurationSeconds([]));
        Assert.Null(WavHeader.TryGetDurationSeconds(new byte[16]));
    }

    [Fact]
    public void Truncated_Data_Chunk_Clamps_To_Available_Bytes()
    {
        var wav = BuildWav(sampleRate: 24_000, bitsPerSample: 16, channels: 1, dataSeconds: 1.0);
        // Chop off half the payload but leave the declared data size intact.
        var truncated = wav[..(wav.Length - 24_000)];

        var duration = WavHeader.TryGetDurationSeconds(truncated);

        Assert.NotNull(duration);
        Assert.Equal(0.5, duration.Value, precision: 3);
    }

    private static byte[] BuildWav(int sampleRate, int bitsPerSample, int channels, double dataSeconds)
    {
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var dataSize = (int)(byteRate * dataSeconds);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)(channels * (bitsPerSample / 8))); // block align
        writer.Write((short)bitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);

        writer.Flush();
        return ms.ToArray();
    }
}
