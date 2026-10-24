using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BossMod.CombatAssistance.Sharing;

/// <summary>One private, bounded transfer file. Never contains unfiltered replay bytes.</summary>
internal sealed class PreparedRecording(string path, string digest, long length) : IDisposable
{
    public string Path { get; } = path;
    public string Digest { get; } = digest;
    public long Length { get; } = length;

    public static async Task<PreparedRecording> CreateAsync(string source,
        Func<string, Func<Dictionary<string, object?>, CancellationToken, Task>, CancellationToken, int, bool?, int, Task> decode,
        string directory,
        bool anonymize, int batchEvents, Func<bool> allowed, CancellationToken token, RecordingModuleEligibility? eligibility = null)
    {
        Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, "fight-transfer-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(path, options))
            {
                await using (var gzip = new GZipStream(file, CompressionLevel.Fastest, true))
                {
                    long expanded = 0;
                    await decode(source, async (batch, ct) =>
                    {
                        if (!allowed())
                            throw new OperationCanceledException(ct);
                        eligibility?.Observe(batch);
                        object identities = batch["shared_identities"]!;
                        batch.Remove("shared_identities");
                        byte[] line = JsonSerializer.SerializeToUtf8Bytes(new
                        {
                            sharing_version = 1,
                            anonymize,
                            batch,
                            identities
                        });
                        expanded += line.Length + 1;
                        if (line.Length + 1 > 6 * 1024 * 1024 || expanded > 4L * 1024 * 1024 * 1024)
                            throw new InvalidDataException("batch_limit");
                        await gzip.WriteAsync(line, ct);
                        await gzip.WriteAsync(new byte[] { 10 }, ct);
                        if (file.Length > 512L * 1024 * 1024)
                            throw new InvalidDataException("compressed_batch_limit");
                    }, token, batchEvents, anonymize, 3);
                }
                eligibility?.RequireEligible();
                await file.FlushAsync(token);
                if (file.Length > 512L * 1024 * 1024)
                    throw new InvalidDataException("compressed_batch_limit");
            }
            await using var input = File.OpenRead(path);
            string digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, token));
            return new PreparedRecording(path, digest, input.Length);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            File.Delete(this.Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Startup retries cleanup of this private owned artifact.
        }
    }
}
