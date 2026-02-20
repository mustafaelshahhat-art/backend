using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
/// Cloud-ready file storage that writes to a configurable base path
/// and returns portable relative URLs. For production, configure
/// <see cref="FileStorageOptions.BasePath"/> to a network/NFS/blob-fuse mount
/// and <see cref="FileStorageOptions.PublicBaseUrl"/> to the CDN/blob URL.
///
/// Horizontal-scaling safe: no dependency on local wwwroot.
/// </summary>
public sealed class CloudFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<CloudFileStorageService> _logger;

    public CloudFileStorageService(
        IOptions<FileStorageOptions> options,
        ILogger<CloudFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var basePath = _options.BasePath;
        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(basePath, uniqueFileName);

        await using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await fileStream.CopyToAsync(outputStream, ct);

        _logger.LogInformation("File saved to {FilePath}", filePath);

        var publicUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/{uniqueFileName}";
        return publicUrl;
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        try
        {
            var fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);
            var filePath = Path.Combine(_options.BasePath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("File deleted: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from {FileUrl}", fileUrl);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration for file storage.
/// In production, set BasePath to blob mount and PublicBaseUrl to CDN URL.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Physical base path for file storage.
    /// Default: wwwroot/uploads (backward compatible with LocalFileStorageService).
    /// Production: /mnt/blob/uploads or Azure Blob fuse mount.
    /// </summary>
    public string BasePath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

    /// <summary>
    /// Public URL prefix for stored files.
    /// Default: /uploads (relative, served by static files middleware).
    /// Production: https://cdn.example.com/uploads
    /// </summary>
    public string PublicBaseUrl { get; set; } = "/uploads";
}
