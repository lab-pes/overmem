using Overmem.Abstractions.Processes;
using System.Collections.Concurrent;

namespace Overmem.Runtime.Attachments;

public sealed class InMemoryAttachmentSessionRegistry : IAttachmentSessionRegistry
{
    private readonly ConcurrentDictionary<AttachmentId, AttachmentSessionInfo> _sessions = new();

    public IReadOnlyList<AttachmentSessionInfo> ListActive()
        => _sessions.Values
            .OrderBy(session => session.AttachedAtUtc)
            .ToArray();

    public bool Remove(AttachmentId attachmentId)
        => _sessions.TryRemove(attachmentId, out _);

    public void Register(AttachmentInfo attachment, DateTimeOffset timestampUtc)
    {
        var session = new AttachmentSessionInfo(
            attachment.AttachmentId,
            attachment.ProcessId,
            attachment.ProcessName,
            attachment.Architecture,
            timestampUtc,
            timestampUtc);

        _sessions[attachment.AttachmentId] = session;
    }

    public bool TryTouch(AttachmentId attachmentId, DateTimeOffset timestampUtc)
    {
        if (!_sessions.TryGetValue(attachmentId, out var session))
        {
            return false;
        }

        var updated = session with { LastSeenAtUtc = timestampUtc };
        return _sessions.TryUpdate(attachmentId, updated, session);
    }
}