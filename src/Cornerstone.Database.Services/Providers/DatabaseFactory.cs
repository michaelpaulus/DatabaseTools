using System.Data;
using System.Data.Common;
using Cornerstone.Database.Extensions;
using Cornerstone.Database.Models;
using Cornerstone.Database.Services;

namespace Cornerstone.Database.Providers;
public class DatabaseFactory : IDatabaseFactory
{

    public DatabaseFactory(IEnumerable<IDatabaseProvider> databaseProviders, IEnumerable<IConnectionCreatedNotification> connectionCreatedNotifications)
    {
        DatabaseProviders = databaseProviders;
        ConnectionCreatedNotifications = connectionCreatedNotifications;
    }

    public IEnumerable<IDatabaseProvider> DatabaseProviders { get; }
    public IEnumerable<IConnectionCreatedNotification> ConnectionCreatedNotifications { get; }

    public IDatabaseProvider GetDatabaseProvider(DbConnection connection)
    {
        return DatabaseProviders.FirstOrDefault(i => i.SupportsConnection(connection));
    }

    public IDatabaseProvider GetDatabaseProvider(ConnectionStringModel connectionString)
    {
        return DatabaseProviders.FirstOrDefault(i => i.Name.EqualsIgnoreCase(connectionString.DatabaseProviderName));
    }

    public void NotifyConnections(IDbConnection connection)
    {
        foreach (var notification in ConnectionCreatedNotifications)
        {
            notification.Notify(connection);
        }
    }

}
