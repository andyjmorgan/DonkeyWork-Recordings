using System.Text.RegularExpressions;
using DonkeyWork.Recordings.Audio.Contracts.Services;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed partial class SsmlPreprocessor : ISsmlPreprocessor
{
    // Kokoro TTS takes raw UTF-8 text — no SSML, and it does NOT decode XML/HTML entities (it would
    // speak "&amp;" as "amp"). Callers supply plain spoken text, but as a defensive measure we strip
    // any stray bracketed control tokens (e.g. [PAUSE=500], [EMPHASIS=...]) that would otherwise be
    // read aloud verbatim, and pass the words through without HTML-encoding or a <speak> wrapper.
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
