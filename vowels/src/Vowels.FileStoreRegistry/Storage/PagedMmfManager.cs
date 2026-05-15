using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Vowels.FileStoreRegistry.Storage;

internal unsafe class PagedMmfManager : IPageManager
{
    private readonly string _filePath;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private byte* _basePtr;
    private long _currentSize;
    private uint _pageCount;
    public uint PageCount => _pageCount;

    private uint _allocatedPages = 0;
    public uint AllocatedPages
    {
        get => _allocatedPages;
        set
        {
            if (value > _pageCount) Grow((long)value * BinarySpec.PageSize);
            _allocatedPages = value;
        }
    }

    public PagedMmfManager(string filePath, long initialSize = 64 * 1024)
    {
        _filePath = filePath;

        using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            _currentSize = fileStream.Length;
            _pageCount = (uint)(_currentSize / BinarySpec.PageSize);

            if (_currentSize > 0)
            {
                _allocatedPages = _pageCount;
            }
            else
            {
                _allocatedPages = 0;
            }
        }

        if (_currentSize > 0)
        {
            _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePtr);
        }
    }

    public uint AllocatePage(BinarySpec.PageType type)
    {
        lock (this)
        {
            uint newPageId = _allocatedPages++;
            long requiredSize = (long)_allocatedPages * BinarySpec.PageSize;

            if (requiredSize > _currentSize)
            {
                Grow(requiredSize);
            }

            var header = (BinarySpec.PageHeader*)(_basePtr + (newPageId * BinarySpec.PageSize));
            header->Type = type;
            header->NextPageId = 0;
            header->DataOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();

            return newPageId;
        }
    }

    private void Grow(long newSize)
    {
        long growTo = Math.Max(newSize, _currentSize * 2);

        if (_accessor != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _accessor = null!;
        }

        if (_mmf != null)
        {
            _mmf.Dispose();
            _mmf = null!;
        }

        using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            fs.SetLength(growTo);
        }

        _currentSize = growTo;
        _pageCount = (uint)(_currentSize / BinarySpec.PageSize);
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
        _accessor?.Flush();
    }

    public void Dispose()
    {
        if (_basePtr != null)
        {
            _accessor?.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePtr = null;
        }
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}
