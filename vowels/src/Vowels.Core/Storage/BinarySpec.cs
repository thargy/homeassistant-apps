using System.Runtime.InteropServices;
using Vowels.Core.Common;

namespace Vowels.Core.Storage;

/// <summary>
/// Defines the binary layout and constants for the Vowels storage format.
/// </summary>
public static class BinarySpec
{
    public const int PageSize = 4096;
    public const uint InvalidPageId = uint.MaxValue;
    public const uint Magic = 0x4C574F56; // 'VOWL' in little-endian

    public enum PageType : byte
    {
        Header = 0,
        StringTable = 1,
        Directory = 2,
        SchemaChain = 3,
        DataChain = 4,
        BlobSpace = 5
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileHeader
    {
        public uint Magic;
        public ushort Version;
        public byte DirtyBit;
        public long CreatedAt;
        public uint DirectoryHeadPageId;
        public uint StringTableHeadPageId;
        public uint BlobSpaceHeadPageId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PageHeader
    {
        public PageType Type;
        public uint NextPageId;
        public ushort DataOffset;
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
        public uint NextSchemaEntryPageId;   // Explicit link to next version
        public ushort NextSchemaEntryOffset; // Explicit link to next version
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
