using System.Diagnostics;
using System.Globalization;

namespace DonkeyWork.Recordings.Audio.Core.Helpers;

public static class AudioConverter
{
    public static byte[] ConcatWav(IReadOnlyList<byte[]> wavChunks)
    {
        if (wavChunks.Count == 0)
        {
            throw new ArgumentException("ConcatWav requires at least one chunk.", nameof(wavChunks));
        }

        if (wavChunks.Count == 1)
        {
            return wavChunks[0];
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"tts-concat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var clipPaths = new List<string>(wavChunks.Count);
            for (var i = 0; i < wavChunks.Count; i++)
            {
                var path = Path.Combine(workDir, $"clip-{i:D5}.wav");
                File.WriteAllBytes(path, wavChunks[i]);
                clipPaths.Add(path);
            }

            var listPath = Path.Combine(workDir, "list.txt");
            File.WriteAllLines(listPath, clipPaths.Select(p => $"file '{p.Replace("'", "'\\''")}'"));

            return RunFfmpeg(
                args: new[] { "-hide_banner", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", "-f", "wav", "pipe:1" },
                stdin: null);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { }
        }
    }

    public static byte[] WavToMp3(byte[] wavBytes, int bitrateKbps = 192)
    {
        return RunFfmpeg(
            args: new[] { "-hide_banner", "-loglevel", "error", "-i", "pipe:0", "-codec:a", "libmp3lame", "-b:a", $"{bitrateKbps}k", "-f", "mp3", "pipe:1" },
            stdin: wavBytes);
    }

    public static double ProbeDurationSeconds(byte[] mediaBytes)
    {
        // ffprobe reads only as much of -i pipe:0 as it needs and then stops draining stdin,
        // so piping a large payload deadlocks once the OS pipe buffer fills. Probe a seekable
        // temp file instead.
        var tempPath = Path.Combine(Path.GetTempPath(), $"tts-probe-{Guid.NewGuid():N}");
        File.WriteAllBytes(tempPath, mediaBytes);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(tempPath);
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("csv=p=0");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start ffprobe process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var output = stdoutTask.GetAwaiter().GetResult();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = stderrTask.GetAwaiter().GetResult();
                throw new InvalidOperationException($"ffprobe failed with exit code {process.ExitCode}: {error}");
            }

            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : 0.0;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static byte[] RunFfmpeg(IReadOnlyList<string> args, byte[]? stdin)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardInput = stdin is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg process.");

        var stdoutTask = Task.Run(() =>
        {
            using var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            return ms.ToArray();
        });
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (stdin is not null)
        {
            process.StandardInput.BaseStream.Write(stdin, 0, stdin.Length);
            process.StandardInput.BaseStream.Flush();
            process.StandardInput.Close();
        }

        var output = stdoutTask.GetAwaiter().GetResult();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = stderrTask.GetAwaiter().GetResult();
            throw new InvalidOperationException($"ffmpeg failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}
