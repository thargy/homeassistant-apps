namespace Vowels.Common.Storage;

public interface IDataReader : IDisposable
{
    IEnumerable<string> GetKnownEntityIds();
    IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end);
}
