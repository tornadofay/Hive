namespace Hive.Core;

/// <summary>
/// The single owner of the public <see cref="Error"/> shape for spreadsheet
/// input-preparation failures.
/// </summary>
/// <remarks>
/// <para>
/// Input preparation raises Hive-authored control-flow exceptions carrying a
/// stable error code and a detailed developer-facing message. That message is
/// diagnostic context, not a user-facing contract: several codes interpolate
/// document-supplied identifiers such as worksheet or header names, so
/// forwarding it into a public <see cref="Error"/> would leak file content into
/// error output.
/// </para>
/// <para>
/// This catalog is the boundary rule, matching the rule already established for
/// <c>Hive.Persistence</c> by <c>HivePersistenceErrorTests</c>: a public error
/// carries a stable Hive-authored message and the stable error code, never raw
/// exception text. A code that is not registered here resolves to a
/// category-appropriate generic message rather than to exception detail, so
/// adding a new failure site cannot silently reintroduce the leak.
/// </para>
/// </remarks>
public static class InputPreparationFailureCatalog
{
    private static readonly Dictionary<string, (ErrorCategory Category, string Message)>
        Known = new(StringComparer.Ordinal)
        {
            ["hive.input.spreadsheet.archive-too-many-entries"] =
                (ErrorCategory.Validation,
                    "The spreadsheet package contains too many archive entries."),

            ["hive.input.spreadsheet.archive-uncompressed-too-large"] =
                (ErrorCategory.Validation,
                    "The spreadsheet package exceeds the supported uncompressed size limit."),

            ["hive.input.spreadsheet.workbook-missing"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet workbook metadata is missing."),

            ["hive.input.spreadsheet.relationships-missing"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet workbook relationships are missing."),

            ["hive.input.spreadsheet.no-worksheets"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet contains no worksheets."),

            ["hive.input.spreadsheet.too-many-worksheets"] =
                (ErrorCategory.Validation,
                    "The spreadsheet exceeds the supported worksheet limit."),

            ["hive.input.spreadsheet.worksheet-missing"] =
                (ErrorCategory.Serialization,
                    "A worksheet package entry could not be found."),

            ["hive.input.spreadsheet.worksheet-invalid"] =
                (ErrorCategory.Serialization,
                    "The worksheet XML is malformed."),

            ["hive.input.spreadsheet.worksheet-name-invalid"] =
                (ErrorCategory.Serialization,
                    "A worksheet name exceeds the supported length."),

            ["hive.input.spreadsheet.worksheet-empty"] =
                (ErrorCategory.Serialization,
                    "A worksheet contains no rows."),

            ["hive.input.spreadsheet.too-many-rows"] =
                (ErrorCategory.Validation,
                    "A worksheet exceeds the supported row limit."),

            ["hive.input.spreadsheet.row-order-invalid"] =
                (ErrorCategory.Serialization,
                    "Worksheet row numbers must increase in document order."),

            ["hive.input.spreadsheet.header-missing"] =
                (ErrorCategory.Serialization,
                    "A worksheet does not contain the required header row."),

            ["hive.input.spreadsheet.header-empty"] =
                (ErrorCategory.Serialization,
                    "A worksheet header row contains no mapped columns."),

            ["hive.input.spreadsheet.header-incomplete"] =
                (ErrorCategory.Serialization,
                    "Worksheet header columns must be contiguous and non-empty."),

            ["hive.input.spreadsheet.header-duplicate"] =
                (ErrorCategory.Serialization,
                    "A worksheet header name is duplicated."),

            ["hive.input.spreadsheet.too-many-columns"] =
                (ErrorCategory.Validation,
                    "A worksheet exceeds the supported column limit."),

            ["hive.input.spreadsheet.row-extra-column"] =
                (ErrorCategory.Validation,
                    "A spreadsheet row contains values beyond the mapped header columns."),

            ["hive.input.spreadsheet.column-out-of-range"] =
                (ErrorCategory.Validation,
                    "A spreadsheet cell column exceeds the supported column limit."),

            ["hive.input.spreadsheet.duplicate-cell"] =
                (ErrorCategory.Validation,
                    "A spreadsheet row contains duplicate cell columns."),

            ["hive.input.spreadsheet.cell-reference-invalid"] =
                (ErrorCategory.Validation,
                    "A spreadsheet cell reference is invalid."),

            ["hive.input.spreadsheet.row-number-invalid"] =
                (ErrorCategory.Serialization,
                    "A spreadsheet row number is invalid."),

            ["hive.input.spreadsheet.shared-string-invalid"] =
                (ErrorCategory.Validation,
                    "A spreadsheet cell references an invalid shared string."),

            ["hive.input.spreadsheet.boolean-invalid"] =
                (ErrorCategory.Validation,
                    "A spreadsheet boolean cell contains an invalid value."),

            ["hive.input.spreadsheet.cell-too-large"] =
                (ErrorCategory.Validation,
                    "A spreadsheet cell exceeds the supported value length."),

            ["hive.input.spreadsheet.too-many-shared-strings"] =
                (ErrorCategory.Validation,
                    "The spreadsheet exceeds the supported shared-string limit."),

            ["hive.input.spreadsheet.shared-string-too-large"] =
                (ErrorCategory.Validation,
                    "A shared string exceeds the supported cell value length."),

            ["hive.input.spreadsheet.shared-strings-too-large"] =
                (ErrorCategory.Validation,
                    "The spreadsheet shared strings exceed the supported size limit."),

            ["hive.input.spreadsheet.relationship-duplicate"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet workbook contains duplicate relationship identities."),

            ["hive.input.spreadsheet.sheet-invalid"] =
                (ErrorCategory.Serialization,
                    "Spreadsheet worksheet metadata is incomplete."),

            ["hive.input.spreadsheet.sheet-duplicate"] =
                (ErrorCategory.Serialization,
                    "A worksheet name is duplicated."),

            ["hive.input.spreadsheet.sheet-relationship-missing"] =
                (ErrorCategory.Serialization,
                    "A worksheet has no valid package relationship."),

            ["hive.input.spreadsheet.relationship-target-invalid"] =
                (ErrorCategory.Serialization,
                    "A spreadsheet relationship target is invalid."),

            ["hive.input.spreadsheet.xml-entry-too-large"] =
                (ErrorCategory.Validation,
                    "A spreadsheet XML entry exceeds the supported size limit."),

            ["hive.input.spreadsheet.too-many-prepared-rows"] =
                (ErrorCategory.Validation,
                    "The spreadsheet exceeds the supported prepared-row limit."),

            ["hive.input.spreadsheet.too-many-prepared-values"] =
                (ErrorCategory.Validation,
                    "The spreadsheet exceeds the supported prepared-value limit."),

            ["hive.input.spreadsheet.too-many-prepared-rows-submission"] =
                (ErrorCategory.Validation,
                    "The submission exceeds the supported prepared-row limit."),

            ["hive.input.spreadsheet.too-many-prepared-values-submission"] =
                (ErrorCategory.Validation,
                    "The submission exceeds the supported prepared-value limit."),

            ["hive.input.spreadsheet.package-invalid"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet package is invalid or cannot be read."),

            ["hive.input.spreadsheet.xml-invalid"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet XML is malformed."),

            ["hive.input.spreadsheet.read-failed"] =
                (ErrorCategory.Serialization,
                    "The spreadsheet could not be read safely.")
        };

    private const string GenericValidationMessage =
        "The spreadsheet input failed a supported input constraint.";

    private const string GenericSerializationMessage =
        "The spreadsheet input could not be interpreted.";

    /// <summary>
    /// Resolves a stable public error for an input-preparation failure code.
    /// </summary>
    /// <param name="code">
    /// The stable Hive-authored failure code. An unregistered code is not an
    /// error; it resolves to a generic message rather than to exception detail.
    /// </param>
    /// <param name="fallbackCategory">
    /// The category to apply when <paramref name="code"/> is not registered.
    /// A registered code always uses its own catalog category.
    /// </param>
    public static Error CreateError(
        string code,
        ErrorCategory fallbackCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (Known.TryGetValue(code, out var entry))
            return new Error(code, entry.Category, entry.Message);

        return new Error(
            code,
            fallbackCategory,
            fallbackCategory == ErrorCategory.Validation
                ? GenericValidationMessage
                : GenericSerializationMessage);
    }
}