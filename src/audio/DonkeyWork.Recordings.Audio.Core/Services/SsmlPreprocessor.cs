using System.Net;
using System.Text.RegularExpressions;
using DonkeyWork.Recordings.Audio.Contracts.Services;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed partial class SsmlPreprocessor : ISsmlPreprocessor
{
    public string Wrap(string chunkWithInlineTokens)
    {
        if (string.IsNullOrEmpty(chunkWithInlineTokens))
        {
            return "<speak></speak>";
        }

        var escaped = WebUtility.HtmlEncode(chunkWithInlineTokens);

        var withBreaks = PauseTokenRegex().Replace(escaped, match =>
        {
            var ms = match.Groups[1].Value;
            return $"<break time=\"{ms}ms\"/>";
        });

        var withoutEmphasis = EmphasisTokenRegex().Replace(withBreaks, match => match.Groups[2].Value);

        return $"<speak>{withoutEmphasis}</speak>";
    }

    [GeneratedRegex(@"\[PAUSE=(\d+)ms\]", RegexOptions.IgnoreCase)]
    private static partial Regex PauseTokenRegex();

    [GeneratedRegex(@"\[EMPHASIS=([a-z]+)\](.*?)\[/EMPHASIS\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EmphasisTokenRegex();
}
