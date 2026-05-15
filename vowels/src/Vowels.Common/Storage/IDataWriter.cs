namespace Vowels.Common.Storage;

public interface IDataWriter : IDataReader
{
    void SaveValues(IEnumerable<EntityValue> values);
}
