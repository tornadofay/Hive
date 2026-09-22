namespace Hive.Core;

public sealed record WorkItemAttachmentMetadata
{
    public WorkItemAttachmentMetadata(
        string fileName,
        string mediaType,
        long contentLength,
        string sha256)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Attachment file name is required.", nameof(fileName));

        if (fileName.Length > 260 || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException(
                "Attachment file name must be a leaf file name no longer than 260 characters.",
                nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(mediaType) ||
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "V1 WorkItem attachments must use an image media type.",
                nameof(mediaType));
        }

        if (contentLength <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Attachment content must not be empty.");

        if (contentLength > WorkItemImageSubmission.MaxContentBytes)
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                $"Attachment content cannot exceed {WorkItemImageSubmission.MaxContentBytes} bytes.");

        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
            throw new ArgumentException(
                "Attachment SHA-256 must be a 64-character hexadecimal string.",
                nameof(sha256));

        if (!sha256.All(static character => Uri.IsHexDigit(character)))
            throw new ArgumentException(
                "Attachment SHA-256 must contain only hexadecimal characters.",
                nameof(sha256));

        FileName = fileName;
        MediaType = mediaType.Trim();
        ContentLength = contentLength;
        Sha256 = sha256.ToLowerInvariant();
    }

    public string FileName { get; }

    public string MediaType { get; }

    public long ContentLength { get; }

    public string Sha256 { get; }
}

public sealed class WorkItemImageSubmission
{
    public const int MaxContentBytes = 10 * 1024 * 1024;

    public WorkItemImageSubmission(
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Image file name is required.", nameof(fileName));

        if (fileName.Length > 260 || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException(
                "Image file name must be a leaf file name no longer than 260 characters.",
                nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(mediaType) ||
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Image submissions must use an image media type.",
                nameof(mediaType));
        }

        if (content.Length <= 0)
            throw new ArgumentException(
                "Image content is required.",
                nameof(content));

        if (content.Length > MaxContentBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(content),
                content.Length,
                $"Image submissions cannot exceed {MaxContentBytes} bytes.");
        }

        FileName = fileName;
        MediaType = mediaType.Trim();
        Content = content.ToArray();
    }

    public string FileName { get; }

    public string MediaType { get; }

    public ReadOnlyMemory<byte> Content { get; }
}

public sealed class WorkItemAttachmentContent
{
    public WorkItemAttachmentContent(
        WorkItemAttachmentMetadata metadata,
        ReadOnlyMemory<byte> content)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        if (content.Length != metadata.ContentLength)
        {
            throw new ArgumentException(
                "Attachment content length does not match its metadata.",
                nameof(content));
        }

        Content = content.ToArray();
    }

    public WorkItemAttachmentMetadata Metadata { get; }

    public ReadOnlyMemory<byte> Content { get; }
}
