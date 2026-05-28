namespace DonkeyWork.Recordings.Audio.Contracts.Services;

public interface ISsmlPreprocessor
{
    string Wrap(string chunkWithInlineTokens);
}
