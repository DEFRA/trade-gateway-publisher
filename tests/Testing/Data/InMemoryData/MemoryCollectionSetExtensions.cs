using Infrastructure.Data;
using Infrastructure.Data.Entities;

namespace Testing.Data.InMemoryData;

public static class MemoryCollectionSetExtensions
{
    public static void AddTestData<T>(this IMongoCollectionSet<T> set, T item)
        where T : IDataEntity
    {
        if (set is MemoryCollectionSet<T> memoryCollectionSet)
        {
            memoryCollectionSet.AddTestData(item);
        }
    }

    public static void AddTestData<T>(this IMongoCollectionSet<T> set, List<T> items)
        where T : IDataEntity
    {
        if (set is MemoryCollectionSet<T> memoryCollectionSet)
        {
            foreach (var item in items)
                memoryCollectionSet.AddTestData(item);
        }
    }
}
