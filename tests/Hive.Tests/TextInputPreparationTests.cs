using System.Text;
using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class TextInputPreparationTests
{
    [Fact]
    public async Task SingleFileSelection_RecognizesTxtAsTextPlain()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "HiveTextInput_" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            await File.WriteAllTextAsync(
                path,
                "Invoice INV-1",
                new UTF8Encoding(false));

            var result = await InputSelectionBuilder.BuildSingleFileAsync(path);

            Assert.True(result.IsSuccess, result.Error?.Message);
            var item = Assert.Single(result.Value!.Submission!.Items);
            Assert.Equal("text/plain", item.MediaType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TextInput_PreparesUtf8Content()
    {
        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.txt",
                "text/plain",
                Encoding.UTF8.GetBytes("Invoice INV-1\r\nCustomer: Ada"))
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.Failures);

        var text = Assert.IsType<PreparedTextInput>(
            Assert.Single(result.Value.PreparedInputs));

        Assert.Equal("Invoice INV-1\r\nCustomer: Ada", text.Content);
        Assert.Equal(InputSourceKind.Text, text.SourceKind);
        Assert.Equal("text/plain", text.MediaType);
    }

    [Fact]
    public void TextInput_InvalidUtf8IsReportedSafely()
    {
        var submission = new InputSubmission(
        [
            new InputItem(
                "invalid.txt",
                "text/plain",
                new byte[] { 0xC3, 0x28 })
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.PreparedInputs);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("hive.input.text-invalid-utf8", failure.Error.Code);
        Assert.Equal(ErrorCategory.Serialization, failure.Error.Category);
        Assert.DoesNotContain("C3", failure.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TextInput_RejectsEmptyContent()
    {
        var submission = new InputSubmission(
        [
            new InputItem(
                "empty.txt",
                "text/plain",
                Encoding.UTF8.GetBytes("   \r\n"))
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.PreparedInputs);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("hive.input.text-empty", failure.Error.Code);
        Assert.Equal(ErrorCategory.Validation, failure.Error.Category);
    }

    [Fact]
    public void TextInput_EnforcesBoundedFileSize()
    {
        var content = new byte[InputPreparationLimits.MaxTextBytes + 1];

        var submission = new InputSubmission(
        [
            new InputItem(
                "large.txt",
                "text/plain",
                content)
        ]);

        var result = InputPreparationEngine.Prepare(
            submission,
            Array.Empty<ExecutionTarget>());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.PreparedInputs);

        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal("hive.input.text-too-large", failure.Error.Code);
        Assert.Equal(ErrorCategory.Validation, failure.Error.Category);
    }

    [Fact]
    public void TextInput_PreparedContractEnforcesCharacterBound()
    {
        var content = new string('x', InputPreparationLimits.MaxTextCharacters + 1);

        Assert.Throws<ArgumentException>(
            () => new PreparedTextInput(
                Guid.NewGuid(),
                0,
                "large.txt",
                "text/plain",
                content));
    }

    [Fact]
    public void TextInput_DoesNotRequireVisionRouting()
    {
        var submission = new InputSubmission(
        [
            new InputItem(
                "invoice.txt",
                "text/plain",
                Encoding.UTF8.GetBytes("Invoice INV-1"))
        ]);

        Assert.False(InputPreparationEngine.RequiresVisionTargetRouting(submission));
    }
}
