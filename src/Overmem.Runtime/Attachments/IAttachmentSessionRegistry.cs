using Overmem.Abstractions.Processes;

namespace Overmem.Runtime.Attachments;

public interface IAttachmentSessionRegistry
{
    void Register(AttachmentInfo attachment, DateTimeOffset timestampUtc);

    bool TryTouch(AttachmentId attachmentId, DateTimeOffset timestampUtc);

    bool Remove(AttachmentId attachmentId);

    IReadOnlyList<AttachmentSessionInfo> ListActive();
}