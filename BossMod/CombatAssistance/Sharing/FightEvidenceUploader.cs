using BossMod.ReplayAnalysis.Evidence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BossMod.CombatAssistance.Sharing;

/// <summary>Opt-in file/network worker. Observe only publishes booleans; it performs no I/O,
/// queues no work and touches no game objects. The worker cancels its own work during combat.</summary>
internal sealed class FightEvidenceUploader : IAsyncDisposable
{
    private const int ReaderVersion = 2;
    private const string UploadUrl = "https://bmr-fights.xel-serv.com/v1/batches";
    private readonly string configDirectory;
    private readonly string pluginConfigRoot;
    private readonly CancellationTokenSource shutdown = new();
    private readonly HttpClient client;
    private readonly bool singleStream;
    private PreparedRecording? prepared;
    private string? preparedKey;
    private volatile bool identifyingTransfer;
    private readonly RecordingModuleCatalog catalog;
    private readonly ReplayEvidenceDecode nativeDecode;
    private RecordingSharingSettings recordingSettings = new(true, null);
    private string? moduleParserMvid;
    private readonly TimeSpan startupDelay;
    private readonly TimeSpan cycleDelay;
    private readonly TimeSpan batchDelay;
    private readonly DropoffRequestBudget requestBudget = new();
    private readonly Stopwatch uploadClock = Stopwatch.StartNew();
    private readonly Task worker;
    private readonly Task monitor;
    private volatile bool enabled;
    private volatile bool busy = true;
    private string status = "Waiting to share.";
    private volatile string statusText = "Recordings: scanning…\nWaiting to share.";
    private volatile string pausedStatusText = "Recordings: scanning…\nPaused for combat/loading.";
    private FightSharingBacklog? backlog;
    private readonly FightSharingBacklogIndex backlogIndex = new();
    private volatile string backlogSummary = "Recordings: scanning…";
    private DateTime nextBacklogScan;
    private DateTime uploadDeferredUntil;
    private int blockedRecordings;
    private DateTime nextRecordingRetry = DateTime.MaxValue;
    private CancellationTokenSource? cycle;
    private Dictionary<string, Receipt> receipts = new();
    private IEnumerator<FileInfo>? scan;
    private string? scanRoot;
    // A confirmed older prefix uses 1024-event boundaries. Without a confirmed
    // prefix, probe bulk transfer and negotiate older remote layouts if needed.
    private sealed record Receipt(int LastPart, bool Complete, int Failures, DateTime RetryAfter,
        int BatchEvents = 0, bool? Anonymize = null, int DropoffVersion = 0,
        int ReaderVersion = 0, string? ParserMvid = null, string? FailureCode = null, bool Blocked = false, int ProjectionSchema = 1, bool Validated = false, int ModulePolicyVersion = 0, bool SkippedModules = false,
        string? DeliveryId = null, string? DeliveryState = null, DateTime StatusAfter = default);
    private sealed class UploadDeferredException(TimeSpan delay) : Exception
    {
        public TimeSpan Delay { get; } = delay;
    }

    public FightEvidenceUploader(string configDirectory, RecordingModuleCatalog catalog, HttpMessageHandler? handler = null,
        TimeSpan? startupDelay = null,
        TimeSpan? cycleDelay = null, TimeSpan? batchDelay = null, bool singleStream = true,
        ReplayEvidenceDecode? nativeDecode = null)
    {
        this.singleStream = singleStream;
        this.catalog = catalog;
        this.nativeDecode = nativeDecode ?? DecodeNative;
        this.client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = singleStream ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(20) };
        this.startupDelay = startupDelay ?? TimeSpan.FromSeconds(2);
        this.cycleDelay = cycleDelay ?? TimeSpan.FromSeconds(5);
        this.batchDelay = batchDelay ?? TimeSpan.Zero;
        this.configDirectory = configDirectory;
        this.pluginConfigRoot = Directory.GetParent(configDirectory)?.FullName ?? configDirectory;
        this.worker = Task.Run(this.RunAsync);
        this.monitor = Task.Run(this.MonitorAsync);
    }

    private static Task DecodeNative(string path, Func<Dictionary<string, object?>, CancellationToken, Task> emit,
        CancellationToken cancel, int batchEvents, bool? anonymize, int schema)
        => new ReplayEvidenceDecoder().DecodeAsync(path, (batch, token) =>
        {
            batch.Remove("parserRevision"); // Removed IPC identity is not an archive field.
            return emit(batch, token);
        }, cancel, batchEvents, anonymize, schema);

    public string Status => !this.enabled ? "Fight sharing is off."
        : this.busy ? this.pausedStatusText : this.statusText;

    private void SetStatus(string value)
    {
        this.status = value;
        this.statusText = $"{this.backlogSummary}\n{value}";
        this.pausedStatusText = $"{this.backlogSummary}\nPaused for combat/loading.";
    }

    public void Observe(bool share, bool occupied, RecordingSharingSettings settings)
    {
        Volatile.Write(ref this.recordingSettings, settings);
        this.enabled = share;
        this.busy = occupied;
    }

    private async Task MonitorAsync()
    {
        try
        {
            while (!this.shutdown.IsCancellationRequested)
            {
                if (!this.enabled || this.busy || (this.identifyingTransfer && this.SharingSettings().Anonymize))
                {
                    CancellationTokenSource? current = Volatile.Read(ref this.cycle);
                    if (current != null)
                    {
                        try
                        {
                            await current.CancelAsync();
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
                await Task.Delay(250, this.shutdown.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunAsync()
    {
        try
        {
            await Task.Delay(this.startupDelay, this.shutdown.Token);
            this.LoadReceipts();
            this.CleanupStaging();
            while (!this.shutdown.IsCancellationRequested)
            {
                if (!this.enabled && this.prepared != null)
                {
                    this.prepared.Dispose();
                    this.prepared = null;
                    this.preparedKey = null;
                }
                if (this.enabled && !this.busy && DateTime.UtcNow >= this.uploadDeferredUntil)
                {
                    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(this.shutdown.Token);
                    Volatile.Write(ref this.cycle, cancel);
                    try
                    {
                        await this.UploadClosedRecordingsAsync(cancel.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception)
                    {
                        // No source filenames, parser messages or HTTP bodies in logs/status.
                        this.SetStatus("Waiting to retry.");
                    }
                    finally
                    {
                        Volatile.Write(ref this.cycle, null);
                        if (!this.enabled)
                        {
                            this.prepared?.Dispose();
                            this.prepared = null;
                            this.preparedKey = null;
                        }
                    }
                }
                if (this.enabled && !this.busy && DateTime.UtcNow < this.uploadDeferredUntil)
                {
                    this.SetStatus("Server busy · retrying shortly.");
                }
                await Task.Delay(this.cycleDelay, this.shutdown.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task UploadClosedRecordingsAsync(CancellationToken token)
    {
        await this.RefreshDeliveryStatusesAsync(token);
        var modules = this.catalog.Modules;
        var parserMvid = this.catalog.ParserRevision;
        Task Decode(string source, Func<Dictionary<string, object?>, CancellationToken, Task> emit,
            CancellationToken cancellation, int events, bool? anonymous, int schema)
            => this.nativeDecode(source, emit, cancellation, events, anonymous, schema);
        this.moduleParserMvid = parserMvid;
        string root = this.RecordingDirectory();
        if (!Directory.Exists(root))
        {
            this.backlogIndex.Invalidate();
            this.backlog = new(0, 0, true);
            this.backlogSummary = this.backlog.Summary;
            this.SetStatus("No recordings yet.");
            return;
        }
        this.RefreshBacklog(root, token, true);
        // Keep an enumeration cursor across idle cycles so completed early files cannot
        // hide later recordings. Directory changes are picked up on the next full pass.
        if (this.scan == null || this.scanRoot != root)
        {
            this.scan?.Dispose();
            this.scan = new DirectoryInfo(root).EnumerateFiles("*.log").GetEnumerator();
            this.scanRoot = root;
        }
        var candidates = new List<FileInfo>();
        for (int n = 0; n < 2048; ++n)
        {
            token.ThrowIfCancellationRequested();
            if (!this.scan.MoveNext())
            {
                this.scan.Dispose();
                this.scan = null;
                break;
            }
            FileInfo candidate = this.scan.Current;
            if (FightSharingBacklog.IsEligible(candidate, DateTime.UtcNow))
            {
                candidates.Add(candidate);
            }
        }
        FileInfo[] files = candidates.OrderBy(f => f.LastWriteTimeUtc).ToArray();
        foreach (FileInfo file in files)
        {
            token.ThrowIfCancellationRequested();
            if (!this.enabled || this.busy)
            {
                throw new OperationCanceledException(token);
            }
            this.backlogIndex.Observe(file);
            // Version the journal namespace because public uploads have different server
            // recording IDs from the former credential-scoped uploads.
            string fingerprint = FightSharingBacklog.Fingerprint(file);
            Receipt receipt = this.receipts.GetValueOrDefault(fingerprint) ??
                new Receipt(-1, false, 0, DateTime.MinValue, ReplayEvidenceDecoder.MaximumBatchEvents, this.SharingSettings().Anonymize, 1, ReaderVersion, parserMvid, ProjectionSchema: 3);
            if (!receipt.Complete && (receipt.ReaderVersion != ReaderVersion || receipt.ParserMvid != parserMvid))
            {
                // The corrected projection has a separate immutable identity.
                // Keep completed receipts and archived older parts intact, and
                // retry pending originals promptly rather than keeping old backoff.
                receipt = receipt with
                {
                    ReaderVersion = ReaderVersion,
                    ParserMvid = parserMvid,
                    Failures = 0,
                    RetryAfter = DateTime.MinValue,
                    FailureCode = null,
                    Blocked = false,
                    ProjectionSchema = 3,
                    Validated = false,
                    SkippedModules = false,
                    ModulePolicyVersion = 0,
                    LastPart = -1,
                    BatchEvents = ReplayEvidenceDecoder.MaximumBatchEvents,
                    Anonymize = receipt.Anonymize ?? true
                };
            }
            if (!receipt.Complete && receipt.Anonymize.HasValue && receipt.DropoffVersion == 0)
            {
                // Early envelope builds could contact a server still using the
                // legacy contract. Retry that one transport transition promptly;
                // preserve confirmed parts, privacy choice and all source data.
                receipt = receipt with
                {
                    Failures = 0,
                    RetryAfter = DateTime.MinValue,
                    DropoffVersion = 1
                };
            }
            if (receipt.BatchEvents == 0)
            {
                receipt = receipt with
                {
                    BatchEvents = receipt.LastPart >= 0
                    ? ReplayEvidenceDecoder.LegacyBatchEvents : ReplayEvidenceDecoder.MaximumBatchEvents
                };
            }
            if (!receipt.Complete && receipt.Anonymize == false && this.SharingSettings().Anonymize)
            {
                // A stricter current choice starts an anonymous namespace. Never
                // send another identifying part merely to finish an older upload.
                receipt = new Receipt(-1, false, 0, DateTime.MinValue,
                    ReplayEvidenceDecoder.MaximumBatchEvents, true, 1, ReaderVersion, parserMvid, ProjectionSchema: 3);
            }
            if (!receipt.Complete && receipt.ModulePolicyVersion != RecordingModuleEligibility.PolicyVersion)
            {
                // Old partial/validated receipts cannot authorize another send under
                // the new gate. Preserve confirmed server parts and completed history.
                receipt = receipt with
                {
                    Validated = false,
                    SkippedModules = false,
                    ModulePolicyVersion = RecordingModuleEligibility.PolicyVersion,
                    RetryAfter = DateTime.MinValue
                };
            }
            if (receipt.DeliveryId != null || receipt.Complete || receipt.SkippedModules || receipt.Blocked || receipt.RetryAfter > DateTime.UtcNow)
            {
                continue;
            }
            try
            {
                this.SetStatus(receipt.LastPart < 0 ? "Preparing recording…" : "Resuming upload…");
                if (this.singleStream)
                {
                    bool anonymize = receipt.Anonymize ?? true;
                    string key = fingerprint + ":" + anonymize + ":" + parserMvid + ":" + receipt.BatchEvents;
                    if (this.preparedKey != key)
                    {
                        this.prepared?.Dispose();
                        this.prepared = null;
                        this.preparedKey = null;
                        this.prepared = await PreparedRecording.CreateAsync(file.FullName, Decode, this.configDirectory,
                            anonymize, receipt.BatchEvents,
                            () => this.enabled && !this.busy && (anonymize || !this.SharingSettings().Anonymize), token,
                            new RecordingModuleEligibility(modules));
                        this.preparedKey = key;
                    }
                    PreparedRecording transfer = this.prepared ?? throw new InvalidOperationException("transfer_unavailable");
                    await this.SendRecordingAsync(transfer, anonymize, token);
                    receipt = receipt with
                    {
                        Complete = false,
                        DeliveryId = transfer.Digest,
                        DeliveryState = "queued",
                        StatusAfter = DateTime.MinValue,
                        Failures = 0,
                        RetryAfter = DateTime.MinValue,
                        FailureCode = null,
                        Blocked = false,
                        Validated = true,
                        ModulePolicyVersion = RecordingModuleEligibility.PolicyVersion
                    };
                    this.receipts[fingerprint] = receipt;
                    this.SaveReceipts();
                    transfer.Dispose();
                    this.prepared = null;
                    this.preparedKey = null;
                    this.RefreshBacklog(root, token, true);
                    this.SetStatus("Accepted for processing.");
                    continue;
                }
                if (!receipt.Validated)
                {
                    // Verify EOF locally before sending a new projection. Broken
                    // compression cannot produce another permanently partial upload.
                    var eligibility = new RecordingModuleEligibility(modules);
                    await Decode(file.FullName,
                        (batch, _) =>
                        {
                            eligibility.Observe(batch);
                            return Task.CompletedTask;
                        }, token, receipt.BatchEvents, receipt.Anonymize, receipt.ProjectionSchema);
                    eligibility.RequireEligible();
                    receipt = receipt with
                    {
                        Validated = true,
                        ModulePolicyVersion = RecordingModuleEligibility.PolicyVersion
                    };
                    this.receipts[fingerprint] = receipt;
                    this.SaveReceipts();
                }
                await Decode(file.FullName, async (batch, ct) =>
                {
                    if (!this.enabled || this.busy)
                    {
                        throw new OperationCanceledException(ct);
                    }
                    int part = (int)batch["part"]!;
                    if (part <= receipt.LastPart)
                    {
                        return;
                    }
                    if (receipt.Anonymize == false && this.SharingSettings().Anonymize)
                    {
                        throw new OperationCanceledException(ct);
                    }
                    object upload = batch;
                    if (receipt.Anonymize is bool anonymize)
                    {
                        object identities = batch["shared_identities"]!;
                        batch.Remove("shared_identities");
                        upload = new
                        {
                            sharing_version = 1,
                            anonymize,
                            batch,
                            identities
                        };
                    }
                    // Bound both payload and network rate independently of the game's update loop.
                    byte[] json = JsonSerializer.SerializeToUtf8Bytes(upload);
                    if (json.Length > 6 * 1024 * 1024)
                    {
                        throw new InvalidDataException("batch_limit");
                    }
                    using var compressed = new MemoryStream();
                    using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, true))
                    {
                        await gzip.WriteAsync(json, ct);
                    }
                    if (compressed.Length > 2 * 1024 * 1024)
                    {
                        throw new InvalidDataException("compressed_batch_limit");
                    }
                    using var request = new HttpRequestMessage(HttpMethod.Post, UploadUrl);
                    request.Headers.TryAddWithoutValidation("Prefer", "respond-async");
                    request.Content = new ByteArrayContent(compressed.ToArray());
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    request.Content.Headers.ContentEncoding.Add("gzip");
                    await this.WaitForUploadSlotAsync(ct);
                    // Recheck after admission waits as well as before preparing
                    // the body; privacy can tighten while a request is waiting.
                    if (receipt.Anonymize == false && this.SharingSettings().Anonymize)
                    {
                        throw new OperationCanceledException(ct);
                    }
                    this.SetStatus("Uploading…");
                    using HttpResponseMessage response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if ((int)response.StatusCode is 429 or 503)
                    {
                        var retry = response.Headers.RetryAfter;
                        TimeSpan delay = retry?.Delta ?? (retry?.Date is { } date
                            ? date - DateTimeOffset.UtcNow : TimeSpan.FromMinutes(1));
                        throw new UploadDeferredException(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 15, 3600)));
                    }
                    // Never retain/print response bodies. A successful response is meaningful only with its typed acknowledgement.
                    await using Stream body = await response.Content.ReadAsStreamAsync(ct);
                    byte[] reply = new byte[4097];
                    int read = 0;
                    while (read < reply.Length)
                    {
                        int next = await body.ReadAsync(reply.AsMemory(read), ct);
                        if (next == 0)
                        {
                            break;
                        }
                        read += next;
                    }
                    if (read > 4096)
                    {
                        throw new InvalidDataException("ack_limit");
                    }
                    using JsonDocument ack = JsonDocument.Parse(reply.AsMemory(0, read));
                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode == 400 && receipt.Anonymize.HasValue
                            && ack.RootElement.TryGetProperty("error", out JsonElement schemaError)
                            && schemaError.GetString() == "invalid_upload_schema")
                        {
                            // A coordinated service rollout is global, not a
                            // reason to fail every file and grow their backoffs.
                            throw new UploadDeferredException(TimeSpan.FromSeconds(15));
                        }
                        if (ack.RootElement.TryGetProperty("error", out JsonElement error)
                            && error.GetString() == "part_zero_required")
                        {
                            // Retention may remove a partial upload. Restart only on the
                            // server's explicit missing-start response, using the same digest.
                            receipt = receipt with
                            {
                                LastPart = -1,
                                Complete = false,
                                Failures = 0,
                                RetryAfter = DateTime.MinValue
                            };
                            this.receipts[fingerprint] = receipt;
                            this.SaveReceipts();
                            throw new UploadDeferredException(TimeSpan.Zero);
                        }
                        if (part == 0 && receipt.LastPart < 0 && receipt.BatchEvents > ReplayEvidenceDecoder.LegacyBatchEvents &&
                            ack.RootElement.TryGetProperty("error", out JsonElement conflict) &&
                            conflict.GetString() == "immutable_batch_conflict")
                        {
                            // A lost local journal or renamed file may already
                            // exist remotely with legacy boundaries. Retry
                            // supported older layouts in bounded order; never
                            // replace remote evidence or change a confirmed prefix.
                            receipt = receipt with
                            {
                                BatchEvents = receipt.BatchEvents == ReplayEvidenceDecoder.MaximumBatchEvents
                                ? ReplayEvidenceDecoder.IntermediateBatchEvents : ReplayEvidenceDecoder.LegacyBatchEvents
                            };
                            this.receipts[fingerprint] = receipt;
                            this.SaveReceipts();
                            throw new UploadDeferredException(TimeSpan.Zero);
                        }
                        throw new HttpRequestException("upload_refused");
                    }
                    if (!ack.RootElement.GetProperty("ok").GetBoolean()
                        || ack.RootElement.GetProperty("part").GetInt32() != part
                        || ack.RootElement.GetProperty("complete").GetBoolean() != (bool)batch["complete"]!
                        || !ack.RootElement.TryGetProperty("recording_id", out JsonElement identifier)
                        || identifier.GetString() is not string recordingId
                        || recordingId.Length != 64 || !recordingId.All(char.IsAsciiHexDigit))
                    {
                        throw new InvalidDataException("invalid_ack");
                    }
                    if ((int)response.StatusCode == 202
                        && (!ack.RootElement.TryGetProperty("state", out JsonElement state)
                            || state.GetString() is not ("queued" or "processing" or "indexed" or "failed")))
                    {
                        throw new InvalidDataException("invalid_durable_ack");
                    }
                    receipt = receipt with
                    {
                        LastPart = part,
                        Complete = (bool)batch["complete"]!,
                        Failures = 0,
                        RetryAfter = DateTime.MinValue,
                        FailureCode = null,
                        Blocked = false
                    };
                    this.receipts[fingerprint] = receipt;
                    this.SaveReceipts();
                    this.RefreshBacklog(root, ct, receipt.Complete);
                    this.SetStatus(receipt.Complete ? "Recording shared." : "Uploading…");
                    if (this.batchDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(this.batchDelay, ct);
                    }
                }, token, receipt.BatchEvents, receipt.Anonymize, receipt.ProjectionSchema);
            }
            catch (OperationCanceledException) { throw; }
            catch (RecordingModuleSkippedException)
            {
                this.receipts[fingerprint] = receipt with
                {
                    SkippedModules = true,
                    ModulePolicyVersion = RecordingModuleEligibility.PolicyVersion,
                    Failures = 0,
                    FailureCode = null,
                    Blocked = false,
                    RetryAfter = DateTime.MinValue
                };
                this.SaveReceipts();
                this.RefreshBacklog(root, token, true);
                this.SetStatus("Recording skipped by module status.");
            }
            catch (UploadDeferredException deferred)
            {
                this.uploadDeferredUntil = DateTime.UtcNow + deferred.Delay;
                this.receipts[fingerprint] = receipt with
                {
                    RetryAfter = this.uploadDeferredUntil
                };
                this.SaveReceipts();
                this.SetStatus("Server busy · retrying shortly.");
                return;
            }
            catch (Exception error)
            {
                int failures = Math.Min(10, receipt.Failures + 1);
                FightUploadFailure failure = FightUploadFailurePolicy.Classify(error, failures);
                this.receipts[fingerprint] = receipt with
                {
                    Failures = failures,
                    FailureCode = failure.Code,
                    Blocked = failure.Blocked,
                    RetryAfter = failure.Blocked ? DateTime.MinValue : DateTime.UtcNow + failure.RetryDelay
                };
                this.SaveReceipts();
                this.RefreshBacklog(root, token, true);
                // Fixed, non-identifying reason codes remain in the bounded
                // receipt journal. Never log source paths or parser messages.
                this.SetStatus(failure.Blocked ? "A recording could not be read." : "Waiting to retry.");
                // A single source failure cannot hold up the rest of this pass.
            }
        }
        this.RefreshBacklog(root, token, true);
        this.SetStatus(this.receipts.Values.Any(r => r.DeliveryState == "failed")
            ? "Archive processing failed · recording retained."
            : this.receipts.Values.Any(r => r.DeliveryId != null && !r.Complete)
            ? "Accepted for processing · awaiting archive confirmation."
            : this.backlog is { Shared: > 0 } && this.receipts.Values.Any(r => r.Complete && r.DeliveryId == null)
            ? "Earlier uploads accepted · indexing status unavailable."
            : this.backlog is { ScanComplete: true } progress && progress.Shared == progress.Total
            ? "Up to date."
            : FightUploadFailurePolicy.WaitingStatus(this.backlog is { } pending ? pending.Total - pending.Shared : 0,
                this.blockedRecordings, this.nextRecordingRetry, DateTime.UtcNow));
    }

    private async Task RefreshDeliveryStatusesAsync(CancellationToken token)
    {
        // Custody receipts outlive the local file. Poll boundedly on the idle
        // worker, including failed receipts so operator recovery becomes visible.
        foreach (var item in this.receipts.Where(item => item.Value.DeliveryId != null && !item.Value.Complete
            && item.Value.StatusAfter <= DateTime.UtcNow).Take(16).ToArray())
        {
            await this.WaitForUploadSlotAsync(token);
            Receipt receipt = item.Value;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://bmr-fights.xel-serv.com/v2/recordings");
                request.Headers.TryAddWithoutValidation("X-Delivery-SHA256", receipt.DeliveryId);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using var response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                byte[] reply = new byte[4097];
                int read = 0;
                while (read < reply.Length)
                {
                    int count = await body.ReadAsync(reply.AsMemory(read), timeout.Token);
                    if (count == 0)
                        break;
                    read += count;
                }
                if (read > 4096)
                    throw new InvalidDataException("status_limit");
                using var document = JsonDocument.Parse(reply.AsMemory(0, read));
                var status = document.RootElement;
                string? state = status.GetProperty("state").GetString();
                if (!status.GetProperty("ok").GetBoolean() || status.GetProperty("delivery_id").GetString() != receipt.DeliveryId
                    || state is not ("queued" or "processing" or "indexed" or "failed"))
                    throw new InvalidDataException("invalid_processing_status");
                receipt = receipt with
                {
                    Complete = state == "indexed",
                    DeliveryState = state
                };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or JsonException
                or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {
                // A status outage never revokes custody or starts another upload.
            }
            this.receipts[item.Key] = receipt with
            {
                StatusAfter = DateTime.UtcNow.AddMinutes(1)
            };
            this.SaveReceipts();
        }
    }

    private async Task SendRecordingAsync(PreparedRecording recording, bool anonymize, CancellationToken token)
    {
        await this.WaitForUploadSlotAsync(token);
        if (!anonymize && this.SharingSettings().Anonymize)
            throw new OperationCanceledException(token);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://bmr-fights.xel-serv.com/v2/recordings");
        request.Headers.TryAddWithoutValidation("X-Delivery-SHA256", recording.Digest);
        request.Headers.ExpectContinue = true;
        request.Content = new StreamContent(File.OpenRead(recording.Path));
        request.Content.Headers.ContentLength = recording.Length;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-xbmr-recording+gzip");
        this.SetStatus("Uploading…");
        this.identifyingTransfer = !anonymize;
        try
        {
            using HttpResponseMessage response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is 429 or 503)
            {
                TimeSpan delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15);
                throw new UploadDeferredException(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 15, 3600)));
            }
            response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(token);
            byte[] reply = new byte[4097];
            int read = 0;
            while (read < reply.Length)
            {
                int count = await body.ReadAsync(reply.AsMemory(read), token);
                if (count == 0)
                    break;
                read += count;
            }
            if (read > 4096)
                throw new InvalidDataException("ack_limit");
            using JsonDocument ack = JsonDocument.Parse(reply.AsMemory(0, read));
            if (response.StatusCode != System.Net.HttpStatusCode.Accepted
                || !ack.RootElement.GetProperty("ok").GetBoolean()
                || !ack.RootElement.GetProperty("queued").GetBoolean()
                || ack.RootElement.GetProperty("delivery_id").GetString() != recording.Digest)
                throw new InvalidDataException("invalid_durable_ack");
        }
        finally
        {
            this.identifyingTransfer = false;
        }
    }

    private async Task WaitForUploadSlotAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!this.enabled || this.busy)
                throw new OperationCanceledException(token);
            var delay = this.requestBudget.Reserve(this.uploadClock.Elapsed);
            if (delay == TimeSpan.Zero)
                return;
            this.SetStatus("Waiting for upload slot.");
            await Task.Delay(delay, token);
        }
    }

    private void RefreshBacklog(string root, CancellationToken token, bool force = false)
    {
        DateTime now = DateTime.UtcNow;
        if (!force && now < this.nextBacklogScan)
        {
            return;
        }
        this.nextBacklogScan = now.AddSeconds(30);
        try
        {
            int blocked = 0;
            DateTime nextRetry = DateTime.MaxValue;
            this.backlog = this.backlogIndex.Read(root,
                fingerprint =>
                {
                    if (!this.receipts.TryGetValue(fingerprint, out Receipt? receipt))
                    {
                        return false;
                    }
                    if (!receipt.Complete)
                    {
                        if (receipt.Blocked)
                            ++blocked;
                        else if (receipt.RetryAfter < nextRetry)
                            nextRetry = receipt.RetryAfter;
                    }
                    return receipt.Complete;
                }, now, token, skippedModules: fingerprint =>
                    this.receipts.TryGetValue(fingerprint, out Receipt? receipt)
                    && !receipt.Complete && receipt.SkippedModules
                    && receipt.ModulePolicyVersion == RecordingModuleEligibility.PolicyVersion
                    && receipt.ReaderVersion == ReaderVersion && receipt.ParserMvid == this.moduleParserMvid);
            this.blockedRecordings = blocked;
            this.nextRecordingRetry = nextRetry;
            this.backlogSummary = this.backlog.Summary;
            int indexed = this.receipts.Values.Count(r => r.DeliveryState == "indexed");
            int accepted = this.receipts.Values.Count(r => r.DeliveryId != null && !r.Complete && r.DeliveryState != "failed");
            int failed = this.receipts.Values.Count(r => r.DeliveryState == "failed");
            this.backlogSummary += $"\nArchive: {indexed} indexed · {accepted} accepted for processing · {failed} processing failed";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            this.backlog = null;
            this.backlogSummary = "Recordings: count unavailable";
        }
        this.SetStatus(this.status);
    }

    private RecordingSharingSettings SharingSettings() => Volatile.Read(ref this.recordingSettings);

    private string RecordingDirectory() => this.SharingSettings().ReplayFolder
        ?? Path.Combine(this.configDirectory, "replays");

    private void CleanupStaging()
    {
        try
        {
            if (!Directory.Exists(this.configDirectory))
                return;
            foreach (string path in Directory.EnumerateFiles(this.configDirectory, "fight-transfer-*.tmp"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (Guid.TryParseExact(name["fight-transfer-".Length..], "N", out _))
                    File.Delete(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover owned transfer file must not stop the upload worker.
            this.SetStatus("Transfer cleanup deferred.");
        }
    }

    private void LoadReceipts()
    {
        string path = Path.Combine(this.configDirectory, "fight-upload-receipts.json");
        // Import once by reading the legacy journal only when no integrated one exists.
        // The next native save writes here; the original remains intact for rollback.
        if (!File.Exists(path)) path = Path.Combine(this.pluginConfigRoot, "XelsCombatAI", "fight-upload-receipts.json");
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length < 1024 * 1024)
            {
                this.receipts = JsonSerializer.Deserialize<Dictionary<string, Receipt>>(File.ReadAllBytes(path)) ?? new();
            }
        }
        catch (Exception) { this.SetStatus("Sharing receipts unavailable; safe to retry."); }
    }

    private void SaveReceipts()
    {
        Directory.CreateDirectory(this.configDirectory);
        string path = Path.Combine(this.configDirectory, "fight-upload-receipts.json");
        // Retain completed receipts within a bounded journal. A missing old receipt only causes an idempotent retry.
        if (this.receipts.Count > 4096)
        {
            this.receipts = this.receipts.TakeLast(4096).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        File.WriteAllBytes(path + ".tmp", JsonSerializer.SerializeToUtf8Bytes(this.receipts));
        File.Move(path + ".tmp", path, true);
    }

    public async ValueTask DisposeAsync()
    {
        this.enabled = false;
        await this.shutdown.CancelAsync();
        await Task.WhenAll(this.worker, this.monitor);
        this.scan?.Dispose();
        this.prepared?.Dispose();
        this.client.Dispose();
        this.shutdown.Dispose();
    }
}
