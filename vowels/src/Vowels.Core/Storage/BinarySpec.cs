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
        SchemaRegistry = 2,
        EntityData = 3,
        Metadata = 4
    }

    public enum HaType : byte
    {
        Double = 0x01,
        Int32 = 0x02,
        Boolean = 0x03,
        StringId = 0x04,
        Blob = 0x05
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileHeader
    {
        public uint Magic;
        public ushort Version;
        public byte DirtyBit;
        public long CreatedAt;
        public uint ReservedPagesForNextHour;
        public uint StringTablePageId;
        public uint SchemaRegistryPageId;
        public uint EntityCount; // Number of entries in the directory
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PageHeader
    {
        public PageType Type;
        public uint NextPageId;
        public ushort DataOffset; // Design doc 3.1 says DataOffset (2 bytes)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityPageHeader
    {
        public uint EntityStringId; // Interned ID from StringTable
        public ushort SchemaId;      // Interned ID from SchemaRegistry (2 bytes per spec)
        public uint PreviousEntityPageId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityDirectoryEntry
    {
        public uint EntityStringId;
        public uint FirstPageId;
        public uint LastPageId;
        public ushort CurrentSchemaId;
        public uint ReservedPages; // Self-tuning hint
    }

    public static int GetHaTypeSize(HaType type) => type switch
    {
        HaType.Double => 8,
        HaType.Int32 => 4,
        HaType.Boolean => 1,
        HaType.StringId => 4,
        HaType.Blob => 8,
        _ => 0
    };
}
