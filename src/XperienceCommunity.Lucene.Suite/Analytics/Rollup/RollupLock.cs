using Microsoft.Data.SqlClient;

namespace XperienceCommunity.Lucene.Suite.Analytics.Rollup;

/// <summary>Farm-wide, non-blocking rollup lock: a session-owned sp_getapplock on a dedicated connection, released on dispose or when the connection drops.</summary>
internal sealed class RollupLock : IDisposable
{
    /// <summary>sp_getapplock resource name.</summary>
    internal const string Resource = "LuceneSuite.Rollup";

    private readonly SqlConnection connection;

    private RollupLock(SqlConnection connection) => this.connection = connection;

    /// <summary>Takes the lock, or returns null at once when another runner holds it.</summary>
    public static RollupLock? TryAcquire(string connectionString)
    {
        // Unpooled, so closing the connection ends the SQL session and with it the session-owned lock.
        var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        try
        {
            connection.Open();
            if (AnalyticsModuleInstaller.GetAppLock(connection, null, Resource, "Session", 0) >= 0)
            {
                return new RollupLock(connection);
            }
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        connection.Dispose();
        return null;
    }

    /// <summary>Releases the lock by closing its connection.</summary>
    public void Dispose() => connection.Dispose();
}
