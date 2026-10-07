using System.Globalization;

using Hive.Core;

namespace Hive.Coordination;

public static class StructuredExtractionValueNormalizer
{
    public static StructuredCandidateField Normalize(
        StructuredTargetField field,
        string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        var normalized = value.Trim();

        return field.ValueType switch
        {
            StructuredValueType.String =>
                new StructuredCandidateField(
                    field.Id,
                    field.ValueType,
                    normalized,
                    StructuredValidationState.Valid),

            StructuredValueType.Int64 =>
                long.TryParse(
                    normalized,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var integer)
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        integer.ToString(CultureInfo.InvariantCulture),
                        StructuredValidationState.Valid)
                    : Invalid(
                        field,
                        "The value is not a valid 64-bit integer."),

            StructuredValueType.Decimal =>
                decimal.TryParse(
                    normalized,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var decimalValue)
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        decimalValue.ToString(
                            CultureInfo.InvariantCulture),
                        StructuredValidationState.Valid)
                    : Invalid(
                        field,
                        "The value is not a valid decimal."),

            StructuredValueType.Boolean =>
                bool.TryParse(normalized, out var boolean)
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        boolean ? "true" : "false",
                        StructuredValidationState.Valid)
                    : Invalid(
                        field,
                        "The value is not a valid boolean."),

            StructuredValueType.DateTime =>
                DateTimeOffset.TryParse(
                    normalized,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var dateTime)
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        dateTime.ToUniversalTime().ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                        StructuredValidationState.Valid)
                    : Invalid(
                        field,
                        "The value is not a valid ISO date/time."),

            StructuredValueType.Guid =>
                Guid.TryParse(normalized, out var guid)
                    ? new StructuredCandidateField(
                        field.Id,
                        field.ValueType,
                        guid.ToString("D"),
                        StructuredValidationState.Valid)
                    : Invalid(
                        field,
                        "The value is not a valid GUID."),

            _ => Invalid(
                field,
                "The target semantic field type is unsupported.")
        };
    }

    private static StructuredCandidateField Invalid(
        StructuredTargetField field,
        string message) =>
        new(
            field.Id,
            field.ValueType,
            null,
            StructuredValidationState.InvalidFormat,
            message);
}
