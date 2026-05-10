namespace Vowels.Core.Storage;

public interface IPageManager : IDisposable
{
    uint PageCount { get; }
    uint AllocatePage(BinarySpec.PageType type);
    Span<byte> GetPageSpan(uint pageId);
    void Flush();
}
