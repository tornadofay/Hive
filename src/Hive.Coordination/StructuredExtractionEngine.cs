using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hive.Core;
using Hive.Providers.OpenAICompatible;

namespace Hive.Coordination;

public sealed class StructuredExtractionEngine
{
    private const string MappingSchemaName = "hive_spreadsheet_mapping";
    private const string CandidateSchemaName = "hive_structured_candidate";
    private const string StructuredOutputCapability = "structured.output";
    private const string VisionCapability = "vision";

    public async Task<Result<SpreadsheetMapping>> ProposeSpreadsheetMappingAsync(
        SpreadsheetMappingContext context,
        StructuredTargetSchema targetSchema,
        ExecutionTarget target,
        SecretMaterial? credential = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(target);

        if (!CanUseStructuredOutput(target))
        {
            return Result<SpreadsheetMapping>.Failure(
                Error.Unsupported(
                    "hive.structured-extraction.structured-output-unsupported",
                    "The selected execution target explicitly reports structured output as unsupported."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var prompt = BuildMappingPrompt(context, targetSchema);

        var response = await CompleteStructuredJsonAsync(
                target,
                credential,
                MappingSchemaName,
                BuildMappingJsonSchema(context, targetSchema),
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.System,
                        "You map source spreadsheet columns to the supplied stable semantic field identities. Never invent field identities. Return only the requested JSON object."),
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        prompt)
                ],
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsFailure)
            return Result<SpreadsheetMapping>.Failure(response.Error!);

        return ParseMapping(
            context,
            targetSchema,
            response.Value!);
    }

    public async Task<Result<StructuredCandidate>> ExtractImageAsync(
        PreparedImageInput input,
        StructuredTargetSchema targetSchema,
        ExecutionTarget target,
        SecretMaterial? credential = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(target);

        if (input.ExecutionTargetId != target.Id)
        {
            return Result<StructuredCandidate>.Failure(
                Error.Conflict(
                    "hive.structured-extraction.image-target-mismatch",
                    "The prepared image's execution target does not match the target supplied for extraction."));
        }

        if (!HasCapability(target, VisionCapability))
        {
            return Result<StructuredCandidate>.Failure(
                Error.Unsupported(
                    "hive.structured-extraction.vision-unsupported",
                    "The selected execution target is not explicitly vision-capable."));
        }

        if (!CanUseStructuredOutput(target))
        {
            return Result<StructuredCandidate>.Failure(
                Error.Unsupported(
                    "hive.structured-extraction.structured-output-unsupported",
                    "The selected execution target explicitly reports structured output as unsupported."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var provenance = new StructuredCandidateProvenance(
            input.SubmissionId,
            input.ItemIndex,
            input.FileName,
            InputSourceKind.Image,
            null,
            null,
            input.ExecutionTargetId,
            null);

        var prompt = BuildImagePrompt(targetSchema);

        var image = new OpenAICompatibleImageContent(
            input.MediaType,
            input.Content);

        var response = await CompleteStructuredJsonAsync(
                target,
                credential,
                CandidateSchemaName,
                BuildCandidateJsonSchema(targetSchema),
                [
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.System,
                        "Extract only information actually visible in the supplied image. Never invent missing values. Return only the requested JSON object."),
                    new OpenAICompatibleMessage(
                        OpenAICompatibleMessageRole.User,
                        prompt,
                        [image])
                ],
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsFailure)
            return Result<StructuredCandidate>.Failure(response.Error!);

        return ParseCandidate(
            response.Value!,
            targetSchema,
            provenance);
    }

    public Result<StructuredCandidate> ApplySpreadsheetMapping(
        PreparedSpreadsheetRowInput input,
        SpreadsheetMapping mapping,
        StructuredTargetSchema targetSchema)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(targetSchema);

        if (!mapping.IsDeterministicallyValid)
        {
            return Result<StructuredCandidate>.Failure(
                Error.Validation(
                    "hive.structured-extraction.mapping-invalid",
                    "The spreadsheet mapping must pass deterministic validation before it can be applied."));
        }

        var provenance = new StructuredCandidateProvenance(
            input.SubmissionId,
            input.ItemIndex,
            input.FileName,
            InputSourceKind.Spreadsheet,
            input.WorksheetName,
            input.RowNumber,
            null,
            mapping.Context.Identity);

        var fields = targetSchema.Fields
            .Where(static field => field.Placement == StructuredFieldPlacement.Parent)
            .Select(
                field => CreateMappedField(
                    field,
                    input.Values,
                    mapping.Entries))
            .ToArray();

        var childGroups = targetSchema.ChildFieldsByCollection;
        var children = new List<StructuredChildCandidate>(childGroups.Count);

        foreach (var group in childGroups)
        {
            var childFields = group.Value
                .Select(
                    field => CreateMappedField(
                        field,
                        input.Values,
                        mapping.Entries))
                .ToArray();

            children.Add(
                new StructuredChildCandidate(
                    group.Key,
                    childFields));
        }

        return Result<StructuredCandidate>.Success(
            new StructuredCandidate(
                StructuredCandidateId.New(),
                provenance,
                fields,
                children));
    }

    public Result<StructuredCandidate> ReplaceSpreadsheetCandidateValues(
        StructuredCandidate existingCandidate,
        IReadOnlyDictionary<string, string> sourceValues,
        SpreadsheetMapping mapping,
        StructuredTargetSchema targetSchema)
    {
        ArgumentNullException.ThrowIfNull(existingCandidate);
        ArgumentNullException.ThrowIfNull(sourceValues);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(targetSchema);

        var fields = targetSchema.Fields
            .Where(static field => field.Placement == StructuredFieldPlacement.Parent)
            .Select(
                field => CreateMappedField(
                    field,
                    sourceValues,
                    mapping.Entries))
            .ToArray();

        var children = targetSchema.ChildFieldsByCollection
            .Select(
                pair => new StructuredChildCandidate(
                    pair.Key,
                    pair.Value
                        .Select(
                            field => CreateMappedField(
                                field,
                                sourceValues,
                                mapping.Entries))
                        .ToArray()))
            .ToArray();

        return Result<StructuredCandidate>.Success(
            new StructuredCandidate(
                existingCandidate.Id,
                existingCandidate.Provenance,
                fields,
                children,
                existingCandidate.Confidence));
    }

    public static Result<SpreadsheetMapping> ValidateMapping(
        SpreadsheetMappingContext context,
        StructuredTargetSchema targetSchema,
        IReadOnlyList<SpreadsheetMappingEntry> entries,
        SpreadsheetMappingReviewState reviewState)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(entries);

        var errors = new List<string>();
        var sourceColumns = new HashSet<string>(context.SourceColumns, StringComparer.OrdinalIgnoreCase);
        var targetFields = targetSchema.Fields
            .Select(static field => field.Id.Value)
            .ToHashSet(StringComparer.Ordinal);

        var seenSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTarget = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!sourceColumns.Contains(entry.SourceColumn))
            {
                errors.Add(
                    $"Source column '{entry.SourceColumn}' is not present in the mapping context.");
            }

            if (!targetFields.Contains(entry.TargetFieldId.Value))
            {
                errors.Add(
                    $"Target semantic field '{entry.TargetFieldId}' is not present in the supplied target schema.");
            }

            if (!seenSource.Add(entry.SourceColumn))
            {
                errors.Add(
                    $"Source column '{entry.SourceColumn}' is mapped more than once.");
            }

            if (!seenTarget.Add(entry.TargetFieldId.Value))
            {
                errors.Add(
                    $"Target semantic field '{entry.TargetFieldId}' is mapped more than once.");
            }
        }

        foreach (var required in targetSchema.Fields.Where(static field => field.Required))
        {
            if (!seenTarget.Contains(required.Id.Value))
            {
                errors.Add(
                    $"Required semantic field '{required.Id}' is not mapped.");
            }
        }

        return new SpreadsheetMapping(
            context,
            entries,
            reviewState,
            errors);
    }

    public static string ComputeTargetSchemaFingerprint(
        StructuredTargetSchema targetSchema)
    {
        ArgumentNullException.ThrowIfNull(targetSchema);

        var normalized = string.Join(
            "|",
            targetSchema.Fields
                .OrderBy(static field => field.Id.Value, StringComparer.Ordinal)
                .Select(
                    field => string.Join(
                        ":",
                        field.Id.Value,
                        field.ValueType,
                        field.Required ? "1" : "0",
                        field.Placement,
                        field.ChildCollectionKey ?? string.Empty)));

        return Sha256(normalized);
    }

    public static string ComputeSourceStructureFingerprint(
        string worksheetName,
        IReadOnlyList<string> sourceColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentNullException.ThrowIfNull(sourceColumns);

        var normalized = string.Join(
            "|",
            new[] { worksheetName.Trim() }.Concat(
                sourceColumns.Select(static column => column.Trim())));

        return Sha256(normalized);
    }

    private static StructuredCandidateField CreateMappedField(
        StructuredTargetField targetField,
        IReadOnlyDictionary<string, string> sourceValues,
        IReadOnlyList<SpreadsheetMappingEntry> entries)
    {
        var mapping = entries.FirstOrDefault(
            entry => entry.TargetFieldId == targetField.Id);

        if (mapping is null ||
            !sourceValues.TryGetValue(mapping.SourceColumn, out var raw))
        {
            return new StructuredCandidateField(
                targetField.Id,
                targetField.ValueType,
                null,
                targetField.Required
                    ? StructuredValidationState.Missing
                    : StructuredValidationState.Valid,
                targetField.Required
                    ? "The required semantic field has no mapped source value."
                    : null);
        }

        return ConvertValue(
            targetField,
            raw);
    }

    private static Result<SpreadsheetMapping> ParseMapping(
        SpreadsheetMappingContext context,
        StructuredTargetSchema targetSchema,
        JsonElement json)
    {
        try
        {
            if (json.ValueKind != JsonValueKind.Object ||
                !json.TryGetProperty("mappings", out var mappingsElement) ||
                mappingsElement.ValueKind != JsonValueKind.Array ||
                mappingsElement.GetArrayLength() > InputPreparationLimits.MaxWorksheetColumns)
            {
                return Result<SpreadsheetMapping>.Failure(
                    Error.Serialization(
                        "hive.structured-extraction.mapping-output-invalid",
                        "The provider returned an invalid spreadsheet mapping object."));
            }

            var entries = new List<SpreadsheetMappingEntry>();

            foreach (var element in mappingsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty("sourceColumn", out var sourceColumn) ||
                    sourceColumn.ValueKind != JsonValueKind.String ||
                    !element.TryGetProperty("targetFieldId", out var targetFieldId) ||
                    targetFieldId.ValueKind != JsonValueKind.String)
                {
                    return Result<SpreadsheetMapping>.Failure(
                        Error.Serialization(
                            "hive.structured-extraction.mapping-output-invalid",
                            "A spreadsheet mapping entry is malformed."));
                }

                entries.Add(
                    new SpreadsheetMappingEntry(
                        sourceColumn.GetString()!,
                        new SemanticFieldId(targetFieldId.GetString()!)));
            }

            return Result<SpreadsheetMapping>.Success(
                ValidateMapping(
                    context,
                    targetSchema,
                    entries,
                    SpreadsheetMappingReviewState.Proposed));
        }
        catch (ArgumentException)
        {
            return Result<SpreadsheetMapping>.Failure(
                Error.Serialization(
                    "hive.structured-extraction.mapping-output-invalid",
                    "The provider returned invalid spreadsheet mapping data."));
        }
    }

    private static Result<StructuredCandidate> ParseCandidate(
        JsonElement json,
        StructuredTargetSchema targetSchema,
        StructuredCandidateProvenance provenance)
    {
        try
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                return Result<StructuredCandidate>.Failure(
                    Error.Serialization(
                        "hive.structured-extraction.candidate-output-invalid",
                        "The provider returned a structured candidate that is not a JSON object."));
            }

            double? confidence = null;

            if (json.TryGetProperty("confidence", out var confidenceElement))
            {
                if (confidenceElement.ValueKind != JsonValueKind.Number ||
                    !confidenceElement.TryGetDouble(out var parsedConfidence) ||
                    double.IsNaN(parsedConfidence) ||
                    double.IsInfinity(parsedConfidence) ||
                    parsedConfidence is < 0 or > 1)
                {
                    return Result<StructuredCandidate>.Failure(
                        Error.Serialization(
                            "hive.structured-extraction.candidate-confidence-invalid",
                            "The provider returned an invalid confidence value."));
                }

                confidence = parsedConfidence;
            }

            var fields = new List<StructuredCandidateField>(
                targetSchema.ParentFields.Count);

            foreach (var field in targetSchema.ParentFields)
            {
                if (!json.TryGetProperty(field.Id.Value, out var value))
                {
                    fields.Add(
                        MissingField(field));
                    continue;
                }

                fields.Add(ParseCandidateField(field, value));
            }

            var children = new List<StructuredChildCandidate>();
            foreach (var pair in targetSchema.ChildFieldsByCollection)
            {
                if (!json.TryGetProperty(pair.Key, out var collection))
                    continue;

                if (collection.ValueKind != JsonValueKind.Array ||
                    collection.GetArrayLength() > 1024)
                {
                    return Result<StructuredCandidate>.Failure(
                        Error.Serialization(
                            "hive.structured-extraction.candidate-child-invalid",
                            $"Child collection '{pair.Key}' is malformed or exceeds the bounded child-item limit."));
                }

                foreach (var child in collection.EnumerateArray())
                {
                    if (child.ValueKind != JsonValueKind.Object)
                    {
                        return Result<StructuredCandidate>.Failure(
                            Error.Serialization(
                                "hive.structured-extraction.candidate-child-invalid",
                                $"Child collection '{pair.Key}' contains a malformed item."));
                    }

                    var childFields = pair.Value
                        .Select(
                            field =>
                                child.TryGetProperty(
                                    field.Id.Value,
                                    out var value)
                                    ? ParseCandidateField(field, value)
                                    : MissingField(field))
                        .ToArray();

                    children.Add(
                        new StructuredChildCandidate(
                            pair.Key,
                            childFields));
                }
            }

            return Result<StructuredCandidate>.Success(
                new StructuredCandidate(
                    StructuredCandidateId.New(),
                    provenance,
                    fields,
                    children,
                    confidence));
        }
        catch (ArgumentException)
        {
            return Result<StructuredCandidate>.Failure(
                Error.Serialization(
                    "hive.structured-extraction.candidate-output-invalid",
                    "The provider returned invalid structured candidate data."));
        }
    }

    private static StructuredCandidateField ParseCandidateField(
        StructuredTargetField field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                null,
                field.Required
                    ? StructuredValidationState.Missing
                    : StructuredValidationState.Valid,
                field.Required
                    ? "The provider did not supply a value for this required field."
                    : null);
        }

        return field.ValueType switch
        {
            StructuredValueType.String =>
                value.ValueKind == JsonValueKind.String
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        value.GetString(),
                        StructuredValidationState.Valid)
                    : InvalidType(field),

            StructuredValueType.Int64 =>
                ParseInteger(field, value),

            StructuredValueType.Decimal =>
                ParseDecimal(field, value),

            StructuredValueType.Boolean =>
                value.ValueKind == JsonValueKind.True ||
                value.ValueKind == JsonValueKind.False
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        value.GetBoolean() ? "true" : "false",
                        StructuredValidationState.Valid)
                    : InvalidType(field),

            StructuredValueType.DateTime =>
                ParseDateTime(field, value),

            StructuredValueType.Guid =>
                ParseGuid(field, value),

            _ => InvalidType(field)
        };
    }

    private static StructuredCandidateField ParseInteger(
        StructuredTargetField field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                number.ToString(CultureInfo.InvariantCulture),
                StructuredValidationState.Valid);
        }

        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out number))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                number.ToString(CultureInfo.InvariantCulture),
                StructuredValidationState.Valid);
        }

        return value.ValueKind == JsonValueKind.String
            ? new StructuredCandidateField(
                field.Id,
                field.ValueType,
                value.GetString(),
                StructuredValidationState.InvalidFormat,
                "The value is not a valid 64-bit integer.")
            : InvalidType(field);
    }

    private static StructuredCandidateField ParseDecimal(
        StructuredTargetField field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                number.ToString(CultureInfo.InvariantCulture),
                StructuredValidationState.Valid);
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out number))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                number.ToString(CultureInfo.InvariantCulture),
                StructuredValidationState.Valid);
        }

        return value.ValueKind == JsonValueKind.String
            ? new StructuredCandidateField(
                field.Id,
                field.ValueType,
                value.GetString(),
                StructuredValidationState.InvalidFormat,
                "The value is not a valid decimal.")
            : InvalidType(field);
    }

    private static StructuredCandidateField ParseDateTime(
        StructuredTargetField field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                StructuredValidationState.Valid);
        }

        return value.ValueKind == JsonValueKind.String
            ? new StructuredCandidateField(
                field.Id,
                field.ValueType,
                value.GetString(),
                StructuredValidationState.InvalidFormat,
                "The value is not a valid ISO date/time.")
            : InvalidType(field);
    }

    private static StructuredCandidateField ParseGuid(
        StructuredTargetField field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(
                value.GetString(),
                out var parsed))
        {
            return new StructuredCandidateField(
                field.Id,
                field.ValueType,
                parsed.ToString("D"),
                StructuredValidationState.Valid);
        }

        return value.ValueKind == JsonValueKind.String
            ? new StructuredCandidateField(
                field.Id,
                field.ValueType,
                value.GetString(),
                StructuredValidationState.InvalidFormat,
                "The value is not a valid GUID.")
            : InvalidType(field);
    }

    private static StructuredCandidateField InvalidType(
        StructuredTargetField field) =>
        new(
            field.Id,
            field.ValueType,
            null,
            StructuredValidationState.InvalidType,
            $"The provider returned a value with the wrong JSON type for '{field.ValueType}'.");

    private static StructuredCandidateField MissingField(
        StructuredTargetField field) =>
        new(
            field.Id,
            field.ValueType,
            null,
            field.Required
                ? StructuredValidationState.Missing
                : StructuredValidationState.Valid,
            field.Required
                ? "The required semantic field was not returned."
                : null);

    private static bool HasCapability(
        ExecutionTarget target,
        string key) =>
        target.Capabilities.Any(
            capability =>
                string.Equals(
                    capability.Capability.Value,
                    key,
                    StringComparison.OrdinalIgnoreCase) &&
                capability.State == CapabilityState.Supported);

    private static bool CanUseStructuredOutput(
        ExecutionTarget target) =>
        target.Capabilities.All(
            capability =>
                !string.Equals(
                    capability.Capability.Value,
                    StructuredOutputCapability,
                    StringComparison.OrdinalIgnoreCase) ||
                capability.State != CapabilityState.Unsupported);

    private static string BuildMappingPrompt(
        SpreadsheetMappingContext context,
        StructuredTargetSchema targetSchema)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Source worksheet:");
        builder.AppendLine(context.WorksheetName);
        builder.AppendLine("Source columns:");

        foreach (var column in context.SourceColumns)
            builder.AppendLine($"- {column}");

        builder.AppendLine("Sample rows:");
        foreach (var row in context.SampleRows)
        {
            builder.Append("{ ");
            builder.Append(
                string.Join(
                    ", ",
                    row.Select(
                        pair => $"\"{pair.Key}\": \"{Truncate(pair.Value, 512)}\"")));
            builder.AppendLine(" }");
        }

        builder.AppendLine("Target semantic fields:");

        foreach (var field in targetSchema.Fields)
        {
            builder.Append("- ");
            builder.Append(field.Id);
            builder.Append(": ");
            builder.Append(field.DisplayName);
            builder.Append(" (");
            builder.Append(field.ValueType);
            if (field.Required)
                builder.Append(", required");
            if (field.Placement == StructuredFieldPlacement.Child)
                builder.Append($", child collection {field.ChildCollectionKey}");
            builder.AppendLine(")");
        }

        builder.AppendLine(
            "Map only source columns that semantically correspond to a target field. Do not invent target field identities.");

        return builder.ToString();
    }

    private static string BuildImagePrompt(
        StructuredTargetSchema targetSchema)
    {
        var fields = string.Join(
            Environment.NewLine,
            targetSchema.Fields.Select(
                field =>
                    $"- {field.Id}: {field.DisplayName} ({field.ValueType}, {(field.Required ? "required" : "optional")})"));

        return
            "Extract the requested target fields from this image. " +
            "Use null when a value is not visible or cannot be established. " +
            "Do not infer business facts beyond the visible evidence. " +
            Environment.NewLine +
            "Target semantic fields:" +
            Environment.NewLine +
            fields;
    }

    private static JsonElement BuildMappingJsonSchema(
        SpreadsheetMappingContext context,
        StructuredTargetSchema targetSchema)
    {
        return JsonSerializer.SerializeToElement(
            new
            {
                type = "object",
                properties = new
                {
                    mappings = new
                    {
                        type = "array",
                        maxItems = context.SourceColumns.Count,
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                sourceColumn = new { type = "string" },
                                targetFieldId = new { type = "string" }
                            },
                            required = new[] { "sourceColumn", "targetFieldId" },
                            additionalProperties = false
                        }
                    }
                },
                required = new[] { "mappings" },
                additionalProperties = false
            });
    }

    private static JsonElement BuildCandidateJsonSchema(
        StructuredTargetSchema targetSchema)
    {
        var parentProperties = new Dictionary<string, object>(
            StringComparer.Ordinal)
        {
            ["confidence"] = new
            {
                anyOf = new object[]
                {
                    new { type = "number", minimum = 0, maximum = 1 },
                    new { type = "null" }
                }
            }
        };

        foreach (var field in targetSchema.ParentFields)
            parentProperties[field.Id.Value] = JsonSchemaFor(field.ValueType);

        foreach (var pair in targetSchema.ChildFieldsByCollection)
        {
            var childProperties = pair.Value.ToDictionary(
                field => field.Id.Value,
                field => JsonSchemaFor(field.ValueType),
                StringComparer.Ordinal);

            parentProperties[pair.Key] = new
            {
                type = "array",
                maxItems = 1024,
                items = new
                {
                    type = "object",
                    properties = childProperties,
                    additionalProperties = false
                }
            };
        }

        return JsonSerializer.SerializeToElement(
            new
            {
                type = "object",
                properties = parentProperties,
                additionalProperties = false
            });
    }

    private static object JsonSchemaFor(
        StructuredValueType valueType)
    {
        var primitive = valueType switch
        {
            StructuredValueType.String => new { type = "string" },
            StructuredValueType.Int64 => new { type = "integer" },
            StructuredValueType.Decimal => new { type = "number" },
            StructuredValueType.Boolean => new { type = "boolean" },
            StructuredValueType.DateTime => new { type = "string" },
            StructuredValueType.Guid => new { type = "string" },
            _ => new { type = "string" }
        };

        return new
        {
            anyOf = new[]
            {
                primitive,
                new { type = "null" }
            }
        };
    }

    private static async Task<Result<JsonElement>> CompleteStructuredJsonAsync(
        ExecutionTarget target,
        SecretMaterial? credential,
        string schemaName,
        JsonElement schema,
        IReadOnlyList<OpenAICompatibleMessage> messages,
        CancellationToken cancellationToken)
    {
        var model = target.Model ?? target.Deployment;

        if (string.IsNullOrWhiteSpace(model))
        {
            return Result<JsonElement>.Failure(
                Error.Validation(
                    "hive.structured-extraction.model-required",
                    "The execution target must define a model or deployment before structured extraction."));
        }

        try
        {
            var adapter = new OpenAICompatibleProviderAdapter(
                new HttpClient(),
                new OpenAICompatibleProviderOptions(
                    target.Endpoint,
                    credential));

            var result = await adapter
                .CompleteChatAsync(
                    new OpenAICompatibleChatRequest(
                        model,
                        messages,
                        new OpenAICompatibleStructuredOutput(
                            schemaName,
                            schema)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure)
                return Result<JsonElement>.Failure(result.Error!);

            if (result.Value!.StructuredContent is not { } content)
            {
                return Result<JsonElement>.Failure(
                    Error.Serialization(
                        "hive.structured-extraction.structured-content-missing",
                        "The provider did not return structured JSON content."));
            }

            return Result<JsonElement>.Success(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return Result<JsonElement>.Failure(
                Error.Validation(
                    "hive.structured-extraction.request-invalid",
                    "The structured extraction request was invalid."));
        }
    }

    private static string Truncate(
        string value,
        int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static string Sha256(string value) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();
}
