using System.Text.RegularExpressions;
using DonkeyWork.Recordings.Audio.Contracts.Services;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed partial class SsmlPreprocessor : ISsmlPreprocessor
{
    // Magpie TTS takes raw UTF-8 text. It only honours the <phoneme> SSML tag (not break/emphasis)
    // and does NOT decode XML/HTML entities — it speaks "&amp;" as "amp", "&#39;" as garbled
    // syllables, etc. So we strip the control tokens the preprocessor may emit and pass the words
    // through verbatim, without HTML-encoding or a <speak> wrapper.
    public string Wrap(string chunkWithInlineTokens)
    {
        if (string.IsNullOrEmpty(chunkWithInlineTokens))
        {
            return string.Empty;
        }

        var withoutPauses = PauseTokenRegex().Replace(chunkWithInlineTokens, string.Empty);
        var withoutEmphasis = EmphasisTokenRegex().Replace(withoutPauses, match => match.Groups[1].Value);
        var cleaned = ResidualTokenRegex().Replace(withoutEmphasis, string.Empty);

        return cleaned.Trim();
    }

    [GeneratedRegex(@"\[PAUSE=[^\]]*\]", RegexOptions.IgnoreCase)]
    private static partial Regex PauseTokenRegex();

    [GeneratedRegex(@"\[EMPHASIS=[a-z]+\](.*?)\[/EMPHASIS\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EmphasisTokenRegex();

    [GeneratedRegex(@"\[/?(?:PAUSE|EMPHASIS)[^\]]*\]", RegexOptions.IgnoreCase)]
    private static partial Regex ResidualTokenRegex();
}
