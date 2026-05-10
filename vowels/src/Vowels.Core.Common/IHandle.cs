namespace Vowels.Core.Common;

public interface IHandle : IEntityRequest
{
    string EntityId { get; }
}

public record SensorHandle(string EntityId) : IHandle;

public record BinarySensorHandle(string EntityId) : IHandle;

public record SensorAttributeHandle(string EntityId, string AttributeName) : IHandle;
