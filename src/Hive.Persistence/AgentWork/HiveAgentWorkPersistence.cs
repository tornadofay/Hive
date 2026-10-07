using Hive.Agents;
using Hive.Core;

namespace Hive.Persistence;

public static class HiveAgentWorkPersistence
{
    public static RuntimeWorkProtocolStores CreateSql(
        HiveDatabaseOptions options,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var store = new SqlAgentWorkStateStore(
            options,
            clock);

        return new RuntimeWorkProtocolStores(
            store,
            store,
            store,
            store);
    }
}
