using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application.Freezing;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Application;

public sealed class ProcessMemoryApplicationService
{
    private readonly ISystemClock _clock;
    private readonly IProcessFreezeCoordinator _freezeCoordinator;
    private readonly IProcessMemoryGateway _gateway;
    private readonly ILogger<ProcessMemoryApplicationService> _logger;
    private readonly IOperationJournal _operationJournal;
    private readonly IAttachmentSessionRegistry _sessionRegistry;

    /// <summary>
    /// The underlying memory gateway. Exposed for components that need direct access to the
    /// narrow reader (the PES 2021 fixture reader is one of them). Consumers must never
    /// call <c>WriteAsync</c> through this handle.
    /// </summary>
    public IProcessMemoryGateway Gateway => _gateway;

    public ProcessMemoryApplicationService(IProcessMemoryGateway gateway, IProcessFreezeCoordinator freezeCoordinator)
        : this(
            gateway,
            freezeCoordinator,
            new InMemoryAttachmentSessionRegistry(),
            new InMemoryOperationJournal(),
            SystemClock.Instance,
            NullLogger<ProcessMemoryApplicationService>.Instance)
    {
    }

    public ProcessMemoryApplicationService(
        IProcessMemoryGateway gateway,
        IProcessFreezeCoordinator freezeCoordinator,
        IAttachmentSessionRegistry sessionRegistry,
        IOperationJournal operationJournal,
        ISystemClock clock,
        ILogger<ProcessMemoryApplicationService> logger)
    {
        _gateway = gateway;
        _freezeCoordinator = freezeCoordinator;
        _sessionRegistry = sessionRegistry;
        _operationJournal = operationJournal;
        _clock = clock;
        _logger = logger;
    }

    public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
    {
        if (!selector.IsValid())
        {
            throw new ArgumentException("A process id or process name is required.", nameof(selector));
        }

        return AttachCoreAsync(selector, cancellationToken);
    }

    public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
        => ExecuteAsync("detach_process", attachmentId, async () =>
        {
            await _freezeCoordinator.UnfreezeByAttachmentAsync(attachmentId, cancellationToken);
            await _gateway.DetachAsync(attachmentId, cancellationToken);
            _sessionRegistry.Remove(attachmentId);
        });

    public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
        => ExecuteAsync("list_modules", attachmentId, () => _gateway.ListModulesAsync(attachmentId, cancellationToken));

    public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IntervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "IntervalMs must be greater than zero.");
        }

        if (request.AddressSource is ModulePointerAddressSource modulePointer && string.IsNullOrWhiteSpace(modulePointer.ModuleName))
        {
            throw new ArgumentException("A module name is required for module-relative freeze requests.", nameof(request));
        }

        return ExecuteAsync("freeze_value", request.AttachmentId, () => _freezeCoordinator.FreezeAsync(request, cancellationToken));
    }

    public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default)
        => ExecuteAsync("unfreeze_value", null, () => _freezeCoordinator.UnfreezeAsync(freezeId, cancellationToken));

    public Task<IReadOnlyList<FreezeInfo>> ListFreezesAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("list_freezes", null, () => _freezeCoordinator.ListAsync(cancellationToken));

    public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
        => ExecuteAsync("list_regions", attachmentId, () => _gateway.ListRegionsAsync(attachmentId, cancellationToken));

    public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync("resolve_pointer", request.AttachmentId, () => _gateway.ResolvePointerAsync(request, cancellationToken));

    public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleName))
        {
            throw new ArgumentException("A module name is required.", nameof(request));
        }

        return ExecuteAsync("resolve_module_pointer", request.AttachmentId, () => _gateway.ResolveModulePointerAsync(request, cancellationToken));
    }

    public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Pattern))
        {
            throw new ArgumentException("A pattern is required.", nameof(request));
        }

        if (request.MaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxResults must be greater than zero.");
        }

        return ExecuteAsync("scan_pattern", request.AttachmentId, () => _gateway.ScanPatternAsync(request, cancellationToken));
    }

    public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync("read_value", request.AttachmentId, () => _gateway.ReadAsync(request, cancellationToken));

    public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync("write_value", request.AttachmentId, () => _gateway.WriteAsync(request, cancellationToken));

    private async Task<AttachmentInfo> AttachCoreAsync(ProcessSelector selector, CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await _gateway.AttachAsync(selector, cancellationToken);
            var now = _clock.UtcNow;
            _sessionRegistry.Register(attachment, now);
            Record("attach_process", "Succeeded", attachment.AttachmentId, $"Process={attachment.ProcessName} ({attachment.ProcessId})");
            _logger.LogInformation("Attached to process {ProcessName} ({ProcessId}) as {AttachmentId}.", attachment.ProcessName, attachment.ProcessId, attachment.AttachmentId);
            return attachment;
        }
        catch (Exception exception)
        {
            Record("attach_process", "Failed", null, exception.Message);
            _logger.LogError(exception, "Failed to attach to process.");
            throw;
        }
    }

    private async Task ExecuteAsync(string operationName, AttachmentId? attachmentId, Func<Task> action)
    {
        try
        {
            await action();
            TouchSession(attachmentId);
            Record(operationName, "Succeeded", attachmentId);
            _logger.LogInformation("Operation {OperationName} succeeded for attachment {AttachmentId}.", operationName, attachmentId);
        }
        catch (Exception exception)
        {
            Record(operationName, "Failed", attachmentId, exception.Message);
            _logger.LogError(exception, "Operation {OperationName} failed for attachment {AttachmentId}.", operationName, attachmentId);
            throw;
        }
    }

    private async Task<T> ExecuteAsync<T>(string operationName, AttachmentId? attachmentId, Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            TouchSession(attachmentId);
            Record(operationName, "Succeeded", attachmentId);
            _logger.LogInformation("Operation {OperationName} succeeded for attachment {AttachmentId}.", operationName, attachmentId);
            return result;
        }
        catch (Exception exception)
        {
            Record(operationName, "Failed", attachmentId, exception.Message);
            _logger.LogError(exception, "Operation {OperationName} failed for attachment {AttachmentId}.", operationName, attachmentId);
            throw;
        }
    }

    private void TouchSession(AttachmentId? attachmentId)
    {
        if (attachmentId is null)
        {
            return;
        }

        _sessionRegistry.TryTouch(attachmentId.Value, _clock.UtcNow);
    }

    private void Record(string operationName, string outcome, AttachmentId? attachmentId, string? detail = null)
        => _operationJournal.Record(new OperationLogEntry(
            Guid.NewGuid(),
            operationName,
            outcome,
            _clock.UtcNow,
            attachmentId?.ToString(),
            detail));
}