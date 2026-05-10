using System.Buffers.Binary;

namespace Vowels.Core.Storage;

public class EntityStore
{
    private readonly IPageManager _pageManager;
    private readonly Dictionary<uint, (uint FirstPage, uint LastPage, ushort SchemaId, uint Reserved)> _directory = new();

    public EntityStore(IPageManager pageManager)
    {
        _pageManager = pageManager;
        if (_pageManager.PageCount == 0)
        {
            InitializeFileHeader();
        }
        else
        {
            LoadDirectory();
        }
    }

    private void InitializeFileHeader()
    {
        uint pageId = _pageManager.AllocatePage(BinarySpec.PageType.Header);
        var span = _pageManager.GetPageSpan(pageId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), BinarySpec.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), 1); // Version
        span[6] = 0; // DirtyBit
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(7, 8), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        // EntityCount at 27
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(27, 4), 0);
    }

    private void LoadDirectory()
    {
        var span = _pageManager.GetPageSpan(0);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(27, 4));
        int offset = 31;
        for (int i = 0; i < count; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 4, 4));
            uint last = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 8, 4));
            ushort schema = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + 12, 2));
            uint reserved = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 14, 4));
            _directory[id] = (first, last, schema, reserved);
            offset += 18;
        }
    }

    public void RecordState(uint entityStringId, ushort schemaId, byte confidence, uint timestampOffset, ReadOnlySpan<byte> data)
    {
        if (!_directory.TryGetValue(entityStringId, out var entry))
        {
            uint pageId = _pageManager.AllocatePage(BinarySpec.PageType.EntityData);
            InitializeEntityPage(pageId, entityStringId, schemaId);
            entry = (pageId, pageId, schemaId, 1);
            _directory[entityStringId] = entry;
            UpdateDirectoryInPage0(entityStringId, entry);
        }

        uint currentPageId = entry.LastPage;
        var span = _pageManager.GetPageSpan(currentPageId);
        ushort currentOffset = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(5, 2));
        
        bool schemaChanged = entry.SchemaId != schemaId;
        int requiredSize = (schemaChanged ? 7 : 0) + 5 + data.Length;

        if (currentOffset + requiredSize > BinarySpec.PageSize)
        {
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.EntityData);
            LinkPages(currentPageId, newPageId);
            InitializeEntityPage(newPageId, entityStringId, schemaId);
            
            entry = (entry.FirstPage, newPageId, schemaId, entry.Reserved + 1); // Increment reserved as heuristic
            _directory[entityStringId] = entry;
            UpdateDirectoryInPage0(entityStringId, entry);
            
            currentPageId = newPageId;
            span = _pageManager.GetPageSpan(currentPageId);
            currentOffset = (ushort)(7 + 10);
            schemaChanged = false;
            requiredSize = 5 + data.Length;
        }

        int offset = currentOffset;
        if (schemaChanged)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), uint.MaxValue);
            span[offset + 4] = 0x01; // Schema Switch
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 5, 2), schemaId);
            offset += 7;
            
            entry = (entry.FirstPage, currentPageId, schemaId, entry.Reserved);
            _directory[entityStringId] = entry;
            UpdateDirectoryInPage0(entityStringId, entry);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), timestampOffset);
        span[offset + 4] = confidence;
        data.CopyTo(span.Slice(offset + 5, data.Length));
        
        UpdatePageDataOffset(currentPageId, (ushort)(offset + 5 + data.Length));
    }

    private void UpdateDirectoryInPage0(uint entityStringId, (uint FirstPage, uint LastPage, ushort SchemaId, uint Reserved) entry)
    {
        var span = _pageManager.GetPageSpan(0);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(27, 4));
        int offset = 31;
        for (int i = 0; i < count; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) == entityStringId)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 4, 4), entry.FirstPage);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 8, 4), entry.LastPage);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 12, 2), entry.SchemaId);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 14, 4), entry.Reserved);
                return;
            }
            offset += 18;
        }

        // Add new
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), entityStringId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 4, 4), entry.FirstPage);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 8, 4), entry.LastPage);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 12, 2), entry.SchemaId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 14, 4), entry.Reserved);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(27, 4), count + 1);
    }

    private void InitializeEntityPage(uint pageId, uint entityId, ushort schemaId)
    {
        var span = _pageManager.GetPageSpan(pageId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(7, 4), entityId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(11, 2), schemaId);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(13, 4), BinarySpec.InvalidPageId);
        UpdatePageDataOffset(pageId, 7 + 10);
    }

    private void LinkPages(uint current, uint next)
    {
        var span = _pageManager.GetPageSpan(current);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(1, 4), next);
    }

    private void UpdatePageDataOffset(uint pageId, ushort offset)
    {
        var span = _pageManager.GetPageSpan(pageId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(5, 2), offset);
    }
}
