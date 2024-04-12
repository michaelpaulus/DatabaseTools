using System.Data;
using System.Data.Common;
using System.Text.Json;
using Cornerstone.Database.Extensions;
using Cornerstone.Database.Models;
using Cornerstone.Database.Providers;
using Cornerstone.DependencyInjection;

namespace Cornerstone.Database.Services;
[ExportTransient(typeof(IDatabaseStructureService))]
public class DatabaseStructureService : IDatabaseStructureService
{

    private readonly IDatabaseFactory _databaseFactory;

    public DatabaseStructureService(IDatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    #region Helper Methods

    private bool ContainsTable(IEnumerable<string> tables, string table)
    {
        return (from i in tables where i.EqualsIgnoreCase(table) select i).Any();
    }

    public static string GetStringValue(DataRow row, string columnName)
    {
        string value = null;

        try
        {
            if (row.Table.Columns.Contains(columnName) && !(row.IsNull(columnName)))
            {
                value = Convert.ToString(row[columnName]);
            }
        }
        catch
        {

        }

        return value;
    }

    public static int GetInt32Value(DataRow row, string columnName)
    {
        int value = 0;

        try
        {
            var strValue = GetStringValue(row, columnName);
            if (!(string.IsNullOrEmpty(strValue)))
            {
                int.TryParse(strValue, out value);
            }
        }
        catch
        {

        }

        return value;
    }

    public static bool GetBoolValue(DataRow row, string columnName)
    {
        bool value = false;

        try
        {
            var strValue = GetStringValue(row, columnName);

            if (!(string.IsNullOrEmpty(strValue)))
            {
                if (strValue.EqualsIgnoreCase("Yes") || strValue.EqualsIgnoreCase("1"))
                {
                    strValue = "True";
                }
                else if (strValue.EqualsIgnoreCase("No") || strValue.EqualsIgnoreCase("0"))
                {
                    strValue = "False";
                }
                bool.TryParse(strValue, out value);
            }
        }
        catch
        {

        }

        return value;
    }

    #endregion

    #region Database Structure

    private IList<Models.ColumnModel> GetColumns(IDatabaseProvider databaseProvider, DataTable dataTable)
    {
        IList<Models.ColumnModel> list = new List<Models.ColumnModel>();
        foreach (System.Data.DataRow row in dataTable.Rows)
        {
            Models.ColumnModel column = new Models.ColumnModel();
            InitializeColumn(databaseProvider, column, row);
            list.Add(column);
        }
        //the database sometimes skips column numbers, we don't really care about that here,
        //  we need reproducable numbers over all db's in order
        foreach (var tableGroup in list.GroupBy(i => new { i.SchemaName, i.TableName }))
        {
            var index = 1;
            foreach (var column in tableGroup.OrderBy(i => i.ColumnID).ToList())
            {
                column.ColumnID = index;
                index += 1;
            }
        }

        return list;
    }

    public IList<Models.ColumnModel> GetTableColumns(DbConnection connection)
    {
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        IList<Models.ColumnModel> list = new List<Models.ColumnModel>();
        var dataTable = provider.GetTableColumns(connection);
        if (dataTable != null)
        {
            list = GetColumns(provider, dataTable);
        }

        return list;
    }

    public IList<Models.ColumnModel> GetViewColumns(DbConnection connection)
    {
        IList<Models.ColumnModel> list = new List<Models.ColumnModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        var dataTable = provider.GetViewColumns(connection);

        if (dataTable != null)
        {
            list = GetColumns(provider, dataTable);
        }

        return list;
    }

    private void InitializeColumn(IDatabaseProvider databaseProvider, Models.ColumnModel column, DataRow row)
    {

        column.TableName = GetStringValue(row, "table_name");
        column.SchemaName = GetStringValue(row, "schema_name");
        column.ColumnName = GetStringValue(row, "column_name");
        column.Precision = GetInt32Value(row, "precision");
        column.Scale = GetInt32Value(row, "scale");
        column.ColumnType = GetStringValue(row, "column_type");
        column.IsNullable = GetBoolValue(row, "is_nullable");
        column.IsIdentity = GetBoolValue(row, "is_identity");
        column.IsComputed = GetBoolValue(row, "is_computed");
        column.IsHidden = GetBoolValue(row, "is_hidden");
        column.GeneratedAlwaysType = GetInt32Value(row, "generated_always_type");
        column.ComputedDefinition = GetStringValue(row, "computed_definition");
        column.ColumnID = GetInt32Value(row, "column_id");
        column.IsPrimaryKey = GetBoolValue(row, "is_primary_key");
        column.ColumnDefault = GetStringValue(row, "column_default");
        var extendedProperites = GetStringValue(row, "extended_properties");
        if (!string.IsNullOrEmpty(extendedProperites))
        {
            foreach (var item in JsonSerializer.Deserialize<List<ExtendedProperty>>(extendedProperites, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }))
            {
                column.ExtendedProperties.Add(item.Name, item.Value);
            }
        }

        var targetColumnType = databaseProvider.GetColumnType(new Models.ColumnTypeModel() { ColumnType = column.ColumnType, Precision = column.Precision, Scale = column.Scale });
        if (targetColumnType != null)
        {
            column.ColumnType = targetColumnType.ColumnType;
            column.Precision = targetColumnType.Precision.GetValueOrDefault();
            column.Scale = targetColumnType.Scale.GetValueOrDefault();
        }

    }

    public IEnumerable<Models.DefinitionModel> GetDefinitions(DbConnection connection)
    {
        List<Models.DefinitionModel> list = new List<Models.DefinitionModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        var dtDefinitions = provider.GetDefinitions(connection);

        if (dtDefinitions != null)
        {
            foreach (var row in dtDefinitions.Rows.OfType<System.Data.DataRow>())
            {
                var model = new Models.DefinitionModel
                {
                    Definition = row["definition"].ToString(),
                    DefinitionName = row["name"].ToString(),
                    SchemaName = row["schema_name"].ToString(),
                    XType = row["xtype"].ToString().Trim()
                };
                list.Add(model);
            }

        }

        list = list.OrderBy(i => i.XType).ThenBy(i => i.DefinitionName).ToList();

        return list;
    }
    public IEnumerable<Models.SecurityPolicyModel> GetSecurityPolicies(DbConnection connection)
    {
        var list = new List<Models.SecurityPolicyModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);
        var dtDefinitions = provider.GetSecurityPolicies(connection);

        var dtDependencies = provider.GetDefinitionDependencies(connection);

        if (dtDefinitions != null)
        {
            list = (
                from i in dtDefinitions.Rows.OfType<System.Data.DataRow>()
                group new Models.SecurityPolicyPredicate { TargetSchema = i["TargetSchema"].ToString(), Operation = i["Operation"].ToString(), PredicateDefinition = i["PredicateDefinition"].ToString(), PredicateType = i["PredicateType"].ToString(), TargetName = i["TargetName"].ToString() }
                by new { PolicySchema = i["PolicySchema"].ToString(), PolicyName = i["PolicyName"].ToString(), IsEnabled = (bool)i["IsEnabled"], IsSchemaBound = (bool)i["IsSchemaBound"] } into g
                select new Models.SecurityPolicyModel
                {
                    IsEnabled = g.Key.IsEnabled,
                    PolicyName = g.Key.PolicyName,
                    PolicySchema = g.Key.PolicySchema,
                    Predicates = g.ToList(),
                    IsSchemaBound = g.Key.IsSchemaBound
                }
                ).ToList();
        }
        return list;
    }

    public IEnumerable<Models.ForeignKeyModel> GetForeignKeys(DbConnection connection, IEnumerable<string> tables)
    {
        IList<Models.ForeignKeyModel> list = new List<Models.ForeignKeyModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        DataTable dataTable = provider.GetForeignKeys(connection);

        if (dataTable != null)
        {
            foreach (var tableGroup in (
           from i in dataTable.Rows.Cast<System.Data.DataRow>()
           group i by new
           {
               SchemaName = i["schema_name"].ToString(),
               TableName = i["table_name"].ToString(),
               ForeignKeyName = i["foreign_key_name"].ToString()
           } into g
           select new
           {
               TableName = g.Key.TableName,
               SchemaName = g.Key.SchemaName,
               ForeignKeyName = g.Key.ForeignKeyName,
               Items = g.ToList()
           }))
            {
                if (ContainsTable(tables, tableGroup.TableName))
                {
                    System.Data.DataRow summaryRow = tableGroup.Items[0];

                    Models.ForeignKeyModel foreignKey = new Models.ForeignKeyModel
                    {
                        ForeignKeyName = tableGroup.ForeignKeyName,
                        TableName = tableGroup.TableName,
                        SchemaName = tableGroup.SchemaName,
                        ReferencedSchemaName = summaryRow["referenced_schema_name"].ToString(),
                        ReferencedTableName = summaryRow["referenced_table_name"].ToString(),
                        IsNotForReplication = Convert.ToBoolean(summaryRow["is_not_for_replication"]),
                        DeleteAction = summaryRow["delete_action"].ToString(),
                        UpdateAction = summaryRow["update_action"].ToString()
                    };

                    list.Add(foreignKey);

                    foreach (System.Data.DataRow detailRow in tableGroup.Items)
                    {
                        foreignKey.Detail.Add(new Models.ForeignKeyDetailModel
                        {
                            Column = detailRow["column_name"].ToString(),
                            ReferencedColumn = detailRow["referenced_column_name"].ToString()
                        });
                    }
                }
            }
        }

        return list;
    }

    public IEnumerable<Models.CheckConstraintModel> GetCheckConstraints(DbConnection connection, IEnumerable<string> tables)
    {
        var list = new List<Models.CheckConstraintModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        var dataTable = provider.GetCheckConstraints(connection);

        if (dataTable != null)
        {
            foreach (System.Data.DataRow detailRow in dataTable.Rows)
            {
                var strTableName = detailRow["table_name"].ToString();
                var strSchemaName = detailRow["schema_name"].ToString();
                var strConstraintName = detailRow["check_constraint_name"].ToString();
                var strDefinition = detailRow["check_constraint_definition"].ToString();

                if (ContainsTable(tables, strTableName))
                {
                    list.Add(new Models.CheckConstraintModel
                    {
                        CheckConstraintName = strConstraintName,
                        TableName = strTableName,
                        SchemaName = strSchemaName,
                        CheckConstraintDefinition = strDefinition
                    });
                }
            }
        }

        return list;
    }

    private class IndexBucket
    {
        public string TableName { get; set; }
        public string IndexName { get; set; }
        public string SchemaName { get; set; }
        public int BucketCount { get; set; }
    }

    public IEnumerable<Models.IndexModel> GetIndexes(DbConnection connection, IEnumerable<string> tables, bool? isPrimaryKey = null)
    {
        IList<Models.IndexModel> list = new List<Models.IndexModel>();
        System.Data.DataTable dtIndexes = null;
        var provider = _databaseFactory.GetDatabaseProvider(connection);
        dtIndexes = provider.GetIndexes(connection);

        if (dtIndexes != null)
        {

            var indexBucketCounts = new List<IndexBucket>();

            var dtIndexBucketCounts = provider.GetIndexeBucketCounts(connection);
            if (dtIndexBucketCounts != null)
            {
                indexBucketCounts = (from i in dtIndexBucketCounts.Rows.OfType<System.Data.DataRow>()
                                     select new IndexBucket
                                     {
                                         TableName = i["table_name"].ToString(),
                                         IndexName = i["index_name"].ToString(),
                                         SchemaName = i["schema_name"].ToString(),
                                         BucketCount = Convert.ToInt32(i["total_bucket_count"])
                                     }).ToList();
            }

            foreach (var indexGroup in (
                from i in dtIndexes.Rows.Cast<System.Data.DataRow>()
                group i by new { IndexName = i["index_name"].ToString(), TableName = i["table_name"].ToString(), SchemaName = i["schema_name"].ToString() } into g
                select new { IndexName = g.Key.IndexName, TableName = g.Key.TableName, SchemaName = g.Key.SchemaName, Items = g.ToList() }))
            {
                if (ContainsTable(tables, indexGroup.TableName))
                {
                    System.Data.DataRow summaryRow = indexGroup.Items[0];

                    Models.IndexModel index = new Models.IndexModel
                    {
                        TableName = indexGroup.TableName,
                        IndexName = indexGroup.IndexName,
                        SchemaName = indexGroup.SchemaName,
                        PartitionSchemeName = summaryRow["partition_scheme_name"] == DBNull.Value ? "" : summaryRow["partition_scheme_name"].ToString(),
                        DataCompressionDesc = summaryRow["data_compression_desc"] == DBNull.Value ? "" : summaryRow["data_compression_desc"].ToString(),
                        IndexType = summaryRow["index_type"].ToString(),
                        FilterDefinition = summaryRow["filter_definition"] == DBNull.Value ? "" : summaryRow["filter_definition"].ToString(),
                        IsUnique = Convert.ToBoolean(summaryRow["is_unique"]),
                        FillFactor = Convert.ToInt32(summaryRow["fill_factor"]),
                        IsPrimaryKey = Convert.ToBoolean(summaryRow["is_primary_key"])
                    };

                    var indexBucketCount = (from i in indexBucketCounts
                                            where i.SchemaName == index.SchemaName &&
                                           i.TableName == index.TableName &&
                                           i.IndexName == index.IndexName
                                            select i).FirstOrDefault();

                    if (indexBucketCount != null)
                    {
                        index.TotalBucketCount = indexBucketCount.BucketCount;
                    }

                    foreach (var detialRow in indexGroup.Items.OrderBy(i => Convert.ToInt32(i["key_ordinal"])))
                    {
                        bool blnIsDescending = Convert.ToBoolean(detialRow["is_descending_key"]);
                        bool blnIsIncludeColumn = Convert.ToBoolean(detialRow["is_included_column"]);
                        string strColumnName = detialRow["column_name"].ToString();

                        var columnModel = new Models.IndexColumnModel { ColumnName = strColumnName, IsDescending = blnIsDescending, PartitionOrdinal = Convert.ToInt32(detialRow["partition_ordinal"]) };

                        if (blnIsIncludeColumn)
                        {
                            index.IncludeColumns.Add(columnModel);
                        }
                        else
                        {
                            index.Columns.Add(columnModel);
                        }
                    }

                    if (!isPrimaryKey.HasValue || index.IsPrimaryKey == isPrimaryKey)
                    {
                        list.Add(index);
                    }

                }
            }
        }

        return list;
    }

    private IEnumerable<Models.TableModel> GetTables(DataTable dataTable, IEnumerable<Models.ColumnModel> columns)
    {
        List<Models.TableModel> list = new List<Models.TableModel>();

        List<string> tables = new List<string>();

        Dictionary<string, IList<Models.ColumnModel>> columnIndex = new Dictionary<string, IList<Models.ColumnModel>>();

        foreach (var columnGroup in (
            from i in columns
            group i by new { i.TableName, i.SchemaName } into g
            select new { TableName = g.Key.TableName, SchemaName = g.Key.SchemaName, Items = g.ToList() }))
        {
            columnIndex.Add($"{columnGroup.SchemaName}.{columnGroup.TableName}".ToUpper(), columnGroup.Items);
        }

        foreach (System.Data.DataRow row in dataTable.Rows)
        {
            Models.TableModel table = new Models.TableModel();
            table.TableName = GetStringValue(row, "table_name");
            table.SchemaName = GetStringValue(row, "schema_name");
            table.TemporalType = GetInt32Value(row, "temporal_type");
            table.HistoryTableName = GetStringValue(row, "history_table_name");
            table.IsMemoryOptimized = GetBoolValue(row, "is_memory_optimized");
            table.DurabilityDesc = GetStringValue(row, "durability_desc");
            table.IsExternal = GetBoolValue(row, "is_external");
            table.DataSourceName = GetStringValue(row, "data_source_name");
            table.PartitionSchemeColumns = GetStringValue(row, "partition_scheme_columns");
            table.PartitionSchemeName = GetStringValue(row, "partition_scheme_name");
            var extendedProperites = GetStringValue(row, "extended_properties");
            if (!string.IsNullOrEmpty(extendedProperites))
            {
                try
                {
                    var props = JsonSerializer.Deserialize<List<ExtendedProperty>>(extendedProperites,
                        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                    foreach (var item in props)
                    {
                        table.ExtendedProperties.Add(item.Name, item.Value);
                    }
                }
                catch
                {

                }
            }

            var tableKey = $"{table.SchemaName}.{table.TableName}".ToUpper();

            if (!(tables.Contains(tableKey)))
            {
                if (columnIndex.ContainsKey(tableKey))
                {
                    var tableColumns = columnIndex[tableKey];
                    foreach (var column in (
                                from i in tableColumns
                                where i.TableName.EqualsIgnoreCase(table.TableName)
                                select i)
                                )
                    {
                        table.Columns.Add(column);
                    }
                }
                tables.Add(tableKey);
                list.Add(table);
            }
        }

        return list;
    }

    public IEnumerable<Models.TableModel> GetTables(DbConnection connection, IEnumerable<Models.ColumnModel> columns, bool withBackup = false)
    {
        System.Data.DataTable dataTable = null;
        var provider = _databaseFactory.GetDatabaseProvider(connection);

        dataTable = provider.GetTables(connection);

        IList<Models.TableModel> list = new List<Models.TableModel>();

        if (dataTable != null)
        {
            list = GetTables(dataTable, columns).ToList();

            //Remove PowerBuilder Tables
            list = (
                from i in list
                where !(i.TableName.StartsWith("pbcat", StringComparison.InvariantCultureIgnoreCase))
                select i).ToList();

            //Remove Access Sys Tables
            list = (
                from i in list
                where !(i.TableName.StartsWith("MSys", StringComparison.InvariantCultureIgnoreCase))
                select i).ToList();

            //Remove Access Sys Tables
            list = (
                from i in list
                where !(i.TableName.StartsWith("ISYS", StringComparison.InvariantCultureIgnoreCase))
                select i).ToList();

            if (!withBackup)
            {
                list = (
                    from i in list
                    where !(i.TableName.EndsWith("_backup", StringComparison.InvariantCultureIgnoreCase)) && !(i.TableName.EndsWith("_old", StringComparison.InvariantCultureIgnoreCase))
                    select i).ToList();
            }
        }

        return list;
    }

    public IEnumerable<Models.TableModel> GetViews(DbConnection connection, IEnumerable<Models.ColumnModel> columns)
    {
        IList<Models.TableModel> list = new List<Models.TableModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);
        System.Data.DataTable dataTable = provider.GetViews(connection);
        if (dataTable != null)
        {
            list = GetTables(dataTable, columns).ToList();
            foreach (var item in list)
            {
                item.IsView = true;
            }
        }

        return list;
    }

    public IEnumerable<Models.TriggerModel> GetTriggers(DbConnection connection, IEnumerable<string> tables, IEnumerable<string> views, string objectFilter)
    {
        IList<Models.TriggerModel> list = new List<Models.TriggerModel>();
        var provider = _databaseFactory.GetDatabaseProvider(connection);
        var dataTable = provider.GetTriggers(connection);

        if (dataTable != null)
        {
            foreach (System.Data.DataRow detailRow in dataTable.Rows)
            {
                string strTableName = detailRow["table_name"].ToString();
                string strTriggerName = detailRow["trigger_name"].ToString();
                string strDefinition = detailRow["definition"].ToString();

                if (ContainsTable(tables, strTableName) ||
                    ContainsTable(views, strTableName) ||
                    (!(string.IsNullOrEmpty(objectFilter)) && strTriggerName.ToLower().Contains(objectFilter)))
                {
                    list.Add(new Models.TriggerModel
                    {
                        TableName = strTableName,
                        TriggerName = strTriggerName,
                        SchemaName = detailRow["schema_name"].ToString(),
                        Definition = strDefinition
                    });
                }
            }
        }

        return list;
    }

    #endregion

}
