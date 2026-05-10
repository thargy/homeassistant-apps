using System;

namespace Vowels.FileStoreRegistry.Storage;

internal interface IPageManager : IDisposable
{
    uint PageCount { get; }
    uint AllocatedPages { get; set; }
    uint AllocatePage(BinarySpec.PageType type);
    Span<byte> GetPageSpan(uint pageId);
    void Flush();
}
