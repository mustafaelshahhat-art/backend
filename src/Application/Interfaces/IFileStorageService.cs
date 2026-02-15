using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a file from a stream and returns the public URL.
    /// </summary>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by its public URL.
    /// </summary>
    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
}
