using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vowels.Core.Storage;

public unsafe class PagedMmfManager : IPageManager
{
    private readonly string _filePath;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _accessor;
    private byte* _basePtr;
    private long _currentSize;
    private uint _pageCount;
    public uint PageCount => _pageCount;

    public PagedMmfManager(string filePath, long initialSize = 64 * 1024)
    {
        _filePath = filePath;
        _currentSize = initialSize;
        
        var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        if (fileStream.Length < initialSize)
        {
            fileStream.SetLength(initialSize);
        }
        _currentSize = fileStream.Length;
        _pageCount = 0; // Next available page
        
        _mmf = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePtr);
    }

    public uint AllocatePage(BinarySpec.PageType type)
    {
        // For simplicity in POC, we just grow the file if needed
        lock (this)
        {
            uint newPageId = _pageCount++;
            long newSize = (long)_pageCount * BinarySpec.PageSize;
            
            if (newSize > _currentSize)
            {
                Grow(newSize);
            }

            var header = (BinarySpec.PageHeader*)(_basePtr + (newPageId * BinarySpec.PageSize));
            header->Type = type;
            header->NextPageId = BinarySpec.InvalidPageId;
            header->DataOffset = 0;

            return newPageId;
        }
    }

    private void Grow(long newSize)
    {
        // In a real implementation, we'd remap. For POC, we'll double the size to avoid frequent remaps.
        long growTo = Math.Max(newSize, _currentSize * 2);
        
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();

        using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            fs.SetLength(growTo);
        }

        _currentSize = growTo;
        _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePtr);
    }

    public Span<byte> GetPageSpan(uint pageId)
    {
        if (pageId >= _pageCount) throw new ArgumentOutOfRangeException(nameof(pageId));
        return new Span<byte>(_basePtr + (pageId * BinarySpec.PageSize), BinarySpec.PageSize);
    }

    public void Flush()
    {
        _accessor.Flush();
    }

    public void Dispose()
    {
        if (_basePtr != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
