using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class StructuredExtractionInputSelectionTests
{
    [Fact]
    public async Task FolderSelection_IsNonRecursiveByDefault()
    {
        var root = CreateFolder();

        try
        {
            File.WriteAllBytes(Path.Combine(root, "a.png"), [1, 2, 3]);
            File.WriteAllText(Path.Combine(root, "notes.txt"), "unsupported");
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllText(
                Path.Combine(root, "nested", "ignored.txt"),
                "not selected");

            var result = await InputSelectionBuilder.BuildFolderAsync(root);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.NotNull(result.Value!.Submission);
            Assert.Equal(2, result.Value.DiscoveredItemCount);
            Assert.Equal(
                ["a.png", "notes.txt"],
                result.Value.Submission!.Items
                    .Select(static item => item.FileName)
                    .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
        finally
        {
            DeleteFolder(root);
        }
    }

    [Fact]
    public async Task FolderSelection_WithSubfolders_UsesBoundedDepthAndItemLimit()
    {
        var root = CreateFolder();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            Directory.CreateDirectory(
                Path.Combine(root, "nested", "deeper"));

            File.WriteAllBytes(Path.Combine(root, "a.png"), [1]);
            File.WriteAllBytes(Path.Combine(root, "b.png"), [1]);
            File.WriteAllBytes(
                Path.Combine(root, "nested", "c.png"),
                [1]);
            File.WriteAllBytes(
                Path.Combine(root, "nested", "deeper", "d.png"),
                [1]);

            var result = await InputSelectionBuilder.BuildAsync(
                root,
                InputSelectionMode.Folder,
                new InputSelectionOptions(
                    includeSubfolders: true,
                    maxDepth: 1,
                    maxItemCount: 2));

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(3, result.Value!.DiscoveredItemCount);
            Assert.NotNull(result.Value.Submission);
            Assert.Equal(2, result.Value.Submission!.Items.Count);

            var limitFailure = Assert.Single(
                result.Value.Failures
                    .Where(
                        failure =>
                            failure.Error.Code ==
                            "hive.input.selection-item-limit"));

            Assert.Contains(
                Path.Combine(root, "nested", "c.png"),
                limitFailure.SourcePath,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteFolder(root);
        }
    }

    [Fact]
    public async Task SingleFileSelection_DetectsBoundedMediaTypes()
    {
        var root = CreateFolder();

        try
        {
            var path = Path.Combine(root, "invoice.jpg");
            File.WriteAllBytes(path, [1, 2, 3]);

            var result = await InputSelectionBuilder.BuildSingleFileAsync(path);

            Assert.True(result.IsSuccess, result.Error?.Message);

            var item = Assert.Single(result.Value!.Submission!.Items);
            Assert.Equal("image/jpeg", item.MediaType);
            Assert.Equal([1, 2, 3], item.Content.ToArray());
        }
        finally
        {
            DeleteFolder(root);
        }
    }

    [Fact]
    public async Task FolderSelection_PropagatesCancellation()
    {
        var root = CreateFolder();

        try
        {
            File.WriteAllBytes(
                Path.Combine(root, "invoice.png"),
                [1, 2, 3]);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => InputSelectionBuilder.BuildFolderAsync(
                    root,
                    cancellationToken: cancellation.Token));
        }
        finally
        {
            DeleteFolder(root);
        }
    }

    private static string CreateFolder()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Hive_Test_Phase117_Input_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteFolder(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
