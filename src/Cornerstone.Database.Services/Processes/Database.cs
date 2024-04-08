﻿
using Cornerstone.Database.Extensions;
using Cornerstone.Database.Models;
using Cornerstone.Database.Providers;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;

namespace Cornerstone.Database
{
    namespace Processes
    {
        public class Database
        {

            public const int DefaultCommandTimeout = 0;

            private readonly IDatabaseProvider _databaseProvider;
            private readonly IEnumerable<IConnectionCreatedNotification> _connectionCreatedNotifications;

            public Database(IDatabaseProvider databaseProvider, IEnumerable<IConnectionCreatedNotification> connectionCreatedNotifications)
            {
                _databaseProvider = databaseProvider;
                _connectionCreatedNotifications = connectionCreatedNotifications;
            }

            public Database(DatabaseType databaseType, IEnumerable<IDatabaseProvider> databaseProviders, IEnumerable<IConnectionCreatedNotification> connectionCreatedNotifications)
            {
                _databaseProvider = GetDatabaseProvider(databaseProviders, databaseType);
                _connectionCreatedNotifications = connectionCreatedNotifications;
            }

            public IDatabaseProvider Provider => _databaseProvider;

            #region Create Database Methods

            public DbConnection CloneDbConnection(DbConnection dbConnection)
            {
                DbConnection connection = _databaseProvider.CreateProvider().CreateConnection();
                connection.ConnectionString = dbConnection.ConnectionString;
                NotifyConnections(connection);
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }
                return connection;
            }

            public DbCommand CreateDbCommand(DbConnection dbConnection)
            {
                DbCommand command = dbConnection.CreateCommand();
                command.Connection = dbConnection;

                command.CommandTimeout = DefaultCommandTimeout;

                if (Data.DbTransactionScope.Current != null && Data.DbTransactionScope.Current.Connection == dbConnection)
                {
                    command.Transaction = Data.DbTransactionScope.Current;
                }

                return command;
            }

            public DbConnection CreateDbConnection(System.Configuration.ConnectionStringSettings connectionString)
            {
                return CreateDbConnection(_databaseProvider.CreateProvider(), connectionString);
            }

            public DbConnection CreateDbConnection(DbProviderFactory dbProviderFactory, System.Configuration.ConnectionStringSettings connectionString)
            {
                DbConnection connection = dbProviderFactory.CreateConnection();

                connection.ConnectionString = _databaseProvider.TransformConnectionString(connection.ConnectionString);

                connection.ConnectionString = connectionString.ConnectionString;

                NotifyConnections(connection);
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                return connection;
            }

            #endregion

            #region Execute Database Methods


            public System.Data.DataSet Execute(DbConnection connection, string sqlCommand)
            {
                var ds = new System.Data.DataSet();

                using (var command = CreateDbCommand(connection))
                {
                    command.CommandText = sqlCommand;
                    using (DbDataReader reader = command.ExecuteReader())
                    {
                        ds.Load(reader, LoadOption.OverwriteChanges, "Table");
                    }
                }

                return ds;
            }

            public System.Data.DataSet Execute(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                System.Data.DataSet ds = new System.Data.DataSet();

                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    var factory = _databaseProvider.CreateProvider();
                    using (var connection = CreateDbConnection(factory, connectionString))
                    {
                        ds = Execute(connection, sqlCommand);
                    }
                }

                return ds;
            }

            public void ExecuteFile(DbConnection connection, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Text.RegularExpressions.Regex regEx = new System.Text.RegularExpressions.Regex("^[\\s]*GO[^a-zA-Z0-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                    foreach (string commandText in regEx.Split(sqlCommand))
                    {
                        DbConnection commandConnection = connection;

                        if (commandText.IndexOf("ALTER DATABASE", StringComparison.InvariantCultureIgnoreCase) != -1 && Data.DbTransactionScope.Current != null && Data.DbTransactionScope.Current.Connection == connection)
                        {

                            //Cannot Alter Database inside Transaction
                            commandConnection = CloneDbConnection(connection);
                        }

                        if (!(string.IsNullOrEmpty(commandText.Trim())))
                        {
                            ExecuteNonQuery(commandConnection, commandText);
                        }
                    }
                }
            }

            public void ExecuteFile(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    var factory = _databaseProvider.CreateProvider();
                    using (DbConnection connection = CreateDbConnection(factory, connectionString))
                    {
                        ExecuteFile(connection, sqlCommand);
                    }
                }
            }

            public void ExecuteNonQuery(DbConnection connection, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    using (DbCommand command = CreateDbCommand(connection))
                    {
                        command.CommandText = sqlCommand;
                        command.ExecuteNonQuery();
                    }
                }
            }

            public void ExecuteNonQuery(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    var factory = _databaseProvider.CreateProvider();
                    using (DbConnection connection = CreateDbConnection(factory, connectionString))
                    {
                        ExecuteNonQuery(connection, sqlCommand);
                    }
                }
            }

            public object ExecuteScalar(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                object returnValue = null;
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    var factory = _databaseProvider.CreateProvider();

                    using (DbConnection connection = CreateDbConnection(factory, connectionString))
                    {
                        using (DbCommand command = CreateDbCommand(connection))
                        {
                            command.CommandText = sqlCommand;

                            returnValue = command.ExecuteScalar();

                            if (returnValue == DBNull.Value)
                            {
                                returnValue = null;
                            }
                        }
                    }
                }
                return returnValue;
            }

            #endregion

            #region Helper Methods

            private void NotifyConnections(IDbConnection connection)
            {
                foreach (var notification in _connectionCreatedNotifications)
                {
                    notification.Notify(connection);
                }
            }

            private bool ContainsTable(IEnumerable<string> tables, string table)
            {
                return (from i in tables where i.EqualsIgnoreCase(table) select i).Any();
            }


            public static IDatabaseProvider GetDatabaseProvider(IEnumerable<IDatabaseProvider> providers, DatabaseType databaseType, bool throwOnNotFound = false)
            {
                var provider = (from i in providers where i.ForDatabaseType == databaseType select i).FirstOrDefault();
                if (throwOnNotFound &&
                    provider == null)
                {
                    throw new ArgumentOutOfRangeException("databaseType", $"DatabaseType '{databaseType.ToString()}' has no provider.");
                }
                return provider;
            }

            public static System.Configuration.ConnectionStringSettings GetConnectionStringSetting(string connectionStringName)
            {
                return Configuration.ConnectionInfo.GetConnectionStringSetting(connectionStringName);
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

            public static Models.DatabaseType GetDatabaseType(DbConnection connection)
            {
                if (connection.GetType().FullName.StartsWith("System.Data.Odbc.OdbcConnection"))
                {
                    return Models.DatabaseType.Odbc;
                }
                else if (connection.GetType().FullName.StartsWith("System.Data.Oracle", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Models.DatabaseType.Oracle;
                }
                else if (connection.GetType().FullName.StartsWith("System.Data.SqlServerCe", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Models.DatabaseType.MicrosoftSQLServerCompact;
                }
                else if (connection.GetType().FullName.StartsWith("System.Data.Ole", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (connection.ConnectionString.Contains("Microsoft.ACE"))
                    {
                        return Models.DatabaseType.AccessOLE;
                    }
                    return Models.DatabaseType.OLE;
                }
                else if (connection.GetType().FullName.StartsWith("MySql", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Models.DatabaseType.MySql;
                }
                return Models.DatabaseType.MicrosoftSQLServer;
            }

            public static Models.DatabaseType GetDatabaseType(System.Configuration.ConnectionStringSettings connectionString)
            {
                switch (GetProviderName(connectionString).ToLower())
                {
                    case "system.data.odbc":
                        return Models.DatabaseType.Odbc;
                    case "system.data.oracle":
                        return Models.DatabaseType.Oracle;
                    case "system.data.sqlserverce.3.5":
                        return Models.DatabaseType.MicrosoftSQLServerCompact;
                    case "system.data.sqlclient":
                        return Models.DatabaseType.MicrosoftSQLServer;
                    case "mysql.data.mysqlclient":
                        return Models.DatabaseType.MySql;
                    case "system.data.oledb":
                        if (connectionString.ConnectionString.Contains("Microsoft.ACE"))
                        {
                            return Models.DatabaseType.AccessOLE;
                        }
                        return Models.DatabaseType.OLE;
                }
                return Models.DatabaseType.MicrosoftSQLServer;
            }

            public static System.Data.DbType GetDBType(string typeName)
            {
                switch (typeName.ToUpper())
                {
                    case "BIT":
                        return System.Data.DbType.Boolean;
                    case "CHAR":
                        return System.Data.DbType.StringFixedLength;
                    case "DATE":
                        return DbType.Date;
                    case "DATETIME":
                        return System.Data.DbType.DateTime;
                    case "DATETIME2":
                        return System.Data.DbType.DateTime2;
                    case "DECIMAL":
                    case "FLOAT":
                        return System.Data.DbType.Decimal;
                    case "IMAGE":
                    case "BINARY":
                    case "VARBINARY":
                        return System.Data.DbType.Binary;
                    case "INT":
                        return System.Data.DbType.Int32;
                    case "SMALLINT":
                        return System.Data.DbType.Int16;
                    case "BIGINT":
                        return DbType.Int64;
                    case "UNIQUEIDENTIFIER":
                        return System.Data.DbType.Guid;
                    case "VARCHAR":
                        return System.Data.DbType.String;
                    case "TIME":
                        return DbType.Time;
                    case "HIERARCHYID":
                        return DbType.Object;
                }
                return System.Data.DbType.String;
            }

            public static Type GetSystemType(System.Data.DbType dbType)
            {
                switch (dbType)
                {
                    case DbType.AnsiString:
                    case DbType.AnsiStringFixedLength:
                    case DbType.String:
                    case DbType.StringFixedLength:
                        return typeof(string);
                    case DbType.Binary:
                    case DbType.Byte:
                        return typeof(byte);
                    case DbType.Boolean:
                        return typeof(bool);
                    case DbType.Currency:
                        return typeof(decimal);
                    case DbType.Date:
                    case DbType.DateTime:
                    case DbType.DateTime2:
                    case DbType.DateTimeOffset:
                        return typeof(DateTime);
                    case DbType.Time:
                        return typeof(TimeSpan);
                    case DbType.Decimal:
                        return typeof(decimal);
                    case DbType.Double:
                        return typeof(double);
                    case DbType.Guid:
                        return typeof(Guid);
                    case DbType.Int16:
                    case DbType.UInt16:
                        return typeof(short);
                    case DbType.Int32:
                    case DbType.UInt32:
                        return typeof(int);
                    case DbType.Int64:
                    case DbType.UInt64:
                        return typeof(long);
                    case DbType.SByte:
                        return typeof(byte);
                    case DbType.Single:
                        return typeof(float);
                    case DbType.VarNumeric:
                        return typeof(decimal);
                    case DbType.Xml:
                        return typeof(string);
                }
                return typeof(string);
            }

            public static string GetProviderName(DbConnection connection)
            {
                switch (GetDatabaseType(connection))
                {
                    case Models.DatabaseType.Oracle:
                        return "System.Data.Oracle";
                    case Models.DatabaseType.Odbc:
                        return "System.Data.Odbc";
                    case Models.DatabaseType.MicrosoftSQLServerCompact:
                        return "System.Data.SqlServerCe.3.5";
                    case Models.DatabaseType.OLE:
                    case Models.DatabaseType.AccessOLE:
                        return "System.Data.OleDb";
                    case Models.DatabaseType.MySql:
                        return "MySql.Data.MySqlClient";
                }
                return "System.Data.SqlClient";
            }

            public static string GetProviderName(System.Configuration.ConnectionStringSettings connectionString)
            {
                return connectionString.ProviderName;
            }

            #endregion

            #region Database Structure

            private IList<Models.ColumnModel> GetColumns(DataTable dataTable)
            {
                IList<Models.ColumnModel> list = new List<Models.ColumnModel>();
                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    Models.ColumnModel column = new Models.ColumnModel();
                    InitializeColumn(column, row);
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

                IList<Models.ColumnModel> list = new List<Models.ColumnModel>();

                DataTable dataTable = null;

                dataTable = _databaseProvider.GetTableColumns(connection);

                if (dataTable != null)
                {
                    list = GetColumns(dataTable);
                }

                return list;
            }

            public IList<Models.ColumnModel> GetViewColumns(DbConnection connection)
            {
                IList<Models.ColumnModel> list = new List<Models.ColumnModel>();

                var dataTable = _databaseProvider.GetViewColumns(connection);

                if (dataTable != null)
                {
                    list = GetColumns(dataTable);
                }

                return list;
            }

            private void InitializeColumn(Models.ColumnModel column, DataRow row)
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

                var targetColumnType = _databaseProvider.GetColumnType(new Models.ColumnTypeModel() { ColumnType = column.ColumnType, Precision = column.Precision, Scale = column.Scale }, Models.DatabaseType.MicrosoftSQLServer);
                if (targetColumnType != null)
                {
                    column.ColumnType = targetColumnType.ColumnType;
                    column.Precision = targetColumnType.Precision.GetValueOrDefault();
                    column.Scale = targetColumnType.Scale.GetValueOrDefault();
                }


            }

            public IList<Models.DefinitionModel> GetDefinitions(DbConnection connection)
            {
                List<Models.DefinitionModel> list = new List<Models.DefinitionModel>();

                var dtDefinitions = _databaseProvider.GetDefinitions(connection);

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
            public IList<Models.SecurityPolicyModel> GetSecurityPolicies(DbConnection connection)
            {
                var list = new List<Models.SecurityPolicyModel>();

                var dtDefinitions = _databaseProvider.GetSecurityPolicies(connection);

                var dtDependencies = _databaseProvider.GetDefinitionDependencies(connection);

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
            private void VerifyDefinitionsDependencies(IList<Models.DefinitionModel> list, DataTable dependencies)
            {
                bool blnListChanged = false;
                foreach (System.Data.DataRow row in dependencies.Rows)
                {
                    string strName = row["name"].ToString();
                    string strReferencingName = row["referencing_entity_name"].ToString().Trim();

                    var nameDefinition = (
                        from i in list
                        where i.DefinitionName.EqualsIgnoreCase(strName)
                        select i).FirstOrDefault();
                    var referenceDefinition = (
                        from i in list
                        where i.DefinitionName.EqualsIgnoreCase(strReferencingName)
                        select i).FirstOrDefault();

                    if (nameDefinition != null && referenceDefinition != null)
                    {

                        int nameIndex = list.IndexOf(nameDefinition);
                        int referenceIndex = list.IndexOf(referenceDefinition);

                        if (nameIndex > referenceIndex)
                        {
                            list.Remove(nameDefinition);
                            list.Insert(list.IndexOf(referenceDefinition), nameDefinition);
                            blnListChanged = true;
                            break;
                        }
                    }
                }
                if (blnListChanged)
                {
                    VerifyDefinitionsDependencies(list, dependencies);
                }
            }

            public IList<Models.ForeignKeyModel> GetForeignKeys(DbConnection connection, IList<string> tables)
            {
                IList<Models.ForeignKeyModel> list = new List<Models.ForeignKeyModel>();

                DataTable dataTable = _databaseProvider.GetForeignKeys(connection);

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

            public IList<Models.CheckConstraintModel> GetCheckConstraints(DbConnection connection, IList<string> tables)
            {
                var list = new List<Models.CheckConstraintModel>();

                var dataTable = _databaseProvider.GetCheckConstraints(connection);

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

            public IList<Models.IndexModel> GetIndexes(DbConnection connection, IEnumerable<string> tables, bool? isPrimaryKey = null)
            {
                IList<Models.IndexModel> list = new List<Models.IndexModel>();
                System.Data.DataTable dtIndexes = null;

                dtIndexes = _databaseProvider.GetIndexes(connection);

                if (dtIndexes != null)
                {

                    var indexBucketCounts = new List<IndexBucket>();

                    var dtIndexBucketCounts = _databaseProvider.GetIndexeBucketCounts(connection);
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

            private List<Models.TableModel> GetTables(DataTable dataTable, IList<Models.ColumnModel> columns)
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

            public IList<Models.TableModel> GetTables(DbConnection connection, IList<Models.ColumnModel> columns, bool withBackup = false)
            {
                System.Data.DataTable dataTable = null;

                var databaseType = GetDatabaseType(connection);

                dataTable = _databaseProvider.GetTables(connection);

                IList<Models.TableModel> list = new List<Models.TableModel>();

                if (dataTable != null)
                {
                    list = GetTables(dataTable, columns);

                    if (list.Count == 0 && databaseType == Models.DatabaseType.Odbc)
                    {

                        list = GetViews(connection, columns);
                    }

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

            public IList<Models.TableModel> GetViews(DbConnection connection, IList<Models.ColumnModel> columns)
            {
                IList<Models.TableModel> list = new List<Models.TableModel>();

                System.Data.DataTable dataTable = _databaseProvider.GetViews(connection);
                if (dataTable != null)
                {
                    list = GetTables(dataTable, columns);
                    foreach (var item in list)
                    {
                        item.IsView = true;
                    }
                }

                return list;
            }

            public IList<Models.TriggerModel> GetTriggers(DbConnection connection, IList<string> tables, IList<string> views, string objectFilter)
            {
                IList<Models.TriggerModel> list = new List<Models.TriggerModel>();

                var dataTable = _databaseProvider.GetTriggers(connection);

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

            public bool IsDatabaseEmpty(System.Configuration.ConnectionStringSettings connectionString)
            {
                var model = new Models.DatabaseModel(connectionString, new[] { _databaseProvider }, _connectionCreatedNotifications);
                return model.Tables.Count == 0;
            }

            #endregion

        }
    }

}