using System;
using System.Runtime.InteropServices;

namespace Vowels.FileStoreRegistry.Storage;

internal class BlobSpace
{
    private readonly IPageManager _pageManager;
    private uint _headPageId;

    public BlobSpace(IPageManager pageManager, uint headPageId)
    {
        _pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
        _headPageId = headPageId;
    }

    /// <summary>
    /// Stores a blob and returns a pointer to it.
    /// </summary>
    public BinarySpec.BlobPointer StoreBlob(ReadOnlySpan<byte> data)
    {
        if (data.Length > ushort.MaxValue)
            throw new ArgumentException("Blob too large; maximum size is 64KB.", nameof(data));

        uint currentPageId = _headPageId;
        int requiredSize = data.Length;

        uint lastPageId = GetLastPageInChain(currentPageId);
        var lastPageSpan = _pageManager.GetPageSpan(lastPageId);
        var header = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);

        if (header.DataOffset + requiredSize > BinarySpec.PageSize)
        {
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.BlobSpace);
            InitializePage(newPageId, BinarySpec.PageType.BlobSpace);

            header.NextPageId = newPageId;
            MemoryMarshal.Write(lastPageSpan, in header);

            lastPageId = newPageId;
            lastPageSpan = _pageManager.GetPageSpan(lastPageId);
            header = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);
        }

        ushort offset = header.DataOffset;
        data.CopyTo(lastPageSpan[offset..]);

        header.DataOffset = (ushort)(offset + requiredSize);
        MemoryMarshal.Write(lastPageSpan, in header);

        return new BinarySpec.BlobPointer
        {
            PageId = lastPageId,
            Offset = offset,
            Length = (ushort)data.Length
        };
    }

    /// <summary>
    /// Reads a blob using a pointer.
    /// </summary>
    public byte[] ReadBlob(BinarySpec.BlobPointer pointer)
    {
        var span = _pageManager.GetPageSpan(pointer.PageId);
        return span.Slice(pointer.Offset, pointer.Length).ToArray();
    }

    private void InitializePage(uint pageId, BinarySpec.PageType type)
    {
        var span = _pageManager.GetPageSpan(pageId);
        var header = new BinarySpec.PageHeader
        {
            Type = type,
            NextPageId = 0,
            DataOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>()
        };
        MemoryMarshal.Write(span, in header);
    }

    private uint GetLastPageInChain(uint headPageId)
    {
        uint current = headPageId;
        while (true)
        {
            var span = _pageManager.GetPageSpan(current);
            var header = MemoryMarshal.Read<BinarySpec.PageHeader>(span);
            if (header.NextPageId == 0) return current;
            current = header.NextPageId;
        }
    }
}
