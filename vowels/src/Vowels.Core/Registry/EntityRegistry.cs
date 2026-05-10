using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Vowels.Core.Common;

namespace Vowels.Core.Registry;

/// <summary>
/// Manages the mapping between Home Assistant entity IDs (strings) and Vowels internal handles.
/// </summary>
public class EntityRegistry : IEntityRegistry
{
    private static EntityRegistry? _instance;
    public static EntityRegistry Instance => _instance ?? throw new InvalidOperationException("EntityRegistry must be initialized with an IEntityStore first.");

    public static void Initialize(IEntityStore store)
    {
        _instance = new EntityRegistry(store);
    }

    /// <summary>
    /// Resets the singleton instance for testing purposes.
    /// </summary>
    public static void ResetForTesting()
    {
        _instance = null;
    }

    private readonly IEntityStore _store;
    private readonly Subject<EntityValue> _liveValues = new();
    private readonly Dictionary<string, IHandle> _handleCache = new();
    public IEnumerable<IHandle> Entities => _handleCache.Values;

    private EntityRegistry(IEntityStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IObservable<EntityValue> GetEntityValues(IObservable<IEntityRequest> requests, DateTime start, DateTime end)
    {
        return requests.SelectMany(request =>
        {
            // 1. Resolve handles for this request (could be Regex, ID, etc.)
            // We ask the store to find historical handles, and we also check our live cache.
            var historicalHandles = _store.DiscoverHistoricalHandles(request);
            
            // For now, let's simplify and resolve concrete handles
            return historicalHandles.ToList().SelectMany(handles =>
            {
                var historicalData = _store.GetValues(handles, start, end < DateTime.Now ? end : DateTime.Now);
                
                IObservable<EntityValue> liveData = Observable.Empty<EntityValue>();
                if (end > DateTime.Now)
                {
                    // Filter live values that match the handles
                    liveData = _liveValues.Where(v => handles.Any(h => h.EntityId == v.Handle.EntityId));
                    
                    // Limit live data to the end time
                    var duration = end - DateTime.Now;
                    if (duration > TimeSpan.Zero)
                    {
                        liveData = liveData.TakeUntil(Observable.Timer(duration));
                    }
                }
                
                return historicalData.Concat(liveData);
            });
        });
    }

    public IObservable<EntityValue> SetEntityValues(IObservable<EntityValue> values)
    {
        // 1. Persist to store
        var saved = _store.SaveValues(values);
        
        // 2. Broadcast to live listeners
        return saved.Do(v => _liveValues.OnNext(v));
    }

    /// <summary>
    /// Registers interest in a Home Assistant entity.
    /// </summary>
    /// <param name="entityId">The Home Assistant entity ID (e.g., 'sensor.temperature').</param>
    public void RegisterEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId)) return;
        
        if (!_handleCache.ContainsKey(entityId))
        {
            // Defaulting to SensorHandle for now, but in reality we might want to discover the type
            var handle = new SensorHandle(entityId);
            _handleCache[entityId] = handle;
        }
    }

    public bool IsEntityRegistered(string entityId)
    {
        return _handleCache.ContainsKey(entityId);
    }
}
