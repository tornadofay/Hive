using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class StructuredExtractionContractsTests
{
    [Fact]
    public void TargetSchema_RejectsChildCollectionIdentityCollision()
    {
        var field = new StructuredTargetField(
            new SemanticFieldId("customer.name"),
            "Customer Name",
            StructuredValueType.String,
            required: true,
            placement: StructuredFieldPlacement.Child,
            childCollectionKey: "customer.name");

        Assert.Throws<ArgumentException>(
            () => new StructuredTargetSchema(
                [
                    new StructuredTargetField(
                        new SemanticFieldId("customer.name"),
                        "Customer Name",
                        StructuredValueType.String,
                        required: true),
                    field
                ]));
    }

    [Fact]
    public void Candidate_ValidatesRequiredTypesAndPreservesParentChildStructure()
    {
        var parent = new StructuredCandidateField(
            new SemanticFieldId("invoice.number"),
            StructuredValueType.String,
            "INV-1",
            StructuredValidationState.Valid);

        var child = new StructuredChildCandidate(
            "lines",
            [
                new StructuredCandidateField(
                    new SemanticFieldId("line.quantity"),
                    StructuredValueType.Int64,
                    "2",
                    StructuredValidationState.Valid)
            ]);

        var provenance = new StructuredCandidateProvenance(
            Guid.NewGuid(),
            4,
            "invoice.png",
            InputSourceKind.Image,
            null,
            null,
            null,
            null);

        var candidate = new StructuredCandidate(
            StructuredCandidateId.New(),
            provenance,
            [parent],
            [child],
            confidence: 0.85);

        Assert.True(candidate.IsValid);
        Assert.Equal("INV-1", candidate.Fields.Single().Value);
        Assert.Equal("lines", candidate.Children.Single().CollectionKey);
        Assert.Equal(0.85, candidate.Confidence);
    }

    [Fact]
    public void Candidate_WithMissingRequiredField_IsInvalidButReviewable()
    {
        var field = new StructuredCandidateField(
            new SemanticFieldId("invoice.number"),
            StructuredValueType.String,
            null,
            StructuredValidationState.Missing,
            "Required field was not returned.");

        var provenance = new StructuredCandidateProvenance(
            Guid.NewGuid(),
            0,
            "invoice.png",
            InputSourceKind.Image,
            null,
            null,
            null,
            null);

        var candidate = new StructuredCandidate(
            StructuredCandidateId.New(),
            provenance,
            [field]);

        Assert.False(candidate.IsValid);
        Assert.Equal(
            StructuredValidationState.Missing,
            candidate.Fields.Single().ValidationState);
        Assert.Contains(
            "Required",
            candidate.Fields.Single().ValidationMessage!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MappingValidation_RequiresRequiredSemanticFieldsAndKnownSourceColumns()
    {
        var schema = CreateSchema();

        var context = new SpreadsheetMappingContext(
            "context-1",
            "invoice.xlsx",
            "Orders",
            ["Invoice Number", "Amount"],
            [
                new Dictionary<string, string>
                {
                    ["Invoice Number"] = "INV-1",
                    ["Amount"] = "10.50"
                }
            ],
            "source-fingerprint",
            StructuredExtractionEngine.ComputeTargetSchemaFingerprint(schema));

        var mapping = StructuredExtractionEngine.ValidateMapping(
            context,
            schema,
            [
                new SpreadsheetMappingEntry(
                    "Invoice Number",
                    new SemanticFieldId("invoice.number")),
                new SpreadsheetMappingEntry(
                    "Unknown Column",
                    new SemanticFieldId("invoice.amount"))
            ],
            SpreadsheetMappingReviewState.Proposed);

        Assert.False(mapping.IsDeterministicallyValid);
        Assert.Contains(
            mapping.ValidationMessages,
            message => message.Contains("not present", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            mapping.ValidationMessages,
            message => message.Contains("Required semantic field", StringComparison.Ordinal));
    }

    private static StructuredTargetSchema CreateSchema() =>
        new(
            [
                new StructuredTargetField(
                    new SemanticFieldId("invoice.number"),
                    "Invoice Number",
                    StructuredValueType.String,
                    required: true),
                new StructuredTargetField(
                    new SemanticFieldId("invoice.amount"),
                    "Invoice Amount",
                    StructuredValueType.Decimal,
                    required: true)
            ]);
}
