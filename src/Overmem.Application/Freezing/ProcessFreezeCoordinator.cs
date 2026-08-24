using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using System.Collections.Concurrent;

namespace Overmem.Application.Freezing;

public sealed class ProcessFreezeCoordinator(IProcessMemoryGateway gateway) : IProcessFreezeCoordinator, IDisposable
{
    private readonly ConcurrentDictionary<FreezeId, FreezeRegistration> _registrations = new();

    public async Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var freezeId = FreezeId.New();
        var registration = new FreezeRegistration(freezeId, request);
        if (!_registrations.TryAdd(freezeId, registration))
        {
            throw new InvalidOperationException("Failed to register the freeze operation.");
        }

        try
        {
            await ApplyFreezeAsync(registration, cancellationToken);
            registration.ExecutionTask = Task.Run(() => RunAsync(registration), CancellationToken.None);
            return registration.Snapshot();
        }
        catch
        {
            _registrations.TryRemove(freezeId, out _);
            registration.Dispose();
            throw;
        }
    }

    public async Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_registrations.TryRemove(freezeId, out var registration))
        {
            return false;
        }

        registration.Cancel();
        await registration.AwaitCompletionAsync();
        registration.Dispose();
        return true;
    }

    public async Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var freezeIds = _registrations.Values
            .Where(registration => registration.Request.AttachmentId == attachmentId)
            .Select(registration => registration.FreezeId)
            .ToArray();

        var removed = 0;
        foreach (var freezeId in freezeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await UnfreezeAsync(freezeId, cancellationToken))
            {
                removed++;
            }
        }

        return removed;
    }

    public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FreezeInfo>>(_registrations.Values.Select(registration => registration.Snapshot()).ToArray());
    }

    public void Dispose()
    {
        foreach (var registration in _registrations.Values)
        {
            registration.Cancel();
        }

        Task.WaitAll(_registrations.Values.Select(static registration => registration.ExecutionTask ?? Task.CompletedTask).ToArray(), TimeSpan.FromSeconds(2));

        foreach (var registration in _registrations.Values)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    private async Task RunAsync(FreezeRegistration registration)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(registration.Request.IntervalMs));
            while (await timer.WaitForNextTickAsync(registration.CancellationTokenSource.Token))
            {
                await ApplyFreezeAsync(registration, registration.CancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException) when (registration.CancellationTokenSource.IsCancellationRequested)
        {
            registration.SetStatus(FreezeStatus.Cancelled);
        }
        catch (Exception ex)
        {
            registration.SetStatus(FreezeStatus.Faulted, ex.Message);
        }
    }

    private async Task ApplyFreezeAsync(FreezeRegistration registration, CancellationToken cancellationToken)
    {
        var address = await ResolveAddressAsync(registration.Request, cancellationToken);
        await gateway.WriteAsync(
            new WriteMemoryRequest(
                registration.Request.AttachmentId,
                address,
                registration.Request.ValueKind,
                registration.Request.Value,
                registration.Request.Size),
            cancellationToken);
    }

    private async Task<ulong> ResolveAddressAsync(FreezeRequest request, CancellationToken cancellationToken)
    {
        return request.AddressSource switch
        {
            AbsoluteAddressSource absolute => absolute.Address,
            PointerAddressSource pointer => (await gateway.ResolvePointerAsync(
                new ResolvePointerRequest(request.AttachmentId, pointer.BaseAddress, pointer.Offsets),
                cancellationToken)).ResolvedAddress,
            ModulePointerAddressSource modulePointer => (await gateway.ResolveModulePointerAsync(
                new ResolveModulePointerRequest(request.AttachmentId, modulePointer.ModuleName, modulePointer.BaseOffset, modulePointer.Offsets),
                cancellationToken)).ResolvedAddress,
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unsupported freeze address source."),
        };
    }

    private sealed class FreezeRegistration(FreezeId freezeId, FreezeRequest request) : IDisposable
    {
        private readonly object _sync = new();
        private FreezeStatus _status = FreezeStatus.Active;
        private string? _errorMessage;

        public FreezeId FreezeId { get; } = freezeId;

        public FreezeRequest Request { get; } = request;

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public Task? ExecutionTask { get; set; }

        public void Cancel() => CancellationTokenSource.Cancel();

        public void SetStatus(FreezeStatus status, string? errorMessage = null)
        {
            lock (_sync)
            {
                _status = status;
                _errorMessage = errorMessage;
            }
        }

        public FreezeInfo Snapshot()
        {
            lock (_sync)
            {
                return new FreezeInfo(
                    FreezeId,
                    Request.AttachmentId,
                    Request.AddressSource,
                    Request.ValueKind,
                    Request.Value,
                    Request.Size,
                    Request.IntervalMs,
                    _status,
                    _errorMessage);
            }
        }

        public async Task AwaitCompletionAsync()
        {
            if (ExecutionTask is not null)
            {
                try
                {
                    await ExecutionTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        public void Dispose() => CancellationTokenSource.Dispose();
    }
}