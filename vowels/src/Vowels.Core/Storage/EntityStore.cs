using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Vowels.Core.Storage;

public class EntityStore
{
    private readonly IPageManager _pageManager;
    private readonly Dictionary<uint, uint> _entityToSchemaHead = new();

    public EntityStore(IPageManager pageManager)
    {
        _pageManager = pageManager;
        if (_pageManager.PageCount == 0)
        {
            InitializeFile();
        }
        else
        {
            LoadDirectory();
        }
    }

    private void InitializeFile()
    {
        // Allocate Page 0 (Header)
        uint headerId = _pageManager.AllocatePage(BinarySpec.PageType.Header);
        
        // Allocate String Table Head
        uint stringTableId = _pageManager.AllocatePage(BinarySpec.PageType.StringTable);
        InitializePage(stringTableId, BinarySpec.PageType.StringTable);

        // Allocate Directory Head (System Entity 0)
        uint directoryId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        InitializePage(directoryId, BinarySpec.PageType.Directory);

        var span = _pageManager.GetPageSpan(headerId);
        var header = new BinarySpec.FileHeader
        {
            Magic = BinarySpec.Magic,
            Version = 1,
            DirtyBit = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DirectoryHeadPageId = directoryId,
            StringTableHeadPageId = stringTableId
        };
        
        MemoryMarshal.Write(span, ref header);
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
        MemoryMarshal.Write(span, ref header);
    }

    private void LoadDirectory()
    {
        var headerSpan = _pageManager.GetPageSpan(0);
        var header = MemoryMarshal.Read<BinarySpec.FileHeader>(headerSpan);
        
        uint currentPageId = header.DirectoryHeadPageId;
        while (currentPageId != 0)
        {
            var pageSpan = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(pageSpan);
            
            int recordSize = Marshal.SizeOf<BinarySpec.EntityDescriptor>();
            int offset = Marshal.SizeOf<BinarySpec.PageHeader>();
            
            while (offset + recordSize <= pageHeader.DataOffset)
            {
                var descriptor = MemoryMarshal.Read<BinarySpec.EntityDescriptor>(pageSpan.Slice(offset));
                _entityToSchemaHead[descriptor.EntityIdStringId] = descriptor.SchemaHeadPageId;
                offset += recordSize;
            }
            
            currentPageId = pageHeader.NextPageId;
        }
    }

    public uint GetOrCreateSchemaChain(uint entityIdStringId)
    {
        if (_entityToSchemaHead.TryGetValue(entityIdStringId, out uint schemaHeadId))
        {
            return schemaHeadId;
        }

        // Create new schema chain
        schemaHeadId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaChain);
        InitializePage(schemaHeadId, BinarySpec.PageType.SchemaChain);
        
        // Register in Directory
        AppendToDirectory(entityIdStringId, schemaHeadId);
        
        _entityToSchemaHead[entityIdStringId] = schemaHeadId;
        return schemaHeadId;
    }

    private void AppendToDirectory(uint entityId, uint schemaHeadId)
    {
        var fileHeaderSpan = _pageManager.GetPageSpan(0);
        var fileHeader = MemoryMarshal.Read<BinarySpec.FileHeader>(fileHeaderSpan);
        
        uint currentPageId = fileHeader.DirectoryHeadPageId;
        uint lastPageId = currentPageId;
        
        while (currentPageId != 0)
        {
            var pageSpan = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(pageSpan);
            
            int recordSize = Marshal.SizeOf<BinarySpec.EntityDescriptor>();
            if (pageHeader.DataOffset + recordSize <= BinarySpec.PageSize)
            {
                // Found space
                var descriptor = new BinarySpec.EntityDescriptor 
                { 
                    EntityIdStringId = entityId, 
                    SchemaHeadPageId = schemaHeadId 
                };
                MemoryMarshal.Write(pageSpan.Slice(pageHeader.DataOffset), ref descriptor);
                
                // Update offset
                pageHeader.DataOffset += (ushort)recordSize;
                MemoryMarshal.Write(pageSpan, ref pageHeader);
                return;
            }
            
            lastPageId = currentPageId;
            currentPageId = pageHeader.NextPageId;
        }
        
        // No space, allocate new directory page
        uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        InitializePage(newPageId, BinarySpec.PageType.Directory);
        
        // Link from previous
        var lastSpan = _pageManager.GetPageSpan(lastPageId);
        var lastHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastSpan);
        lastHeader.NextPageId = newPageId;
        MemoryMarshal.Write(lastSpan, ref lastHeader);
        
        // Write to new page
        AppendToDirectory(entityId, schemaHeadId);
    }

    public void AddSchemaEntry(uint schemaChainHeadId, BinarySpec.SchemaEntryHeader header, ReadOnlySpan<BinarySpec.AttributeDefinition> attributes)
    {
        uint lastPageId = GetLastPageInChain(schemaChainHeadId);
        var lastPageSpan = _pageManager.GetPageSpan(lastPageId);
        var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);

        int recordSize = Marshal.SizeOf<BinarySpec.SchemaEntryHeader>() + (attributes.Length * Marshal.SizeOf<BinarySpec.AttributeDefinition>());
        
        if (pageHeader.DataOffset + recordSize > BinarySpec.PageSize)
        {
            // Allocate new page in schema chain
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaChain);
            InitializePage(newPageId, BinarySpec.PageType.SchemaChain);
            
            // Link
            pageHeader.NextPageId = newPageId;
            MemoryMarshal.Write(lastPageSpan, ref pageHeader);
            
            lastPageId = newPageId;
            lastPageSpan = _pageManager.GetPageSpan(lastPageId);
            pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);
        }

        // Write Header
        MemoryMarshal.Write(lastPageSpan.Slice(pageHeader.DataOffset), ref header);
        int offset = pageHeader.DataOffset + Marshal.SizeOf<BinarySpec.SchemaEntryHeader>();
        
        // Write Attributes
        for (int i = 0; i < attributes.Length; i++)
        {
            var attr = attributes[i];
            MemoryMarshal.Write(lastPageSpan.Slice(offset), ref attr);
            offset += Marshal.SizeOf<BinarySpec.AttributeDefinition>();
        }

        // Update Page Offset
        pageHeader.DataOffset = (ushort)offset;
        MemoryMarshal.Write(lastPageSpan, ref pageHeader);
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
