using DonkeyWork.Recordings.Audio.Core.Helpers;

namespace DonkeyWork.Recordings.Audio.Core.Tests;

public class OpenAiCompatibilityTests
{
    [Theory]
    [InlineData("alloy", "af_alloy")]
    [InlineData("ash", "am_michael")]
    [InlineData("coral", "af_bella")]
    [InlineData("echo", "am_echo")]
    [InlineData("fable", "bm_fable")]
    [InlineData("nova", "af_nova")]
    [InlineData("onyx", "am_onyx")]
    [InlineData("sage", "af_sarah")]
    [InlineData("shimmer", "af_heart")]
    public void VoiceAliases_Map_OpenAi_Names_To_Kokoro_Ids(string alias, string expected)
    {
        Assert.True(OpenAiCompatibility.TryResolveVoiceAlias(alias, out var kokoroId));
        Assert.Equal(expected, kokoroId);
    }

    [Theory]
    [InlineData("Alloy")]
    [InlineData("SHIMMER")]
    public void VoiceAliases_Are_Case_Insensitive(string alias)
    {
        Assert.True(OpenAiCompatibility.TryResolveVoiceAlias(alias, out _));
    }

    [Theory]
    [InlineData("af_heart")]
    [InlineData("marilyn")]
    [InlineData("")]
    public void VoiceAliases_Do_Not_Match_Non_OpenAi_Names(string voice)
    {
        Assert.False(OpenAiCompatibility.TryResolveVoiceAlias(voice, out _));
    }

    [Fact]
    public void VoiceAliases_Cover_Exactly_The_Nine_OpenAi_Voices()
    {
        var expected = new[] { "alloy", "ash", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer" };
        Assert.Equal(expected, OpenAiCompatibility.VoiceAliases.Keys.Order());
    }

    // Content types captured live from api.openai.com per response_format.
    [Theory]
    [InlineData("mp3", "audio/mpeg")]
    [InlineData("opus", "audio/opus")]
    [InlineData("aac", "audio/aac")]
    [InlineData("flac", "audio/flac")]
    [InlineData("wav", "audio/wav")]
    [InlineData("pcm", "audio/pcm")]
    public void Formats_Map_To_OpenAi_Content_Types(string format, string expectedContentType)
    {
        Assert.True(OpenAiCompatibility.TryGetContentType(format, out var contentType));
        Assert.Equal(expectedContentType, contentType);
    }

    [Fact]
    public void Format_Lookup_Is_Case_Insensitive()
    {
        Assert.True(OpenAiCompatibility.TryGetContentType("MP3", out var contentType));
        Assert.Equal("audio/mpeg", contentType);
    }

    [Theory]
    [InlineData("ogg")]
    [InlineData("m4a")]
    [InlineData("")]
    public void Unknown_Formats_Are_Rejected(string format)
    {
        Assert.False(OpenAiCompatibility.TryGetContentType(format, out _));
    }

    [Fact]
    public void ConvertWav_Wav_Is_A_Passthrough()
    {
        var wav = new byte[] { 1, 2, 3 };
        Assert.Same(wav, OpenAiCompatibility.ConvertWav(wav, "wav"));
    }

    [Fact]
    public void ConvertWav_Unknown_Format_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OpenAiCompatibility.ConvertWav([1], "ogg"));
    }

    [Fact]
    public void Model_Constants_Are_Stable()
    {
        Assert.Equal("kokoro", OpenAiCompatibility.ModelId);
        Assert.Equal("donkeywork", OpenAiCompatibility.ModelOwner);
        Assert.Equal(1780005101, OpenAiCompatibility.ModelCreatedUnixSeconds);
    }
}
