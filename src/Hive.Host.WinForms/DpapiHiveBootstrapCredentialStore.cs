using System.Security.Cryptography;
using System.Text;
using Hive.Core;

namespace Hive.Host.WinForms;

public sealed class DpapiHiveBootstrapCredentialStore :
    IHiveBootstrapCredentialStore
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly byte[] AdditionalEntropy =
        Encoding.UTF8.GetBytes("Hive.BootstrapCredential.v1");

    private readonly string _rootPath;

    public DpapiHiveBootstrapCredentialStore(string? rootPath = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Hive",
                "bootstrap-credentials")
            : Path.GetFullPath(rootPath);
    }

    public async Task<Result> SetAsync(
        HiveBootstrapCredentialReference reference,
        SecretMaterial material,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        var validation = ValidateReference(reference);
        if (validation is not null)
            return Result.Failure(validation);

        if (!OperatingSystem.IsWindows())
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-windows-only",
                    "Hive bootstrap credential storage is supported only on Windows."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] plaintext = Encoding.UTF8.GetBytes(material.Reveal());
        byte[]? encrypted = null;

        try
        {
            encrypted = ProtectedData.Protect(
                plaintext,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            Directory.CreateDirectory(_rootPath);

            var path = GetPath(reference);
            var temporaryPath =
                path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await File.WriteAllBytesAsync(
                    temporaryPath,
                    encrypted,
                    cancellationToken).ConfigureAwait(false);

                File.Move(
                    temporaryPath,
                    path,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            return Result.Failure(
                new Error(
                    "hive.host.bootstrap-credential-protection-failed",
                    ErrorCategory.Internal,
                    "The bootstrap credential could not be protected for the current Windows user."));
        }
        catch (Exception)
        {
            return Result.Failure(
                new Error(
                    "hive.host.bootstrap-credential-write-failed",
                    ErrorCategory.External,
                    "The Hive bootstrap credential could not be stored."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);

            if (encrypted is not null)
                CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public async Task<Result<SecretMaterial>> ResolveAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateReference(reference);
        if (validation is not null)
        {
            return Result<SecretMaterial>.Failure(validation);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Result<SecretMaterial>.Failure(
                Error.Unsupported(
                    "hive.host.bootstrap-credential-windows-only",
                    "Hive bootstrap credential storage is supported only on Windows."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var path = GetPath(reference);

        if (!File.Exists(path))
        {
            return Result<SecretMaterial>.Failure(
                new Error(
                    "hive.host.bootstrap-credential-not-found",
                    ErrorCategory.NotFound,
                    "The referenced Hive bootstrap credential was not found."));
        }

        byte[] encrypted;
        byte[]? plaintext = null;

        try
        {
            encrypted = await File.ReadAllBytesAsync(
                path,
                cancellationToken).ConfigureAwait(false);

            plaintext = ProtectedData.Unprotect(
                encrypted,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);

            var value = StrictUtf8.GetString(plaintext);

            return Result<SecretMaterial>.Success(
                SecretMaterial.Create(value));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            return Result<SecretMaterial>.Failure(
                new Error(
                    "hive.host.bootstrap-credential-decrypt-failed",
                    ErrorCategory.Internal,
                    "The referenced Hive bootstrap credential could not be decrypted for the current Windows user."));
        }
        catch (DecoderFallbackException)
        {
            return Result<SecretMaterial>.Failure(
                new Error(
                    "hive.host.bootstrap-credential-invalid-state",
                    ErrorCategory.Internal,
                    "The referenced Hive bootstrap credential contains invalid encrypted state."));
        }
        catch (ArgumentException)
        {
            return Result<SecretMaterial>.Failure(
                new Error(
                    "hive.host.bootstrap-credential-invalid-state",
                    ErrorCategory.Internal,
                    "The referenced Hive bootstrap credential contains invalid secret material."));
        }
        catch (Exception exception)
        {
            return Result<SecretMaterial>.Failure(
                new Error(
                    "hive.host.bootstrap-credential-read-failed",
                    ErrorCategory.External,
                    "The Hive bootstrap credential could not be read."));
        }
        finally
        {
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task<Result> ClearAsync(
        HiveBootstrapCredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateReference(reference);
        if (validation is not null)
            return Task.FromResult(Result.Failure(validation));

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(
                Result.Failure(
                    Error.Unsupported(
                        "hive.host.bootstrap-credential-windows-only",
                        "Hive bootstrap credential storage is supported only on Windows.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var path = GetPath(reference);

            if (!File.Exists(path))
                return Task.FromResult(Result.Success());

            File.Delete(path);
            return Task.FromResult(Result.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(
                Result.Failure(
                    new Error(
                        "hive.host.bootstrap-credential-clear-failed",
                        ErrorCategory.External,
                        "The Hive bootstrap credential could not be removed.")));
        }
    }

    private string GetPath(
        HiveBootstrapCredentialReference reference) =>
        Path.Combine(
            _rootPath,
            reference.Id.Value.ToString("N") + ".bin");

    private static Error? ValidateReference(
        HiveBootstrapCredentialReference reference) =>
        reference.Id.Value == Guid.Empty
            ? Error.Validation(
                "hive.host.bootstrap-credential-reference-invalid",
                "A valid bootstrap credential reference is required.")
            : null;
}
