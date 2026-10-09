using System.Data;
using System.Globalization;

using CMS.DataEngine;
using CMS.FormEngine;
using CMS.Modules;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Creates or updates the Suite module and its analytics classes; safe to run on every instance at once.</summary>
internal sealed class AnalyticsModuleInstaller(
    IInfoProvider<ResourceInfo> resources,
    IInfoProvider<SettingsKeyInfo> settingsKeys,
    ILogger<AnalyticsModuleInstaller> logger)
{
    /// <summary>Code name of the Suite module (resource).</summary>
    internal const string ResourceName = "LuceneSuite";

    /// <summary>Settings key holding the installed schema version.</summary>
    internal const string VersionKeyName = "LuceneSuiteInstalledVersion";

    /// <summary>Hidden settings key holding the last fully rolled UTC day (yyyy-MM-dd), empty before the first rollup.</summary>
    internal const string WatermarkKeyName = "LuceneSuiteRollupWatermark";

    /// <summary>Schema version this build installs; bump it and add a migration step when the schema changes.</summary>
    internal const int Version = 3;

    internal const string ReceivedIndexName = "IX_LuceneSuite_SearchEvent_ReceivedUtc_SearchId";

    internal const string InteractionReceivedIndexName = "IX_LuceneSuite_SearchInteraction_ReceivedUtc_SearchId";

    internal const string DailyKeyIndexName = "IX_LuceneSuite_SearchDaily_Day_IndexName_QueryText";

    private const string LockResource = "LuceneSuite.Install";
    private const int LockTimeoutMs = 120_000;

    /// <summary>Installs under a farm-wide SQL application lock so concurrent starts cannot duplicate objects.</summary>
    public void Install()
    {
        using var connection = new SqlConnection(ConnectionHelper.ConnectionString);
        connection.Open();
        // A transaction-owned lock is released by the rollback on dispose, even when the connection returns to the pool.
        using var transaction = connection.BeginTransaction();
        AcquireLock(connection, transaction);

        var versionKey = GetVersionKey();
        int installed = ParseVersion(versionKey);
        if (installed > Version)
        {
            logger.LogWarning(
                "Lucene Suite analytics schema version {Installed} is newer than this build's version {Version}; nothing was changed.",
                installed,
                Version);
            return;
        }

        // Every step is add-only and idempotent, so fresh and older databases take the same path.
        var resource = EnsureResource();
        EnsureClass(resource, SearchEventInfo.TYPEINFO, "Lucene Suite search event", SearchEventForm());
        EnsureReceivedIndex(ReceivedIndexName, SearchEventInfo.CLASS_NAME);

        // Version 2: click and conversion events.
        EnsureClass(resource, SearchInteractionInfo.TYPEINFO, "Lucene Suite search interaction", SearchInteractionForm());
        EnsureReceivedIndex(InteractionReceivedIndexName, SearchInteractionInfo.CLASS_NAME);

        // Version 3: daily KPIs.
        EnsureClass(resource, SearchDailyInfo.TYPEINFO, "Lucene Suite search daily KPIs", SearchDailyForm());
        CreateAscendingIndex(
            DailyKeyIndexName,
            SearchDailyInfo.CLASS_NAME,
            true,
            nameof(SearchDailyInfo.Day),
            nameof(SearchDailyInfo.IndexName),
            nameof(SearchDailyInfo.QueryText));
        EnsureWatermarkKey();

        if (installed < Version)
        {
            versionKey.KeyValue = Version.ToString(CultureInfo.InvariantCulture);
            settingsKeys.Set(versionKey);
        }
    }

    internal static FormInfo SearchEventForm()
    {
        var form = FormHelper.GetBasicFormDefinition(nameof(SearchEventInfo.SearchEventID));
        Add(form, nameof(SearchEventInfo.SearchId), FieldDataType.Guid);
        Add(form, nameof(SearchEventInfo.Sequence), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.SessionId), FieldDataType.Guid, allowEmpty: true);
        Add(form, nameof(SearchEventInfo.ReceivedUtc), FieldDataType.DateTime);
        Add(form, nameof(SearchEventInfo.IndexName), FieldDataType.Text, SearchEventInfo.IndexNameMaxLength);
        // Nullable for safety; a match-all search is stored as ''.
        Add(form, nameof(SearchEventInfo.QueryText), FieldDataType.Text, SearchEventInfo.QueryTextMaxLength, allowEmpty: true);
        Add(form, nameof(SearchEventInfo.Page), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.PageSize), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.FacetFilterCount), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.ResultCount), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.LatencyMs), FieldDataType.Integer);
        Add(form, nameof(SearchEventInfo.IsSubmit), FieldDataType.Boolean);
        Add(form, nameof(SearchEventInfo.Origin), FieldDataType.Integer);
        return form;
    }

    internal static FormInfo SearchInteractionForm()
    {
        var form = FormHelper.GetBasicFormDefinition(nameof(SearchInteractionInfo.SearchInteractionID));
        Add(form, nameof(SearchInteractionInfo.SearchId), FieldDataType.Guid);
        Add(form, nameof(SearchInteractionInfo.Sequence), FieldDataType.Integer);
        Add(form, nameof(SearchInteractionInfo.ItemGuid), FieldDataType.Guid);
        Add(form, nameof(SearchInteractionInfo.Position), FieldDataType.Integer, allowEmpty: true);
        Add(form, nameof(SearchInteractionInfo.Type), FieldDataType.Integer);
        Add(form, nameof(SearchInteractionInfo.EventId), FieldDataType.Guid);
        Add(form, nameof(SearchInteractionInfo.SessionId), FieldDataType.Guid, allowEmpty: true);
        Add(form, nameof(SearchInteractionInfo.ReceivedUtc), FieldDataType.DateTime);
        return form;
    }

    internal static FormInfo SearchDailyForm()
    {
        var form = FormHelper.GetBasicFormDefinition(nameof(SearchDailyInfo.SearchDailyID));
        Add(form, nameof(SearchDailyInfo.Day), FieldDataType.Date);
        Add(form, nameof(SearchDailyInfo.IndexName), FieldDataType.Text, SearchEventInfo.IndexNameMaxLength);
        // Nullable like SearchEvent.QueryText; the browse bucket is stored as ''.
        Add(form, nameof(SearchDailyInfo.QueryText), FieldDataType.Text, SearchEventInfo.QueryTextMaxLength, allowEmpty: true);
        Add(form, nameof(SearchDailyInfo.Searches), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.ZeroResultSearches), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.SubmittedSearches), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.ClickedSearches), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.Clicks), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.ClickPositionSum), FieldDataType.LongInteger);
        Add(form, nameof(SearchDailyInfo.ConvertedSearches), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.Conversions), FieldDataType.Integer);
        Add(form, nameof(SearchDailyInfo.SessionCount), FieldDataType.Integer);
        for (int i = 0; i < SearchDailyInfo.LatencyBucketCount; i++)
        {
            Add(form, $"LatencyBucket{i}", FieldDataType.Integer);
        }

        return form;
    }

    private static void EnsureReceivedIndex(string indexName, string className) =>
        CreateAscendingIndex(indexName, className, false, nameof(SearchEventInfo.ReceivedUtc), nameof(SearchEventInfo.SearchId));

    // The tuple bool is "ascending" (undocumented; false produced a DESC index in 02-01). No-op when the index exists.
    private static void CreateAscendingIndex(string indexName, string className, bool isUnique, params string[] columns) =>
        new TableManager(null).CreateIndex(indexName, className.Replace('.', '_'), isUnique, columns.Select(column => (column, true)).ToList());

    private static void AcquireLock(SqlConnection connection, SqlTransaction transaction)
    {
        int result = GetAppLock(connection, transaction, LockResource, "Transaction", LockTimeoutMs);
        if (result < 0)
        {
            throw new TimeoutException($"sp_getapplock '{LockResource}' returned {result}.");
        }
    }

    /// <summary>Runs an exclusive sp_getapplock and returns its result; negative means the lock was not granted.</summary>
    internal static int GetAppLock(SqlConnection connection, SqlTransaction? transaction, string resource, string owner, int timeoutMs)
    {
        using var command = new SqlCommand("sp_getapplock", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = (timeoutMs / 1000) + 30,
        };
        command.Parameters.AddWithValue("@Resource", resource);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", owner);
        command.Parameters.AddWithValue("@LockTimeout", timeoutMs);
        var result = command.Parameters.Add("@Result", SqlDbType.Int);
        result.Direction = ParameterDirection.ReturnValue;
        command.ExecuteNonQuery();
        return (int)result.Value;
    }

    private ResourceInfo EnsureResource()
    {
        var resource = resources.Get().WhereEquals(nameof(ResourceInfo.ResourceName), ResourceName).FirstOrDefault() ?? new ResourceInfo();
        resource.ResourceName = ResourceName;
        resource.ResourceDisplayName = "Lucene Suite";
        resource.ResourceDescription = "Storage of XperienceCommunity.Lucene.Suite (search analytics).";
        resource.ResourceIsInDevelopment = false;
        if (resource.HasChanged)
        {
            resources.Set(resource);
        }

        return resource;
    }

    private static void EnsureClass(ResourceInfo resource, ObjectTypeInfo typeInfo, string displayName, FormInfo declared)
    {
        var dataClass = DataClassInfoProvider.GetDataClassInfo(typeInfo.ObjectClassName);
        var form = declared;
        if (dataClass is null)
        {
            dataClass = DataClassInfo.New(typeInfo.ObjectType);
        }
        else
        {
            // Add-only: columns already installed (and their data) are never dropped or retyped here.
            form = new FormInfo(dataClass.ClassFormDefinition);
            form.CombineWithForm(declared, new CombineWithFormSettings());
        }

        dataClass.ClassName = typeInfo.ObjectClassName;
        dataClass.ClassTableName = typeInfo.ObjectClassName.Replace('.', '_');
        dataClass.ClassDisplayName = displayName;
        dataClass.ClassType = ClassType.OTHER;
        dataClass.ClassResourceID = resource.ResourceID;
        dataClass.ClassFormDefinition = form.GetXmlDefinition();
        if (dataClass.HasChanged)
        {
            DataClassInfoProvider.SetDataClassInfo(dataClass);
        }
    }

    private SettingsKeyInfo GetVersionKey() =>
        settingsKeys.Get().WhereEquals(nameof(SettingsKeyInfo.KeyName), VersionKeyName).FirstOrDefault() ?? new SettingsKeyInfo
        {
            KeyName = VersionKeyName,
            KeyDisplayName = "Lucene Suite installed schema version",
            KeyType = "int",
            KeyIsCustom = true,
            KeyIsHidden = true,
        };

    private void EnsureWatermarkKey()
    {
        if (!settingsKeys.Get().WhereEquals(nameof(SettingsKeyInfo.KeyName), WatermarkKeyName).Any())
        {
            settingsKeys.Set(new SettingsKeyInfo
            {
                KeyName = WatermarkKeyName,
                KeyDisplayName = "Lucene Suite rollup watermark (last rolled UTC day)",
                KeyType = "string",
                KeyValue = string.Empty,
                KeyIsCustom = true,
                KeyIsHidden = true,
            });
        }
    }

    private static int ParseVersion(SettingsKeyInfo key) =>
        int.TryParse(key.KeyValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    private static void Add(FormInfo form, string name, string dataType, int size = 0, bool allowEmpty = false) =>
        form.AddFormItem(new FormFieldInfo
        {
            Name = name,
            DataType = dataType,
            Size = size,
            AllowEmpty = allowEmpty,
            Visible = true,
            Enabled = true,
        }, -1);
}
