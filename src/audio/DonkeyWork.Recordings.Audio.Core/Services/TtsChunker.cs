using System.Text;
using System.Text.RegularExpressions;
using DonkeyWork.Recordings.Audio.Contracts.Services;

namespace DonkeyWork.Recordings.Audio.Core.Services;

public sealed partial class TtsChunker : ITtsChunker
{
    public IReadOnlyList<string> Chunk(string text, ChunkerOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var stripped = StripFormatting(text);
        var blocks = SplitParagraphs(stripped);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var block in blocks)
        {
            if (block.Length > options.MaxCharCount)
            {
                FlushCurrent(current, chunks);
                foreach (var split in SplitOversizedBlock(block, options))
                {
                    chunks.Add(split);
                }
                continue;
            }

            var separator = current.Length == 0 ? string.Empty : "\n\n";
            var projected = current.Length + separator.Length + block.Length;

            if (projected > options.TargetCharCount && current.Length > 0)
            {
                FlushCurrent(current, chunks);
                current.Append(block);
            }
            else
            {
                current.Append(separator).Append(block);
            }
        }

        FlushCurrent(current, chunks);
        return chunks;
    }

    private static void FlushCurrent(StringBuilder buffer, List<string> chunks)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        chunks.Add(buffer.ToString().Trim());
        buffer.Clear();
    }

    internal static string StripFormatting(string input)
    {
        var text = FencedCodeRegex().Replace(input, string.Empty);
        text = InlineCodeRegex().Replace(text, "$1");
        text = ImageRegex().Replace(text, "$1");
        text = LinkRegex().Replace(text, "$1");
        text = BoldItalicRegex().Replace(text, "$2");
        text = StrikethroughRegex().Replace(text, "$1");
        text = HeadingRegex().Replace(text, "$1");
        text = BlockquoteRegex().Replace(text, string.Empty);
        text = HorizontalRuleRegex().Replace(text, string.Empty);
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = ExcessNewlinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    internal static IEnumerable<string> SplitParagraphs(string text)
    {
        foreach (var raw in BlankLineRegex().Split(text))
        {
            var block = raw.Trim();
            if (block.Length == 0)
            {
                continue;
            }

            yield return block;
        }
    }

    private static IEnumerable<string> SplitOversizedBlock(string block, ChunkerOptions options)
    {
        if (LooksLikeList(block))
        {
            foreach (var piece in GreedyJoin(SplitListItems(block), options))
            {
                yield return piece;
            }
            yield break;
        }

        foreach (var piece in GreedyJoin(SplitSentences(block), options))
        {
            yield return piece;
        }
    }

    private static bool LooksLikeList(string block)
    {
        using var reader = new StringReader(block);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            return IsListItemStart(trimmed);
        }

        return false;
    }

    private static bool IsListItemStart(string trimmed)
    {
        if (trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length > 2 && char.IsDigit(trimmed[0]))
        {
            var dotIndex = trimmed.IndexOf('.');
            if (dotIndex > 0 && dotIndex < 4 && dotIndex + 1 < trimmed.Length && trimmed[dotIndex + 1] == ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitListItems(string block)
    {
        var current = new StringBuilder();
        using var reader = new StringReader(block);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimStart();

            if (IsListItemStart(trimmed) && current.Length > 0)
            {
                yield return current.ToString().TrimEnd();
                current.Clear();
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
        {
            yield return current.ToString().TrimEnd();
        }
    }

    private static IEnumerable<string> SplitSentences(string paragraph)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < paragraph.Length; i++)
        {
            current.Append(paragraph[i]);

            if (paragraph[i] is not ('.' or '?' or '!'))
            {
                continue;
            }

            var boundary = i + 1 >= paragraph.Length || char.IsWhiteSpace(paragraph[i + 1]);
            if (!boundary)
            {
                continue;
            }

            while (i + 1 < paragraph.Length && char.IsWhiteSpace(paragraph[i + 1]))
            {
                current.Append(paragraph[i + 1]);
                i++;
            }

            pieces.Add(current.ToString().Trim());
            current.Clear();
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString().Trim());
        }

        return pieces.Where(p => p.Length > 0);
    }

    private static IEnumerable<string> GreedyJoin(IEnumerable<string> parts, ChunkerOptions options)
    {
        var buffer = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > options.MaxCharCount)
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString().Trim();
                    buffer.Clear();
                }

                foreach (var slice in HardSplit(part, options.MaxCharCount))
                {
                    yield return slice;
                }
                continue;
            }

            var separator = buffer.Length == 0 ? string.Empty : "\n";
            if (buffer.Length + separator.Length + part.Length > options.TargetCharCount && buffer.Length > 0)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
                buffer.Append(part);
            }
            else
            {
                buffer.Append(separator).Append(part);
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString().Trim();
        }
    }

    private static IEnumerable<string> HardSplit(string value, int maxLength)
    {
        for (var start = 0; start < value.Length; start += maxLength)
        {
            var length = Math.Min(maxLength, value.Length - start);
            yield return value.Substring(start, length).Trim();
        }
    }

    [GeneratedRegex("```[\\s\\S]*?```", RegexOptions.Multiline)]
    private static partial Regex FencedCodeRegex();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"(\*\*|__|\*|_)(.+?)\1")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex("~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    [GeneratedRegex(@"^[ \t]*#{1,6}[ \t]+(.*)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[ \t]*>[ \t]?", RegexOptions.Multiline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^[ \t]*([-*_])([ \t]*\1){2,}[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlinesRegex();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLineRegex();
}
