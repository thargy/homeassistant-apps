using System.Buffers.Binary;

namespace Vowels.Core.Storage;

public class SchemaRegistry
{
    private readonly IPageManager _pageManager;
    private readonly StringTable _stringTable;
    private readonly Dictionary<string, ushort> _fingerprintToId = new();
    private uint _currentPageId;
    private int _currentOffset;
    private ushort _nextId = 1;

    public SchemaRegistry(IPageManager pageManager, StringTable stringTable, uint initialPageId)
    {
        _pageManager = pageManager;
        _stringTable = stringTable;
        _currentPageId = initialPageId;
        _currentOffset = 7; // PageHeader size (1 + 4 + 2)
    }

    public ushort GetOrRegister(IEnumerable<(string Name, BinarySpec.HaType Type)> attributes)
    {
        var sortedAttrs = attributes.OrderBy(a => a.Name).ToList();
        var fingerprint = string.Join("|", sortedAttrs.Select(a => $"{a.Name}:{a.Type}"));

        if (_fingerprintToId.TryGetValue(fingerprint, out ushort id)) return id;

        id = _nextId++;
        // Format: [SchemaID (2), AttrCount (1), {AttrNameID (4), Type (1)}[]]
        // Note: Design doc also mentioned StateType(1), let's add it if needed, 
        // but for now let's match the design doc's specified size if possible.
        int entrySize = 2 + 1 + (sortedAttrs.Count * 5); 

        if (_currentOffset + entrySize > BinarySpec.PageSize)
        {
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaRegistry);
            LinkPages(_currentPageId, newPageId);
            _currentPageId = newPageId;
            _currentOffset = 7;
        }

        var span = _pageManager.GetPageSpan(_currentPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(_currentOffset, 2), id);
        span[_currentOffset + 2] = (byte)sortedAttrs.Count;
        
        int offset = _currentOffset + 3;
        foreach (var attr in sortedAttrs)
        {
            uint nameId = _stringTable.GetOrAdd(attr.Name);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), nameId);
            span[offset + 4] = (byte)attr.Type;
            offset += 5;
        }

        _currentOffset += entrySize;
        UpdatePageDataOffset(_currentPageId, (ushort)_currentOffset);

        _fingerprintToId[fingerprint] = id;
        return id;
    }

    private void LinkPages(uint current, uint next)
    {
        var span = _pageManager.GetPageSpan(current);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), next); // NextPageId at offset 1
    }

    private void UpdatePageDataOffset(uint pageId, ushort offset)
    {
        var span = _pageManager.GetPageSpan(pageId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(5, 2), offset); // DataOffset at offset 5
    }
}
