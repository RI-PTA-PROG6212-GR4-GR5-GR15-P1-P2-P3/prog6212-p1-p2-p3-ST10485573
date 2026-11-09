// Azure Blob Storage implementation with local storage for development and tests.
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PROG_API.Services;

public sealed class BlobStorageService
{
    private static readonly HashSet<string> AllowedContainers = new(StringComparer.Ordinal)
    {
        "event-banners",
        "profile-pictures"
    };

    private static readonly Dictionary<string, string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly string _rootFolder;
    private readonly BlobServiceClient? _blobService;

    public BlobStorageService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _rootFolder = configuration["BlobStorage:RootPath"] ?? "BlobStorage";

        var connectionString = configuration["BlobStorage:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _blobService = new BlobServiceClient(connectionString);
        }
        else if (!environment.IsDevelopment() && !configuration.GetValue<bool>("BlobStorage:UseLocalStorage"))
        {
            throw new InvalidOperationException(
                "Configure BlobStorage:ConnectionString or explicitly enable BlobStorage:UseLocalStorage.");
        }
    }

    public async Task<string> UploadAsync(IFormFile file, string container, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Choose a non-empty image file.", nameof(file));
        if (!AllowedContainers.Contains(container))
            throw new ArgumentException("The requested image container is not supported.", nameof(container));

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (!AllowedImageTypes.TryGetValue(extension, out var contentType) ||
            !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Upload a JPG, PNG, GIF or WebP image.", nameof(file));

        var maximumLength = container == "event-banners" ? 10_000_000 : 5_000_000;
        if (file.Length > maximumLength)
            throw new ArgumentException($"The image must be smaller than {maximumLength / 1_000_000} MB.", nameof(file));

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        if (_blobService is not null)
        {
            var blobContainer = _blobService.GetBlobContainerClient(container);
            await blobContainer.CreateIfNotExistsAsync(cancellationToken: ct);
            var blob = blobContainer.GetBlobClient(fileName);
            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, ct);

            // The API streams the private blob so the MVC app never connects to Azure.
            return $"/api/blob/{container}/{fileName}";
        }

        var safeContainer = Sanitize(container);
        var absoluteFolder = Path.Combine(_environment.ContentRootPath, _rootFolder, safeContainer);
        Directory.CreateDirectory(absoluteFolder);
        var absolutePath = Path.Combine(absoluteFolder, fileName);
        await using (var stream = System.IO.File.Create(absolutePath))
        {
            await file.CopyToAsync(stream, ct);
        }

        // Development/test files are served by the API static-file middleware.
        return $"/blob/{safeContainer}/{fileName}";
    }

    public async Task<(Stream Content, string ContentType)> OpenReadAsync(
        string container, string fileName, CancellationToken ct = default)
    {
        if (!AllowedContainers.Contains(container) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            !AllowedImageTypes.TryGetValue(Path.GetExtension(fileName), out var contentType))
            throw new FileNotFoundException("Image not found.");

        if (_blobService is not null)
        {
            var blob = _blobService.GetBlobContainerClient(container).GetBlobClient(fileName);
            var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return (result.Value.Content, result.Value.Details.ContentType ?? contentType);
        }

        var path = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _rootFolder, container, fileName));
        var folder = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _rootFolder, container)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
            throw new FileNotFoundException("Image not found.");

        return (System.IO.File.OpenRead(path), contentType);
    }

    public bool Delete(string relativeUrl)
    {
        if (_blobService is not null || string.IsNullOrWhiteSpace(relativeUrl))
            return false;

        var prefix = "/blob/";
        if (!relativeUrl.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var remainder = relativeUrl[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _rootFolder, remainder));
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _rootFolder)) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(absolutePath))
            return false;

        System.IO.File.Delete(absolutePath);
        return true;
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Where(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
}
