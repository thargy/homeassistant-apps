using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Linq;
using Vowels.Common;
using Vowels.FileStoreRegistry.Storage;

namespace Vowels.FileStoreRegistry;

internal class HourlyMmfFile : IDisposable
{
    private readonly IPageManager _pageManager;
    private readonly StringTable _stringTable;
    private readonly BlobSpace _blobSpace;
    private readonly Dictionary<uint, uint> _entityToSchemaHead = new();

    public IEnumerable<string> GetKnownEntityIds()
    {
        return _entityToSchemaHead.Keys.Select(id => _stringTable.GetString(id)).Where(s => s != null)!;
    }

    public HourlyMmfFile(string filePath)
    {
        _pageManager = new PagedMmfManager(filePath);
        
        bool initialized = false;
        if (_pageManager.PageCount > 0)
        {
            var headerSpan = _pageManager.GetPageSpan(0);
            var header = MemoryMarshal.Read<BinarySpec.FileHeader>(headerSpan);
            if (header.Magic == BinarySpec.Magic)
            {
                initialized = true;
            }
        }

        if (!initialized)
        {
            _pageManager.AllocatedPages = 0; // Force start at 0 if not valid
            InitializeFile();
        }
        else
        {
            // If valid, trust the file length for now (manager already set it to PageCount)
        }
        
        var headSpan = _pageManager.GetPageSpan(0);
        var head = MemoryMarshal.Read<BinarySpec.FileHeader>(headSpan);
        
        _stringTable = new StringTable(_pageManager, head.StringTableHeadPageId);
        _blobSpace = new BlobSpace(_pageManager, head.BlobSpaceHeadPageId);
        
        LoadDirectory();
    }

    private void InitializeFile()
    {
        uint headerId = _pageManager.AllocatePage(BinarySpec.PageType.Header);
        uint stringTableId = _pageManager.AllocatePage(BinarySpec.PageType.StringTable);
        uint directoryId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        uint blobSpaceId = _pageManager.AllocatePage(BinarySpec.PageType.BlobSpace);

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

    public void AddValue(string entityId, VowelsType type, object value, DateTime timestamp)
    {
        uint id = _stringTable.GetOrAdd(entityId);
        var schema = GetActiveSchema(id, timestamp);
        
        if (schema == null || schema.Value.StateType != type)
        {
            SwitchSchema(id, timestamp, type, ReadOnlySpan<BinarySpec.AttributeDefinition>.Empty);
            schema = GetActiveSchema(id, timestamp);
        }
        
        var schemaFull = GetActiveSchemaFull(id, timestamp);
        if (schemaFull != null)
        {
            AppendToDataChain(schemaFull.Value.Header, schemaFull.Value.Attributes, timestamp, value, new Dictionary<uint, object>());
        }
    }

    public IEnumerable<EntityValue> GetValues(IEnumerable<IHandle> handles, DateTime startTime, DateTime endTime)
    {
        long startUnix = ((DateTimeOffset)startTime).ToUnixTimeSeconds();
        long endUnix = ((DateTimeOffset)endTime).ToUnixTimeSeconds();

        foreach (var handle in handles)
        {
            uint entityIdStringId = _stringTable.GetId(handle.EntityId);
            if (entityIdStringId == 0 || !_entityToSchemaHead.TryGetValue(entityIdStringId, out uint currentPageId)) continue;
            ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();

            while (currentPageId != 0)
            {
                BinarySpec.SchemaEntryHeader entryHeader;
                BinarySpec.AttributeDefinition[] attrs;
                uint nextEntryPageId;
                ushort nextEntryOffset;

                {
                    var span = _pageManager.GetPageSpan(currentPageId);
                    entryHeader = MemoryMarshal.Read<BinarySpec.SchemaEntryHeader>(span.Slice(currentOffset));
                    nextEntryPageId = entryHeader.NextSchemaEntryPageId;
                    nextEntryOffset = entryHeader.NextSchemaEntryOffset;

                    // Load attributes
                    int attrCount = entryHeader.AttrCount;
                    attrs = new BinarySpec.AttributeDefinition[attrCount];
                    int attrOffset = currentOffset + Marshal.SizeOf<BinarySpec.SchemaEntryHeader>();
                    for (int i = 0; i < attrCount; i++)
                    {
                        attrs[i] = MemoryMarshal.Read<BinarySpec.AttributeDefinition>(span.Slice(attrOffset));
                        attrOffset += Marshal.SizeOf<BinarySpec.AttributeDefinition>();
                    }
                }

                // Determine when this schema ends
                long nextSchemaStart = nextEntryPageId != 0 
                    ? MemoryMarshal.Read<BinarySpec.SchemaEntryHeader>(_pageManager.GetPageSpan(nextEntryPageId).Slice(nextEntryOffset)).StartTime 
                    : long.MaxValue;

                if (entryHeader.StartTime < endUnix && nextSchemaStart > startUnix)
                {
                    // This schema is relevant. Determine if we are looking for a specific attribute or the state
                    int? targetAttrIndex = null;
                    if (handle is SensorAttributeHandle attrHandle)
                    {
                        uint attrNameId = _stringTable.GetId(attrHandle.AttributeName);
                        if (attrNameId != 0)
                        {
                            for (int i = 0; i < attrs.Length; i++)
                            {
                                if (attrs[i].NameStringId == attrNameId)
                                {
                                    targetAttrIndex = i;
                                    break;
                                }
                            }
                        }
                        if (targetAttrIndex == null) goto next_loop;
                    }

                    // Traverse data pages
                    uint dataPageId = entryHeader.FirstDataPageId;
                    while (dataPageId != 0)
                    {
                        var pageValues = new List<EntityValue>();
                        uint nextDataPageId = 0;
                        
                        {
                            var dataSpan = _pageManager.GetPageSpan(dataPageId);
                            var dataHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(dataSpan);
                            nextDataPageId = dataHeader.NextPageId;
                            int offset = Marshal.SizeOf<BinarySpec.PageHeader>();

                            while (offset < dataHeader.DataOffset)
                            {
                                long ts = BinaryPrimitives.ReadInt64LittleEndian(dataSpan.Slice(offset, 8));
                                int recordStart = offset;
                                offset += 8;

                                if (ts >= startUnix && ts <= endUnix)
                                {
                                    int valueOffset = offset;
                                    if (targetAttrIndex.HasValue)
                                    {
                                        valueOffset += BinarySpec.GetTypeSize(entryHeader.StateType);
                                        for (int i = 0; i < targetAttrIndex.Value; i++)
                                        {
                                            valueOffset += BinarySpec.GetTypeSize(attrs[i].Type);
                                        }
                                        pageValues.Add(new EntityValue(
                                            handle,
                                            DateTimeOffset.FromUnixTimeSeconds(ts).DateTime,
                                            100,
                                            attrs[targetAttrIndex.Value].Type,
                                            ReadValue(dataSpan.Slice(valueOffset), attrs[targetAttrIndex.Value].Type)
                                        ));
                                    }
                                    else
                                    {
                                        pageValues.Add(new EntityValue(
                                            handle,
                                            DateTimeOffset.FromUnixTimeSeconds(ts).DateTime,
                                            100,
                                            entryHeader.StateType,
                                            ReadValue(dataSpan.Slice(valueOffset), entryHeader.StateType)
                                        ));
                                    }
                                }

                                offset = recordStart + 8 + BinarySpec.GetTypeSize(entryHeader.StateType);
                                for (int i = 0; i < attrs.Length; i++) offset += BinarySpec.GetTypeSize(attrs[i].Type);
                            }
                        }

                        foreach (var v in pageValues) yield return v;
                        dataPageId = nextDataPageId;
                    }
                }

                next_loop:
                if (nextEntryPageId == 0) break;
                currentPageId = nextEntryPageId;
                currentOffset = nextEntryOffset;
            }
        }
    }

    private object ReadValue(ReadOnlySpan<byte> span, VowelsType type)
    {
        return type switch
        {
            VowelsType.Double => BinaryPrimitives.ReadDoubleLittleEndian(span),
            VowelsType.Int64 => BinaryPrimitives.ReadInt64LittleEndian(span),
            VowelsType.Boolean => span[0] != 0,
            VowelsType.StringId => BinaryPrimitives.ReadUInt32LittleEndian(span),
            VowelsType.Timestamp => DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(span)).DateTime,
            VowelsType.Blob => _blobSpace.ReadBlob(MemoryMarshal.Read<BinarySpec.BlobPointer>(span)),
            _ => throw new NotSupportedException($"Type {type} is not supported for reading.")
        };
    }

    private void SwitchSchema(uint entityId, DateTime startTime, VowelsType stateType, ReadOnlySpan<BinarySpec.AttributeDefinition> attributes)
    {
        uint schemaHeadId;
        if (!_entityToSchemaHead.TryGetValue(entityId, out schemaHeadId))
        {
            schemaHeadId = _pageManager.AllocatePage(BinarySpec.PageType.SchemaChain);
            AppendToDirectory(entityId, schemaHeadId);
            _entityToSchemaHead[entityId] = schemaHeadId;
        }

        var lastLoc = FindLastSchemaEntry(schemaHeadId);

        var header = new BinarySpec.SchemaEntryHeader
        {
            StartTime = ((DateTimeOffset)startTime).ToUnixTimeSeconds(),
            StateType = stateType,
            AttrCount = (byte)attributes.Length,
            FirstDataPageId = _pageManager.AllocatePage(BinarySpec.PageType.DataChain)
        };

        var newLoc = AddSchemaEntry(schemaHeadId, header, attributes);

        if (lastLoc.PageId != 0)
        {
            var span = _pageManager.GetPageSpan(lastLoc.PageId);
            ref var prevHeader = ref MemoryMarshal.AsRef<BinarySpec.SchemaEntryHeader>(span.Slice(lastLoc.Offset));
            prevHeader.NextSchemaEntryPageId = newLoc.PageId;
            prevHeader.NextSchemaEntryOffset = newLoc.Offset;
        }
    }

    private BinarySpec.SchemaEntryHeader? GetActiveSchema(uint entityId, DateTime time)
    {
        if (!_entityToSchemaHead.TryGetValue(entityId, out uint currentPageId)) return null;
        
        long targetTime = ((DateTimeOffset)time).ToUnixTimeSeconds();
        ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();
        BinarySpec.SchemaEntryHeader? bestMatch = null;

        while (currentPageId != 0)
        {
            var span = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(span);
            
            if (pageHeader.DataOffset <= currentOffset) break;

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

    private (BinarySpec.SchemaEntryHeader Header, BinarySpec.AttributeDefinition[] Attributes)? GetActiveSchemaFull(uint entityId, DateTime time)
    {
        if (!_entityToSchemaHead.TryGetValue(entityId, out uint currentPageId)) return null;
        
        long targetTime = ((DateTimeOffset)time).ToUnixTimeSeconds();
        BinarySpec.SchemaEntryHeader? bestMatchHeader = null;
        (uint PageId, ushort Offset) bestLoc = (0, 0);
        ushort currentOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>();

        while (currentPageId != 0)
        {
            var span = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(span);
            
            if (pageHeader.DataOffset <= currentOffset) break;

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
            case VowelsType.Double: BinaryPrimitives.WriteDoubleLittleEndian(span, (double)value); break;
            case VowelsType.Int64: BinaryPrimitives.WriteInt64LittleEndian(span, (long)value); break;
            case VowelsType.Boolean: span[0] = (bool)value ? (byte)1 : (byte)0; break;
            case VowelsType.StringId: BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)value); break;
            case VowelsType.Timestamp: BinaryPrimitives.WriteInt64LittleEndian(span, ((DateTimeOffset)value).ToUnixTimeSeconds()); break;
        }
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

    private void AppendToDirectory(uint entityId, uint schemaHeadId)
    {
        var fileHeaderSpan = _pageManager.GetPageSpan(0);
        var fileHeader = MemoryMarshal.Read<BinarySpec.FileHeader>(fileHeaderSpan);
        uint currentPageId = fileHeader.DirectoryHeadPageId;
        uint lastPageId = 0;
        int recordSize = Marshal.SizeOf<BinarySpec.EntityDescriptor>();

        while (currentPageId != 0)
        {
            var pageSpan = _pageManager.GetPageSpan(currentPageId);
            var pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(pageSpan);
            
            if (pageHeader.DataOffset + recordSize <= BinarySpec.PageSize)
            {
                var descriptor = new BinarySpec.EntityDescriptor { EntityIdStringId = entityId, SchemaHeadPageId = schemaHeadId };
                MemoryMarshal.Write(pageSpan.Slice(pageHeader.DataOffset), in descriptor);
                pageHeader.DataOffset += (ushort)recordSize;
                MemoryMarshal.Write(pageSpan, in pageHeader);
                return;
            }
            lastPageId = currentPageId;
            currentPageId = pageHeader.NextPageId;
        }
        
        // If we're here, we need a new page
        uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.Directory);
        if (lastPageId != 0)
        {
            var lastSpan = _pageManager.GetPageSpan(lastPageId);
            var lastHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastSpan);
            lastHeader.NextPageId = newPageId;
            MemoryMarshal.Write(lastSpan, in lastHeader);
        }
        else
        {
            // This should not happen if InitializeFile correctly creates the first Directory page
            throw new InvalidOperationException("Directory chain head missing.");
        }

        // Add the record to the newly allocated page
        var newPageSpan = _pageManager.GetPageSpan(newPageId);
        var newPageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(newPageSpan);
        var newDescriptor = new BinarySpec.EntityDescriptor { EntityIdStringId = entityId, SchemaHeadPageId = schemaHeadId };
        MemoryMarshal.Write(newPageSpan.Slice(newPageHeader.DataOffset), in newDescriptor);
        newPageHeader.DataOffset += (ushort)recordSize;
        MemoryMarshal.Write(newPageSpan, in newPageHeader);
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
            pageHeader.NextPageId = lastPageId;
            MemoryMarshal.Write(lastPageSpan, in pageHeader);
            lastPageSpan = _pageManager.GetPageSpan(lastPageId);
            pageHeader = MemoryMarshal.Read<BinarySpec.PageHeader>(lastPageSpan);
        }

        ushort writeOffset = pageHeader.DataOffset;
        MemoryMarshal.Write(lastPageSpan.Slice(writeOffset), in header);
        
        // Fix: Write attributes as well
        int attrOffset = writeOffset + Marshal.SizeOf<BinarySpec.SchemaEntryHeader>();
        foreach (var attr in attributes)
        {
            MemoryMarshal.Write(lastPageSpan.Slice(attrOffset), in attr);
            attrOffset += Marshal.SizeOf<BinarySpec.AttributeDefinition>();
        }
        
        pageHeader.DataOffset = (ushort)attrOffset;
        MemoryMarshal.Write(lastPageSpan, in pageHeader);
        return (lastPageId, writeOffset);
    }

    public void Dispose() => _pageManager.Dispose();
}
