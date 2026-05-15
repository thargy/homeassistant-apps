namespace Vowels.Common;

public enum VowelsType : byte
{
    Double = 0x01,
    Int64 = 0x02,
    Boolean = 0x03,
    StringId = 0x04,
    Blob = 0x05,
    Timestamp = 0x06
}

public record EntityValue(
    IHandle Handle,
    DateTime Timestamp,
    byte Confidence,
    VowelsType Type,
    object Value
);
