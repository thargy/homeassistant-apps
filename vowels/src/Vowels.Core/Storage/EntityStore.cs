using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vowels.Core.Common;

namespace Vowels.Core.Storage;

public partial class EntityStore
{
    private partial class BlobSpace { } // Defined in EntityStore.BlobSpace.cs

    private static EntityStore? _instance;
    public static EntityStore Instance => _instance ?? throw new InvalidOperationException("EntityStore must be initialized with an IPageManager first.");

    public static void Initialize(IPageManager pageManager)
    {
        _instance = new EntityStore(pageManager);
    }

    /// <summary>
    /// Resets the singleton instance for testing purposes.
    /// </summary>
    public static void ResetForTesting()
    {
        _instance = null;
    }

    private readonly IPageManager _pageManager;
    private readonly StringTable _stringTable;
    private readonly BlobSpace _blobSpace;
    private readonly Dictionary<uint, uint> _entityToSchemaHead = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityStore"/> class.
    /// </summary>
    /// <param name="pageManager">The page manager used to interact with the underlying storage.</param>
    private EntityStore(IPageManager pageManager)
    {
        _pageManager = pageManager;
        if (_pageManager.PageCount == 0)
        {
            InitializeFile();
        }
        
        // Load file header to get roots
        var headerSpan = _pageManager.GetPageSpan(0);
        var header = MemoryMarshal.Read<BinarySpec.FileHeader>(headerSpan);
        
        _stringTable = new StringTable(_pageManager, header.StringTableHeadPageId);
        _blobSpace = new BlobSpace(_pageManager, header.BlobSpaceHeadPageId);
        
        LoadDirectory();
    }

    private void InitializeFile()
    {
        // Allocate Page 0 (Header)
        uint headerId = _pageManager.AllocatePage(BinarySpec.PageType.Header);
        
        // Allocate String Table Head
        uint stringTableId = _pageManager.AllocatePage(BinarySpec.PageType.StringTable);
        InitializePage(stringTableId, BinarySpec.PageType.StringTable);

        // Allocate Directory Head
        uint directoryId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        InitializePage(directoryId, BinarySpec.PageType.Directory);

        // Allocate Blob Space Head
        uint blobSpaceId = _pageManager.AllocatePage(BinarySpec.PageType.BlobSpace);
        InitializePage(blobSpaceId, BinarySpec.PageType.BlobSpace);

        var span = _pageManager.GetPageSpan(headerId);
        var header = new BinarySpec.FileHeader
        {
            Magic = BinarySpec.Magic,
            Version = 1,
            DirtyBit = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            DirectoryHeadPageId = directoryId,
            StringTableHeadPageId = stringTableId,
            BlobSpaceHeadPageId = blobSpaceId
        };
        
        MemoryMarshal.Write(span, in header);
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

    /// <summary>
    /// Gets or adds a string to the internal string table.
    /// </summary>
    internal uint GetOrAddString(string value) => _stringTable.GetOrAdd(value);

    /// <summary>
    /// Gets a string from the internal string table by its ID.
    /// </summary>
    internal string? GetString(uint id) => _stringTable.GetString(id);

    /// <summary>
    /// Gets or creates a schema chain for a given entity.
    /// </summary>
    internal (uint PageId, ushort Offset) GetOrCreateSchemaChain(uint entityIdStringId)
    {
        if (_entityToSchemaHead.TryGetValue(entityIdStringId, out uint schemaHeadId))
        {
            return (schemaHeadId, (ushort)Marshal.SizeOf<BinarySpec.PageHeader>());
        }

        // Create new schema chain
        schemaHeadId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaChain);
        InitializePage(schemaHeadId, BinarySpec.PageType.SchemaChain);
        
        // Register in Directory
        AppendToDirectory(entityIdStringId, schemaHeadId);
        
        _entityToSchemaHead[entityIdStringId] = schemaHeadId;
        return (schemaHeadId, (ushort)Marshal.SizeOf<BinarySpec.PageHeader>());
    }

    /// <summary>
    /// Switches the schema for an entity at a specific point in time.
    /// </summary>
    public void SwitchSchema(uint entityId, DateTime startTime, VowelsType stateType, ReadOnlySpan<BinarySpec.AttributeDefinition> attributes)
    {
        var (headId, _) = GetOrCreateSchemaChain(entityId);
        var lastLoc = FindLastSchemaEntry(headId);

        var header = new BinarySpec.SchemaEntryHeader
        {
            StartTime = ((DateTimeOffset)startTime).ToUnixTimeSeconds(),
            StateType = stateType,
            AttrCount = (byte)attributes.Length,
            FirstDataPageId = _pageManager.AllocatePage(BinarySpec.PageType.DataChain)
        };
        InitializePage(header.FirstDataPageId, BinarySpec.PageType.DataChain);

        var newLoc = AddSchemaEntry(headId, header, attributes);

        if (lastLoc.PageId != 0)
        {
            var span = _pageManager.GetPageSpan(lastLoc.PageId);
            ref var prevHeader = ref MemoryMarshal.AsRef<BinarySpec.SchemaEntryHeader>(span.Slice(lastLoc.Offset));
            prevHeader.NextSchemaEntryPageId = newLoc.PageId;
            prevHeader.NextSchemaEntryOffset = newLoc.Offset;
        }
    }

    /// <summary>
    /// Gets the active schema for an entity at a given point in time.
    /// </summary>
    internal BinarySpec.SchemaEntryHeader? GetActiveSchema(uint entityId, DateTime time)
    {
        if (!_entityToSchemaHead.TryGetValue(entityId, out uint currentPageId)) return null;
        
        long targetTime = ((DateTimeOffset)time).ToUnixTimeSeconds();
        ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();
        
        BinarySpec.SchemaEntryHeader? bestMatch = null;

        while (currentPageId != 0)
        {
            var span = _pageManager.GetPageSpan(currentPageId);
            var entryHeader = MemoryMarshal.Read<BinarySpec.SchemaEntryHeader>(span.Slice(currentOffset));

            if (entryHeader.StartTime <= targetTime)
            {
                bestMatch = entryHeader;
                if (entryHeader.NextSchemaEntryPageId == 0) break;

                currentPageId = entryHeader.NextSchemaEntryPageId;
                currentOffset = entryHeader.NextSchemaEntryOffset;
            }
            else break;
        }

        return bestMatch;
    }

    private (uint PageId, ushort Offset) FindLastSchemaEntry(uint headId)
    {
        uint currentPageId = headId;
        ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();
        (uint PageId, ushort Offset) lastLoc = (0, 0);

        while (currentPageId != 0)
        {
            var span = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(span);
            
            if (pageHeader.DataOffset <= currentOffset) break;

            var entryHeader = MemoryMarshal.Read<BinarySpec.SchemaEntryHeader>(span.Slice(currentOffset));
            lastLoc = (currentPageId, currentOffset);

            if (entryHeader.NextSchemaEntryPageId == 0) break;

            currentPageId = entryHeader.NextSchemaEntryPageId;
            currentOffset = entryHeader.NextSchemaEntryOffset;
        }

        return lastLoc;
    }

    private (uint PageId, ushort Offset) AddSchemaEntry(uint schemaChainHeadId, BinarySpec.SchemaEntryHeader header, ReadOnlySpan<BinarySpec.AttributeDefinition> attributes)
    {
        uint lastPageId = GetLastPageInChain(schemaChainHeadId);
        var lastPageSpan = _pageManager.GetPageSpan(lastPageId);
        var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);

        int recordSize = Marshal.SizeOf<BinarySpec.SchemaEntryHeader>() + (attributes.Length * Marshal.SizeOf<BinarySpec.AttributeDefinition>());
        
        if (pageHeader.DataOffset + recordSize > BinarySpec.PageSize)
        {
            lastPageId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaChain);
            InitializePage(lastPageId, BinarySpec.PageType.SchemaChain);
            
            pageHeader.NextPageId = lastPageId;
            MemoryMarshal.Write(lastPageSpan, in pageHeader);
            
            lastPageSpan = _pageManager.GetPageSpan(lastPageId);
            pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);
        }

        ushort writeOffset = pageHeader.DataOffset;
        MemoryMarshal.Write(lastPageSpan.Slice(writeOffset), in header);
        int attrOffset = writeOffset + Marshal.SizeOf<BinarySpec.SchemaEntryHeader>();
        
        for (int i = 0; i < attributes.Length; i++)
        {
            var attr = attributes[i];
            MemoryMarshal.Write(lastPageSpan.Slice(attrOffset), in attr);
            attrOffset += Marshal.SizeOf<BinarySpec.AttributeDefinition>();
        }

        pageHeader.DataOffset = (ushort)attrOffset;
        MemoryMarshal.Write(lastPageSpan, in pageHeader);

        return (lastPageId, writeOffset);
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
                var descriptor = new BinarySpec.EntityDescriptor 
                { 
                    EntityIdStringId = entityId, 
                    SchemaHeadPageId = schemaHeadId 
                };
                MemoryMarshal.Write(pageSpan.Slice(pageHeader.DataOffset), in descriptor);
                
                pageHeader.DataOffset += (ushort)recordSize;
                MemoryMarshal.Write(pageSpan, in pageHeader);
                return;
            }
            
            lastPageId = currentPageId;
            currentPageId = pageHeader.NextPageId;
        }
        
        uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        InitializePage(newPageId, BinarySpec.PageType.Directory);
        
        var lastSpan = _pageManager.GetPageSpan(lastPageId);
        var lastHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastSpan);
        lastHeader.NextPageId = newPageId;
        MemoryMarshal.Write(lastSpan, in lastHeader);
        
        AppendToDirectory(entityId, schemaHeadId);
    }

    /// <summary>
    /// Records the state and attributes for an entity at a given time.
    /// </summary>
    public void RecordState(uint entityId, DateTime time, object state, IReadOnlyDictionary<uint, object> attributes)
    {
        var schemaInfo = GetActiveSchemaFull(entityId, time);
        if (schemaInfo == null)
        {
            throw new InvalidOperationException($"No active schema for entity {entityId} at {time}");
        }

        AppendToDataChain(schemaInfo.Value.Header, schemaInfo.Value.Attributes, time, state, attributes);
    }

    /// <summary>
    /// Gets the full active schema details for an entity at a given point in time.
    /// </summary>
    internal (BinarySpec.SchemaEntryHeader Header, BinarySpec.AttributeDefinition[] Attributes)? GetActiveSchemaFull(uint entityId, DateTime time)
    {
        if (!_entityToSchemaHead.TryGetValue(entityId, out uint currentPageId)) return null;
        
        long targetTime = ((DateTimeOffset)time).ToUnixTimeSeconds();
        ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();
        
        BinarySpec.SchemaEntryHeader? bestMatchHeader = null;
        (uint PageId, ushort Offset) bestLoc = (0, 0);

        while (currentPageId != 0)
        {
            var span = _pageManager.GetPageSpan(currentPageId);
            var entryHeader = MemoryMarshal.Read<BinarySpec.SchemaEntryHeader>(span.Slice(currentOffset));

            if (entryHeader.StartTime <= targetTime)
            {
                bestMatchHeader = entryHeader;
                bestLoc = (currentPageId, currentOffset);
                
                if (entryHeader.NextSchemaEntryPageId == 0) break;

                currentPageId = entryHeader.NextSchemaEntryPageId;
                currentOffset = entryHeader.NextSchemaEntryOffset;
            }
            else break;
        }

        if (bestMatchHeader == null) return null;

        var bestSpan = _pageManager.GetPageSpan(bestLoc.PageId);
        int attrOffset = bestLoc.Offset + Marshal.SizeOf<BinarySpec.SchemaEntryHeader>();
        var bestMatchAttrs = new BinarySpec.AttributeDefinition[bestMatchHeader.Value.AttrCount];
        for (int i = 0; i < bestMatchAttrs.Length; i++)
        {
            bestMatchAttrs[i] = MemoryMarshal.Read<BinarySpec.AttributeDefinition>(bestSpan.Slice(attrOffset));
            attrOffset += Marshal.SizeOf<BinarySpec.AttributeDefinition>();
        }

        return (bestMatchHeader.Value, bestMatchAttrs);
    }

    private void AppendToDataChain(BinarySpec.SchemaEntryHeader schema, BinarySpec.AttributeDefinition[] attrDefs, DateTime time, object state, IReadOnlyDictionary<uint, object> attributes)
    {
        uint lastPageId = GetLastPageInChain(schema.FirstDataPageId);
        var lastPageSpan = _pageManager.GetPageSpan(lastPageId);
        var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);

        int recordSize = 8 + BinarySpec.GetTypeSize(schema.StateType);
        foreach (var attr in attrDefs) recordSize += BinarySpec.GetTypeSize(attr.Type);

        if (pageHeader.DataOffset + recordSize > BinarySpec.PageSize)
        {
            uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.DataChain);
            InitializePage(newPageId, BinarySpec.PageType.DataChain);
            
            pageHeader.NextPageId = newPageId;
            MemoryMarshal.Write(lastPageSpan, in pageHeader);
            
            lastPageId = newPageId;
            lastPageSpan = _pageManager.GetPageSpan(lastPageId);
            pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);
        }

        int writeOffset = pageHeader.DataOffset;
        BinaryPrimitives.WriteInt64LittleEndian(lastPageSpan.Slice(writeOffset), ((DateTimeOffset)time).ToUnixTimeSeconds());
        writeOffset += 8;

        WriteValue(lastPageSpan.Slice(writeOffset), schema.StateType, state);
        writeOffset += BinarySpec.GetTypeSize(schema.StateType);

        foreach (var attrDef in attrDefs)
        {
            if (attributes.TryGetValue(attrDef.NameStringId, out var val))
            {
                WriteValue(lastPageSpan.Slice(writeOffset), attrDef.Type, val);
            }
            writeOffset += BinarySpec.GetTypeSize(attrDef.Type);
        }

        pageHeader.DataOffset = (ushort)writeOffset;
        MemoryMarshal.Write(lastPageSpan, in pageHeader);
    }

    private void WriteValue(Span<byte> span, VowelsType type, object value)
    {
        switch (type)
        {
            case VowelsType.Double:
                BinaryPrimitives.WriteDoubleLittleEndian(span, (double)value);
                break;
            case VowelsType.Int64:
                BinaryPrimitives.WriteInt64LittleEndian(span, (long)value);
                break;
            case VowelsType.Boolean:
                span[0] = (bool)value ? (byte)1 : (byte)0;
                break;
            case VowelsType.StringId:
                BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)value);
                break;
            case VowelsType.Timestamp:
                BinaryPrimitives.WriteInt64LittleEndian(span, ((DateTimeOffset)value).ToUnixTimeSeconds());
                break;
        }
    }

    /// <summary>
    /// Stores a blob of data in the storage engine.
    /// </summary>
    public BinarySpec.BlobPointer StoreBlob(ReadOnlySpan<byte> data) => _blobSpace.StoreBlob(data);

    /// <summary>
    /// Reads a blob of data from the storage engine.
    /// </summary>
    public byte[] ReadBlob(BinarySpec.BlobPointer pointer) => _blobSpace.ReadBlob(pointer);

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
