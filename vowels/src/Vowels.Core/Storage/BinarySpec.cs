using System.Runtime.InteropServices;

namespace Vowels.Core.Storage;

public static class BinarySpec
{
    public const int PageSize = 4096;
    public const uint InvalidPageId = uint.MaxValue;
    public const uint Magic = 0x4C574F56; // 'VOWL' in little-endian

    public enum PageType : byte
    {
        Header = 0,
        StringTable = 1,
        Directory = 2,    // Was SchemaRegistry
        SchemaChain = 3,  // Was EntityData
        DataChain = 4,    // Was Metadata
        BlobSpace = 5
    }

    public enum VowelsType : byte
    {
        Double = 0x01,
        Int64 = 0x02,
        Boolean = 0x03,
        StringId = 0x04,
        Blob = 0x05,
        Timestamp = 0x06
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileHeader
    {
        public uint Magic;
        public ushort Version;
        public byte DirtyBit;
        public long CreatedAt;
        public uint DirectoryHeadPageId; // System Entity 0
        public uint StringTableHeadPageId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PageHeader
    {
        public PageType Type;
        public uint NextPageId;
        public ushort DataOffset; // Design doc 3.1 says DataOffset (2 bytes)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityDescriptor // Record in the Directory chain
    {
        public uint EntityIdStringId; // e.g. sensor.temperature
        public uint SchemaHeadPageId; // Pointer to first page of Schema chain
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SchemaEntryHeader // Variable-length record in Schema chain
    {
        public long StartTime;
        public uint FirstDataPageId;
        public VowelsType StateType;
        public byte AttrCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AttributeDefinition
    {
        public uint NameStringId;
        public VowelsType Type;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlobPointer
    {
        public uint PageId;
        public ushort Offset;
        public ushort Length;
    }

    public static int GetTypeSize(VowelsType type) => type switch
    {
        VowelsType.Double => 8,
        VowelsType.Int64 => 8,
        VowelsType.Boolean => 1,
        VowelsType.StringId => 4,
        VowelsType.Blob => 8, // Size of BlobPointer
        VowelsType.Timestamp => 8,
        _ => 0
    };
}
