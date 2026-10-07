using System.Collections.ObjectModel;

namespace Hive.Core;

public enum InputSelectionMode
{
    SingleFile,
    Folder
}

public sealed class InputSelectionOptions
{
    public const int DefaultMaxDepth = 4;
    public const int MaxMaxDepth = 8;

    public InputSelectionOptions(
        bool includeSubfolders = false,
        int maxDepth = DefaultMaxDepth,
        int maxItemCount = InputSubmission.MaxItemCount,
        long maxTotalContentBytes = InputSubmission.MaxTotalContentBytes)
    {
        if (maxDepth < 0 || maxDepth > MaxMaxDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        if (maxItemCount <= 0 || maxItemCount > InputSubmission.MaxItemCount)
            throw new ArgumentOutOfRangeException(nameof(maxItemCount));

        if (maxTotalContentBytes <= 0 ||
            maxTotalContentBytes > InputSubmission.MaxTotalContentBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTotalContentBytes));
        }

        IncludeSubfolders = includeSubfolders;
        MaxDepth = maxDepth;
        MaxItemCount = maxItemCount;
        MaxTotalContentBytes = maxTotalContentBytes;
    }

    public bool IncludeSubfolders { get; }

    public int MaxDepth { get; }

    public int MaxItemCount { get; }

    public long MaxTotalContentBytes { get; }
}

public sealed record InputSelectionFailure
{
    public InputSelectionFailure(
        int sourceIndex,
        string sourcePath,
        Error error)
    {
        if (sourceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));

        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(error);

        SourceIndex = sourceIndex;
        SourcePath = sourcePath.Trim();
        Error = error;
    }

    public int SourceIndex { get; }

    public string SourcePath { get; }

    public Error Error { get; }
}

public sealed class InputSelectionResult
{
    public InputSelectionResult(
        InputSubmission? submission,
        IReadOnlyList<InputSelectionFailure> failures,
        int discoveredItemCount)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (discoveredItemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(discoveredItemCount));

        Submission = submission;
        Failures = new ReadOnlyCollection<InputSelectionFailure>(
            failures.ToArray());
        DiscoveredItemCount = discoveredItemCount;
    }

    public InputSubmission? Submission { get; }

    public IReadOnlyList<InputSelectionFailure> Failures { get; }

    public int DiscoveredItemCount { get; }

    public bool HasSubmission => Submission is not null;

    public bool HasFailures => Failures.Count != 0;
}

public static class InputSelectionBuilder
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp",
            ".gif",
            ".jpeg",
            ".jpg",
            ".png",
            ".webp"
        };

    public static Task<Result<InputSelectionResult>> BuildSingleFileAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        BuildAsync(
            filePath,
            InputSelectionMode.SingleFile,
            new InputSelectionOptions(),
            cancellationToken);

    public static Task<Result<InputSelectionResult>> BuildFolderAsync(
        string folderPath,
        bool includeSubfolders = false,
        CancellationToken cancellationToken = default) =>
        BuildAsync(
            folderPath,
            InputSelectionMode.Folder,
            new InputSelectionOptions(includeSubfolders),
            cancellationToken);

    public static async Task<Result<InputSelectionResult>> BuildAsync(
        string path,
        InputSelectionMode mode,
        InputSelectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(mode))
        {
            return Result<InputSelectionResult>.Failure(
                Error.Validation(
                    "hive.input.selection-mode-invalid",
                    "Input selection mode is invalid."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception) when (path.Length > 0)
        {
            return Result<InputSelectionResult>.Failure(
                Error.Validation(
                    "hive.input.selection-path-invalid",
                    "The selected input path is invalid."));
        }

        try
        {
            return mode switch
            {
                InputSelectionMode.SingleFile =>
                    await BuildSingleFileCoreAsync(
                        fullPath,
                        options,
                        cancellationToken).ConfigureAwait(false),
                InputSelectionMode.Folder =>
                    await BuildFolderCoreAsync(
                        fullPath,
                        options,
                        cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Result<InputSelectionResult>.Failure(
                new Error(
                    "hive.input.selection-access-denied",
                    ErrorCategory.Unauthorized,
                    "The selected input path cannot be accessed."));
        }
        catch (IOException)
        {
            return Result<InputSelectionResult>.Failure(
                new Error(
                    "hive.input.selection-io-failed",
                    ErrorCategory.External,
                    "The selected input path could not be read."));
        }
    }

    private static async Task<Result<InputSelectionResult>> BuildSingleFileCoreAsync(
        string path,
        InputSelectionOptions options,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Result<InputSelectionResult>.Failure(
                new Error(
                    "hive.input.selection-file-not-found",
                    ErrorCategory.NotFound,
                    "The selected input file was not found."));
        }

        var failure = await TryReadFileAsync(
            path,
            0,
            options.MaxTotalContentBytes,
            cancellationToken).ConfigureAwait(false);

        if (failure.Success)
        {
            return Result<InputSelectionResult>.Success(
                new InputSelectionResult(
                    new InputSubmission([failure.Item!]),
                    Array.Empty<InputSelectionFailure>(),
                    1));
        }

        return Result<InputSelectionResult>.Success(
            new InputSelectionResult(
                null,
                [failure.Failure!],
                1));
    }

    private static async Task<Result<InputSelectionResult>> BuildFolderCoreAsync(
        string folderPath,
        InputSelectionOptions options,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            return Result<InputSelectionResult>.Failure(
                new Error(
                    "hive.input.selection-folder-not-found",
                    ErrorCategory.NotFound,
                    "The selected input folder was not found."));
        }

        var sourceFiles = new List<string>();
        var failures = new List<InputSelectionFailure>();
        var discovered = 0;

        try
        {
            EnumerateFiles(
                folderPath,
                options,
                sourceFiles,
                failures,
                ref discovered,
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            failures.Add(
                new InputSelectionFailure(
                    discovered,
                    folderPath,
                    Error.Unauthorized(
                        "hive.input.selection-enumeration-access-denied",
                        "A folder or file could not be enumerated.")));
        }

        sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);

        var items = new List<InputItem>();
        var totalBytes = 0L;

        for (var index = 0; index < sourceFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (items.Count >= options.MaxItemCount)
            {
                failures.Add(
                    new InputSelectionFailure(
                        index,
                        sourceFiles[index],
                        Error.Validation(
                            "hive.input.selection-item-limit",
                            $"The selected folder exceeds the {options.MaxItemCount}-item input batch limit.")));
                continue;
            }

            var file = new FileInfo(sourceFiles[index]);
            if (!file.Exists)
            {
                failures.Add(
                    new InputSelectionFailure(
                        index,
                        sourceFiles[index],
                        new Error(
                            "hive.input.selection-file-disappeared",
                            ErrorCategory.NotFound,
                            "A selected file is no longer available.")));
                continue;
            }

            if (file.Length <= 0)
            {
                failures.Add(
                    new InputSelectionFailure(
                        index,
                        sourceFiles[index],
                        Error.Validation(
                            "hive.input.selection-empty-file",
                            "A selected input file is empty.")));
                continue;
            }

            if (file.Length > options.MaxTotalContentBytes ||
                file.Length > InputItem.MaxContentBytes ||
                totalBytes > options.MaxTotalContentBytes - file.Length)
            {
                failures.Add(
                    new InputSelectionFailure(
                        index,
                        sourceFiles[index],
                        Error.Validation(
                            "hive.input.selection-size-limit",
                            "The selected file would exceed the bounded input batch content limit.")));
                continue;
            }

            var read = await TryReadFileAsync(
                sourceFiles[index],
                index,
                options.MaxTotalContentBytes - totalBytes,
                cancellationToken).ConfigureAwait(false);

            if (!read.Success)
            {
                failures.Add(read.Failure!);
                continue;
            }

            items.Add(read.Item!);
            totalBytes += read.Item.Content.Length;
        }

        return Result<InputSelectionResult>.Success(
            new InputSelectionResult(
                items.Count == 0
                    ? null
                    : new InputSubmission(items),
                failures,
                discovered));
    }

    private static void EnumerateFiles(
        string folderPath,
        InputSelectionOptions options,
        List<string> files,
        List<InputSelectionFailure> failures,
        ref int discovered,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((folderPath, 0));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (current, depth) = pending.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(
                    current,
                    "*",
                    new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false,
                        ReturnSpecialDirectories = false
                    });
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(
                    new InputSelectionFailure(
                        discovered,
                        current,
                        new Error(
                            "hive.input.selection-enumeration-failed",
                            ErrorCategory.External,
                            "A folder could not be enumerated.")));
                continue;
            }

            var ordered = entries
                .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (var index = ordered.Length - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = ordered[index];

                if (Directory.Exists(entry))
                {
                    if (!options.IncludeSubfolders || depth >= options.MaxDepth)
                        continue;

                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            continue;
                    }
                    catch (Exception) when (true)
                    {
                        continue;
                    }

                    pending.Push((entry, depth + 1));
                    continue;
                }

                discovered++;

                if (files.Count >= options.MaxItemCount)
                    continue;

                files.Add(entry);
            }
        }
    }

    private static async Task<(bool Success, InputItem? Item, InputSelectionFailure? Failure)> TryReadFileAsync(
        string path,
        int sourceIndex,
        long remainingBudget,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    Error.Validation(
                        "hive.input.selection-file-name-invalid",
                        "The selected file has an invalid leaf name.")));
        }

        var info = new FileInfo(path);

        if (!info.Exists)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    new Error(
                        "hive.input.selection-file-not-found",
                        ErrorCategory.NotFound,
                        "The selected file was not found.")));
        }

        if (info.Length <= 0)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    Error.Validation(
                        "hive.input.selection-empty-file",
                        "The selected file is empty.")));
        }

        if (info.Length > InputItem.MaxContentBytes ||
            info.Length > remainingBudget)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    Error.Validation(
                        "hive.input.selection-size-limit",
                        "The selected file exceeds the bounded input content limit.")));
        }

        try
        {
            var bytes = new byte[checked((int)info.Length)];
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);

            var offset = 0;
            while (offset < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await stream
                    .ReadAsync(
                        bytes.AsMemory(offset, bytes.Length - offset),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    return (
                        false,
                        null,
                        new InputSelectionFailure(
                            sourceIndex,
                            path,
                            new Error(
                                "hive.input.selection-short-read",
                                ErrorCategory.External,
                                "The selected file changed while it was being read.")));
                }

                offset += read;
            }

            var mediaType = DetectMediaType(fileName);

            return (
                true,
                new InputItem(
                    fileName,
                    mediaType,
                    bytes),
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    new Error(
                        "hive.input.selection-file-access-denied",
                        ErrorCategory.Unauthorized,
                        "The selected file cannot be read.")));
        }
        catch (IOException)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    new Error(
                        "hive.input.selection-file-read-failed",
                        ErrorCategory.External,
                        "The selected file could not be read.")));
        }
        catch (OverflowException)
        {
            return (
                false,
                null,
                new InputSelectionFailure(
                    sourceIndex,
                    path,
                    Error.Validation(
                        "hive.input.selection-size-invalid",
                        "The selected file is too large for the bounded input representation.")));
        }
    }

    private static string DetectMediaType(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (ImageExtensions.Contains(extension))
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        if (string.Equals(
                extension,
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        return "application/octet-stream";
    }
}
