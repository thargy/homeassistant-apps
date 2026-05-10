using System.Text;

namespace Vowels.Core.Storage;

public class StringTable
{
    private readonly IPageManager _pageManager;
    private readonly Dictionary<string, uint> _stringToId = new();
    private readonly Dictionary<uint, string> _idToString = new();
    private uint _currentPageId;
    private int _currentOffset;
    private uint _nextId = 1; // 0 could be reserved for null/empty

    public StringTable(IPageManager pageManager, uint initialPageId)
    {
        _pageManager = pageManager;
        _currentPageId = initialPageId;
        _currentOffset = 7; // PageHeader size (1 + 4 + 2)
    }

    public uint GetOrAdd(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (_stringToId.TryGetValue(value, out uint id)) return id;

        id = _nextId++;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue) throw new ArgumentException("String too long");

        int entrySize = 2 + bytes.Length; // Length(2) + Data

        if (_currentOffset + entrySize > BinarySpec.PageSize)
        {
            // Allocate new page and link
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.StringTable);
            LinkPages(_currentPageId, newPageId);
            _currentPageId = newPageId;
            _currentOffset = 7;
        }

        var span = _pageManager.GetPageSpan(_currentPageId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(_currentOffset, 2), (ushort)bytes.Length);
        bytes.CopyTo(span.Slice(_currentOffset + 2, bytes.Length));

        _currentOffset += entrySize;
        UpdatePageDataOffset(_currentPageId, (ushort)_currentOffset);

        _stringToId[value] = id;
        _idToString[id] = value;
        return id;
    }

    private void LinkPages(uint current, uint next)
    {
        var span = _pageManager.GetPageSpan(current);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), next); // NextPageId at offset 1
    }

    private void UpdatePageDataOffset(uint pageId, ushort offset)
    {
        var span = _pageManager.GetPageSpan(pageId);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(5, 2), offset); // DataOffset at offset 5
    }
}
