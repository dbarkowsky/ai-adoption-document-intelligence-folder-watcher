using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Enumerates and filters files in the inbox folder
/// </summary>
public interface IInboxEnumerator
{
    /// <summary>
    /// Scans the inbox folder and returns eligible files matching all filter criteria
    /// </summary>
    /// <returns>Collection of eligible files</returns>
    IEnumerable<IFileInfo> EnumerateEligibleFiles();
}
