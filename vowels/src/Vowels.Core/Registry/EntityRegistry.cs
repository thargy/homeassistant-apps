using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Vowels.Core.Models;
using Vowels.Core.Storage;

namespace Vowels.Core.Registry;

/// <summary>
/// Manages the mapping between Home Assistant entity IDs (strings) and Vowels internal entity IDs (uint).
/// </summary>
public class EntityRegistry
{
    private readonly EntityStore _store;
    private readonly ObservableCollection<EntityRequest> _entities = new();

    /// <summary>
    /// Gets a collection of all registered entities.
    /// </summary>
    public ReadOnlyObservableCollection<EntityRequest> Entities { get; }

    public EntityRegistry(EntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Entities = new ReadOnlyObservableCollection<EntityRequest>(_entities);
    }

    /// <summary>
    /// Registers interest in a Home Assistant entity.
    /// </summary>
    /// <param name="entityId">The Home Assistant entity ID (e.g., 'sensor.temperature').</param>
    public void RegisterEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId)) return;
        
        if (!_entities.Any(e => e.EntityId == entityId))
        {
            _entities.Add(new EntityRequest(entityId));
            EnsureStorageRegistration(entityId);
        }
    }

    private void EnsureStorageRegistration(string entityId)
    {
        uint stringId = _store.GetOrAddString(entityId);
        _store.GetOrCreateSchemaChain(stringId);
    }

    /// <summary>
    /// Checks if an entity is registered.
    /// </summary>
    public bool IsEntityRegistered(string entityId)
    {
        return _entities.Any(e => e.EntityId == entityId);
    }
}
