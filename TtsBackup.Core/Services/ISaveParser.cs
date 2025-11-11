using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

public interface ISaveParser
{
    /// <summary>
    /// Parse a raw TTS save JSON into our internal SaveDocument + object tree.
    /// </summary>
    Task<SaveDocument> ParseAsync(string json, CancellationToken cancellationToken = default);
}