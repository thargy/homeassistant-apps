using Xunit;
using Vowels.Core.Registry;
using Vowels.Common;
using System.IO;
using System.Reactive.Linq;

namespace Vowels.Core.Tests;

/// <summary>
/// Minimal IEntityStore stub for unit tests that only test EntityRegistry logic.
/// </summary>
internal class NullEntityStore : IEntityStore
{
    public IObservable<EntityValue> GetValues(IEnumerable<IHandle> handles, DateTime start, DateTime end)
        => Observable.Empty<EntityValue>();

    public IObservable<EntityValue> SaveValues(IObservable<EntityValue> values)
        => Observable.Empty<EntityValue>();

    public IObservable<IHandle> DiscoverHistoricalHandles(IEntityRequest request)
        => Observable.Empty<IHandle>();
}

public class EntityRegistryTests
{
    [Fact]
    public void RegisterEntity_ShouldCreateNewEntityId()
    {
        // Arrange
        EntityRegistry.ResetForTesting();

        var store = new NullEntityStore();
        EntityRegistry.Initialize(store);
        
        var registry = EntityRegistry.Instance;

        // Act
        registry.RegisterEntity("sensor.living_room_temperature");

        // Assert
        Assert.True(registry.IsEntityRegistered("sensor.living_room_temperature"));
        Assert.Contains(registry.Entities, e => e.EntityId == "sensor.living_room_temperature");
    }
}
