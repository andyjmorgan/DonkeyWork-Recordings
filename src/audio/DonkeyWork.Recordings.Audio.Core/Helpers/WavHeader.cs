using System.Buffers.Binary;
using System.Text;

namespace DonkeyWork.Recordings.Audio.Core.Helpers;

public static class WavHeader
{
    // Reads the duration of a PCM WAV clip straight from its RIFF header (data size / byte rate).
    // Much cheaper than shelling out to ffprobe per chunk. Returns null for anything that does not
    // parse as a plain RIFF/WAVE file — callers treat the duration as best-effort metadata.
    public static double? TryGetDurationSeconds(ReadOnlySpan<byte> wavBytes)
    {
        if (wavBytes.Length < 44
            || !wavBytes[..4].SequenceEqual("RIFF"u8)
            || !wavBytes[8..12].SequenceEqual("WAVE"u8))
        {
            return null;
        }

        uint? byteRate = null;
        uint? dataSize = null;

        // Walk the RIFF chunk list: [id:4][size:4][payload:size (word aligned)].
        var offset = 12;
        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(wavBytes.Slice(offset, 4));
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(wavBytes.Slice(offset + 4, 4));

            if (chunkId == "fmt " && offset + 8 + 16 <= wavBytes.Length)
            {
                byteRate = BinaryPrimitives.ReadUInt32LittleEndian(wavBytes.Slice(offset + 8 + 8, 4));
            }
            else if (chunkId == "data")
            {
                // Streamed WAVs sometimes carry a placeholder size; clamp to what is actually present.
                var available = (uint)Math.Max(0, wavBytes.Length - (offset + 8));
                dataSize = Math.Min(chunkSize, available);
            }

            if (byteRate.HasValue && dataSize.HasValue)
            {
                break;
            }

            var advance = 8L + chunkSize + (chunkSize % 2);
            if (advance <= 0 || offset + advance > int.MaxValue)
            {
                return null;
            }

            offset += (int)advance;
        }

        if (byteRate is null or 0 || dataSize is null)
        {
            return null;
        }

        return (double)dataSize.Value / byteRate.Value;
    }
}
