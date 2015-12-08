﻿
using DatabaseTools.Extensions;
using DatabaseTools.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace DatabaseTools
{
    namespace Processes
    {
        public class Database
        {

            private Database()
            {

            }

            #region Create Database Methods

            public static System.Data.Common.DbConnection CloneDbConnection(System.Data.Common.DbConnection dbConnection)
            {
                System.Data.Common.DbConnection connection = CreateDbProviderFactory(dbConnection).CreateConnection();
                connection.ConnectionString = dbConnection.ConnectionString;
                connection.Open();
                return connection;
            }

            public static System.Data.Common.DbCommand CreateDbCommand(System.Data.Common.DbConnection dbConnection)
            {
                System.Data.Common.DbCommand command = dbConnection.CreateCommand();
                command.Connection = dbConnection;

                command.CommandTimeout = Math.Max(1800, command.CommandTimeout);

                if (Data.DbTransactionScope.Current != null && Data.DbTransactionScope.Current.Connection == dbConnection)
                {
                    command.Transaction = Data.DbTransactionScope.Current;
                }
                return command;
            }

            public static System.Data.Common.DbConnection CreateDbConnection(System.Configuration.ConnectionStringSettings connectionString)
            {
                return CreateDbConnection(CreateDbProviderFactory(connectionString), connectionString);
            }

            public static System.Data.Common.DbConnection CreateDbConnection(System.Data.Common.DbProviderFactory dbProviderFactory, System.Configuration.ConnectionStringSettings connectionString)
            {
                System.Data.Common.DbConnection connection = dbProviderFactory.CreateConnection();

                var dbType = GetDatabaseType(connectionString);

                if (dbType == Models.DatabaseType.MicrosoftSQLServer)
                {
                    System.Data.SqlClient.SqlConnectionStringBuilder csb = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString.ConnectionString);
                    csb.ConnectTimeout = Math.Max(45, csb.ConnectTimeout);
                    connection.ConnectionString = csb.ConnectionString;
                }
                else if (dbType == Models.DatabaseType.MySql)
                {
                    var csb = new  MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connectionString.ConnectionString);
                    csb.ConnectionTimeout = Math.Max(45, csb.ConnectionTimeout);
                    csb.DefaultCommandTimeout = Math.Max(150, csb.ConnectionTimeout);
                    connection.ConnectionString = csb.ConnectionString;
                }
                else
                {
                    connection.ConnectionString = connectionString.ConnectionString;
                }

                connection.Open();
                return connection;
            }

            public static System.Data.Common.DbConnection CreateDbConnection(System.Data.SqlClient.SqlConnectionStringBuilder csb)
            {
                System.Data.Common.DbConnection connection = new System.Data.SqlClient.SqlConnection();
                connection.ConnectionString = csb.ConnectionString;
                connection.Open();
                return connection;
            }

            public static System.Data.Common.DbProviderFactory CreateDbProviderFactory(System.Data.Common.DbConnection connection)
            {
                string strProviderName = GetProviderName(connection);
                return CreateDbProviderFactory(strProviderName);
            }

            public static System.Data.Common.DbProviderFactory CreateDbProviderFactory(System.Configuration.ConnectionStringSettings connectionString)
            {
                string strProviderName = GetProviderName(connectionString);
                return CreateDbProviderFactory(strProviderName);
            }

            public static System.Data.Common.DbProviderFactory CreateDbProviderFactory(string providerName)
            {
                switch (providerName)
                {
                    default:
                        return System.Data.Common.DbProviderFactories.GetFactory(providerName);
                }
            }

            #endregion

            #region Execute Database Methods


            public static System.Data.DataSet Execute(System.Data.Common.DbConnection connection, string sqlCommand)
            {
                System.Data.DataSet ds = new System.Data.DataSet();

                using (System.Data.Common.DbCommand command = Database.CreateDbCommand(connection))
                {
                    command.CommandText = sqlCommand;
                    using (System.Data.Common.DbDataReader reader = command.ExecuteReader())
                    {
                        ds.Load(reader, LoadOption.OverwriteChanges, "Table");
                    }
                }

                return ds;
            }

            public static System.Data.DataSet Execute(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                System.Data.DataSet ds = new System.Data.DataSet();

                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Data.Common.DbProviderFactory factory = Database.CreateDbProviderFactory(connectionString);
                    using (System.Data.Common.DbConnection connection = Database.CreateDbConnection(factory, connectionString))
                    {
                        ds = Execute(connection, sqlCommand);
                    }
                }

                return ds;
            }

            public static void ExecuteFile(System.Data.Common.DbConnection connection, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Text.RegularExpressions.Regex regEx = new System.Text.RegularExpressions.Regex("^[\\s]*GO[^a-zA-Z0-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                    foreach (string commandText in regEx.Split(sqlCommand))
                    {
                        System.Data.Common.DbConnection commandConnection = connection;

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

            public static void ExecuteFile(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Data.Common.DbProviderFactory factory = Database.CreateDbProviderFactory(connectionString);
                    using (System.Data.Common.DbConnection connection = Database.CreateDbConnection(factory, connectionString))
                    {
                        ExecuteFile(connection, sqlCommand);
                    }
                }
            }

            public static void ExecuteNonQuery(System.Data.Common.DbConnection connection, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    using (System.Data.Common.DbCommand command = Database.CreateDbCommand(connection))
                    {
                        command.CommandText = sqlCommand;
                        command.ExecuteNonQuery();
                    }
                }
            }

            public static void ExecuteNonQuery(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Data.Common.DbProviderFactory factory = Database.CreateDbProviderFactory(connectionString);
                    using (System.Data.Common.DbConnection connection = Database.CreateDbConnection(factory, connectionString))
                    {
                        ExecuteNonQuery(connection, sqlCommand);
                    }
                }
            }

            public static object ExecuteScalar(System.Configuration.ConnectionStringSettings connectionString, string sqlCommand)
            {
                object returnValue = null;
                if (!(string.IsNullOrEmpty(sqlCommand)))
                {
                    System.Data.Common.DbProviderFactory factory = Database.CreateDbProviderFactory(connectionString);

                    using (System.Data.Common.DbConnection connection = Database.CreateDbConnection(factory, connectionString))
                    {
                        using (System.Data.Common.DbCommand command = Database.CreateDbCommand(connection))
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

            private static bool ContainsTable(IList<string> tables, string table)
            {
                return (from i in tables where i.EqualsIgnoreCase(table) select i).Any();
            }

            private static IDatabaseProvider GetDatabaseProvider(Models.DatabaseType databaseType)
            {
                if (databaseType == Models.DatabaseType.MySql)
                {
                    return new DatabaseTools.Providers.MySql.DatabaseProvider();
                }
                else if (databaseType == Models.DatabaseType.MicrosoftSQLServer)
                {
                    return new DatabaseTools.Providers.Mssql.DatabaseProvider();
                }
                return null;
            }

            public static System.Configuration.ConnectionStringSettings GetConnectionStringSetting(string connectionStringName)
            {
                return Configuration.ConnectionInfo.GetConnectionStringSetting(connectionStringName);
            }

            public static string GetStringValue(DataRow row, string columnName)
            {
                string value = string.Empty;

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

            public static Int32 GetInt32Value(DataRow row, string columnName)
            {
                Int32 value = 0;

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

            public static Models.DatabaseType GetDatabaseType(System.Data.Common.DbConnection connection)
            {
                if ((connection) is System.Data.Odbc.OdbcConnection)
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
                    case "DATETIME":
                        return System.Data.DbType.DateTime;
                    case "DATETIME2":
                        return System.Data.DbType.DateTime2;
                    case "DECIMAL":
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
                }
                return System.Data.DbType.String;
            }

            public static string GetProviderName(System.Data.Common.DbConnection connection)
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
                }
                return "System.Data.SqlClient";
            }

            public static string GetProviderName(System.Configuration.ConnectionStringSettings connectionString)
            {
                return connectionString.ProviderName;
            }

            #endregion

            #region Database Structure

            private static List<Models.ColumnModel> GetColumns(DataTable dataTable, IDatabaseProvider provider)
            {
                List<Models.ColumnModel> list = new List<Models.ColumnModel>();
                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    Models.ColumnModel column = new Models.ColumnModel();
                    InitializeColumn(column, row, provider);
                    list.Add(column);
                }
                return list;
            }

            public static List<Models.ColumnModel> GetTableColumns(System.Configuration.ConnectionStringSettings connectionString)
            {

                List<Models.ColumnModel> list = new List<Models.ColumnModel>();

                var databaseType = GetDatabaseType(connectionString);
                var provider = GetDatabaseProvider(databaseType);

                DataTable dataTable = null;

                if (provider != null)
                {
                    dataTable = GetDatabaseProvider(databaseType).GetTableColumns(connectionString);
                }

                if (dataTable != null)
                {
                    list = GetColumns(dataTable, GetDatabaseProvider(databaseType));
                }

                return list;
            }

            public static List<Models.ColumnModel> GetViewColumns(System.Configuration.ConnectionStringSettings connectionString)
            {
                List<Models.ColumnModel> list = new List<Models.ColumnModel>();

                var databaseType = GetDatabaseType(connectionString);
                var provider = GetDatabaseProvider(databaseType);

                DataTable dataTable = null;

                if (provider != null)
                {
                    dataTable = GetDatabaseProvider(databaseType).GetViewColumns(connectionString);
                }

                if (dataTable != null)
                {
                    list = GetColumns(dataTable, GetDatabaseProvider(databaseType));
                }

                return list;
            }

            private static void InitializeColumn(Models.ColumnModel column, DataRow row, IDatabaseProvider converter)
            {

                column.TableName = GetStringValue(row, "table_name");
                column.ColumnName = GetStringValue(row, "column_name");
                column.Precision = GetInt32Value(row, "precision");
                column.Scale = GetInt32Value(row, "scale");
                column.ColumnType = GetStringValue(row, "column_type");
                column.IsNullable = GetBoolValue(row, "is_nullable");
                column.IsIdentity = GetBoolValue(row, "is_identity");
                column.IsComputed = GetBoolValue(row, "is_computed");
                column.ComputedDefinition = GetStringValue(row, "computed_definition");
                column.ColumnID = GetInt32Value(row, "column_id");
                column.IsPrimaryKey = GetBoolValue(row, "is_primary_key");
                column.ColumnDefault = GetStringValue(row, "column_default");

                if (converter != null)
                {
                    var targetColumnType = converter.GetColumnType(new Models.ColumnTypeModel() { ColumnType = column.ColumnType, Precision = column.Precision, Scale = column.Scale }, Models.DatabaseType.MicrosoftSQLServer);
                    if (targetColumnType != null)
                    {
                        column.ColumnType = targetColumnType.ColumnType;
                        column.Precision = targetColumnType.Precision.GetValueOrDefault();
                        column.Scale = targetColumnType.Scale.GetValueOrDefault();
                    }
                }

            }

            public static IList<Models.DefinitionModel> GetDefinitions(System.Configuration.ConnectionStringSettings connectionString)
            {
                List<Models.DefinitionModel> list = new List<Models.DefinitionModel>();

                var databaseType = GetDatabaseType(connectionString);
                var provider = GetDatabaseProvider(databaseType);

                if (provider != null)
                {
                    var dtDefinitions = provider.GetDefinitions(connectionString);

                    var dtDependencies = provider.GetDefinitionDependencies(connectionString);

                    if (dtDefinitions != null)
                    {
                        list = (
                            from i in dtDefinitions.Rows.OfType<System.Data.DataRow>()
                            select new Models.DefinitionModel
                            {
                                Definition = i["definition"].ToString(),
                                DefinitionName = i["name"].ToString(),
                                XType = i["xtype"].ToString().Trim()
                            }
                            ).ToList();
                    }

                    if (dtDependencies != null)
                    {
                        VerifyDependencies(list, dtDependencies);
                    }


                }

                return list;
            }

            private static void VerifyDependencies(IList<Models.DefinitionModel> list, DataTable dependencies)
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
                    VerifyDependencies(list, dependencies);
                }
            }

            public static IList<Models.ForeignKeyModel> GetForeignKeys(System.Configuration.ConnectionStringSettings connectionString, IList<string> tables)
            {
                List<Models.ForeignKeyModel> list = new List<Models.ForeignKeyModel>();

                var databaseType = GetDatabaseType(connectionString);
                var provider = GetDatabaseProvider(databaseType);

                if (provider != null)
                {
                    DataTable dataTable = provider.GetForeignKeys(connectionString);

                    if (dataTable != null)
                    {
                        foreach (var tableGroup in (
                       from i in dataTable.Rows.Cast<System.Data.DataRow>()
                       group i by new
                       {
                           TableName = i["table_name"].ToString(),
                           ForeignKeyName = i["foreign_key_name"].ToString()
                       } into g
                       select new
                       {
                           TableName = g.Key.TableName,
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
                                    ReferencedTableName = summaryRow["referenced_table_name"].ToString(),
                                    IsNotForReplication = Convert.ToBoolean(summaryRow["is_not_for_replication"]),
                                    DeleteAction = summaryRow["delete_action"].ToString().Replace("_", " "),
                                    UpdateAction = summaryRow["update_action"].ToString().Replace("_", " ")
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

                }

                return list;
            }

            public static IList<Models.IndexModel> GetIndexes(System.Configuration.ConnectionStringSettings connectionString, IList<string> tables)
            {
                return GetIndexes(connectionString, tables, false);
            }

            private static IList<Models.IndexModel> GetIndexes(System.Configuration.ConnectionStringSettings connectionString, IList<string> tables, bool isPrimaryKey)
            {
                List<Models.IndexModel> list = new List<Models.IndexModel>();
                var provider = GetDatabaseProvider(GetDatabaseType(connectionString));
                System.Data.DataTable dtIndexes = null;

                if (provider != null)
                {
                    dtIndexes = provider.GetIndexes(connectionString);
                }

                if (dtIndexes != null)
                {

                    foreach (var indexGroup in (
                        from i in dtIndexes.Rows.Cast<System.Data.DataRow>()
                        group i by new { IndexName = i["index_name"].ToString(), TableName = i["table_name"].ToString() } into g
                        select new { IndexName = g.Key.IndexName, TableName = g.Key.TableName, Items = g.ToList() }))
                    {
                        if (ContainsTable(tables, indexGroup.TableName))
                        {
                            System.Data.DataRow summaryRow = indexGroup.Items[0];

                            Models.IndexModel index = new Models.IndexModel
                            {
                                TableName = indexGroup.TableName,
                                IndexName = indexGroup.IndexName,
                                IndexType = summaryRow["index_type"].ToString(),
                                IsUnique = Convert.ToBoolean(summaryRow["is_unique"]),
                                FillFactor = Convert.ToInt32(summaryRow["fill_factor"]),
                                IsPrimaryKey = Convert.ToBoolean(summaryRow["is_primary_key"])
                            };

                            if (index.IsPrimaryKey == isPrimaryKey)
                            {
                                foreach (var detialRow in indexGroup.Items.OrderBy(i => Convert.ToInt32(i["key_ordinal"])))
                                {
                                    bool blnIsDescending = Convert.ToBoolean(detialRow["is_descending_key"]);
                                    bool blnIsIncludeColumn = Convert.ToBoolean(detialRow["is_included_column"]);
                                    string strColumnName = detialRow["column_name"].ToString();

                                    if (blnIsIncludeColumn)
                                    {
                                        index.IncludeColumns.Add(strColumnName);
                                    }
                                    else if (blnIsDescending)
                                    {
                                        index.Columns.Add(strColumnName + " DESC");
                                    }
                                    else
                                    {
                                        index.Columns.Add(strColumnName);
                                    }
                                }

                                list.Add(index);
                            }

                        }
                    }
                }

                return list;
            }

            public static IList<Models.IndexModel> GetPrimaryKeys(System.Configuration.ConnectionStringSettings connectionString, IList<string> tables)
            {
                return GetIndexes(connectionString, tables, true);
            }

            private static List<Models.TableModel> GetTables(DataTable dataTable, IList<Models.ColumnModel> columns)
            {
                List<Models.TableModel> list = new List<Models.TableModel>();

                List<string> tables = new List<string>();

                Dictionary<string, IList<Models.ColumnModel>> columnIndex = new Dictionary<string, IList<Models.ColumnModel>>();

                foreach (var columnGroup in (
                    from i in columns
                    group i by i.TableName into g
                    select new { TableName = g.Key, Items = g.ToList() }))
                {
                    columnIndex.Add(columnGroup.TableName.ToUpper(), columnGroup.Items);
                }

                foreach (System.Data.DataRow row in dataTable.Rows)
                {
                    Models.TableModel table = new Models.TableModel();
                    table.TableName = Processes.Database.GetStringValue(row, "table_name");
                    if (!(tables.Contains(table.TableName.ToUpper())))
                    {
                        if (columnIndex.ContainsKey(table.TableName.ToUpper()))
                        {
                            var tableColumns = columnIndex[table.TableName.ToUpper()];
                            foreach (var column in (
                                        from i in tableColumns
                                        where i.TableName.EqualsIgnoreCase(table.TableName)
                                        select i)
                                        )
                            {
                                table.Columns.Add(column);
                            }
                        }
                        tables.Add(table.TableName.ToUpper());
                        list.Add(table);
                    }
                }

                return list;
            }

            public static List<Models.TableModel> GetTables(System.Configuration.ConnectionStringSettings connectionString, IList<Models.ColumnModel> columns, bool withBackup = false)
            {
                System.Data.DataTable dataTable = null;

                var databaseType = GetDatabaseType(connectionString);

                var provider = GetDatabaseProvider(databaseType);

                if (provider != null)
                {
                    dataTable = provider.GetTables(connectionString);
                }

                List<Models.TableModel> list = new List<Models.TableModel>();

                if (dataTable != null)
                {
                    list = GetTables(dataTable, columns);

                    if (list.Count == 0 && databaseType == Models.DatabaseType.Odbc)
                    {

                        list = GetViews(connectionString, columns);
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

            public static List<Models.TableModel> GetViews(System.Configuration.ConnectionStringSettings connectionString, IList<Models.ColumnModel> columns)
            {
                System.Data.DataTable dataTable = null;

                var databaseType = GetDatabaseType(connectionString);

                var provider = GetDatabaseProvider(databaseType);

                if (provider != null)
                {
                    dataTable = provider.GetViews(connectionString);
                }

                var list = GetTables(dataTable, columns);

                return list;
            }

            public static IList<Models.TriggerModel> GetTriggers(System.Configuration.ConnectionStringSettings connectionString, IList<string> tables, string objectFilter)
            {
                List<Models.TriggerModel> list = new List<Models.TriggerModel>();

                var databaseType = GetDatabaseType(connectionString);
                var provider = GetDatabaseProvider(databaseType);

                if (provider != null)
                {
                    var dataTable = provider.GetTriggers(connectionString);

                    if (dataTable != null)
                    {
                        foreach (System.Data.DataRow detailRow in dataTable.Rows)
                        {
                            string strTableName = detailRow["table_name"].ToString();
                            string strTriggerName = detailRow["trigger_name"].ToString();
                            string strDefinition = detailRow["definition"].ToString();

                            if (ContainsTable(tables, strTableName) || (!(string.IsNullOrEmpty(objectFilter)) && strTriggerName.ToLower().Contains(objectFilter)))
                            {
                                list.Add(new Models.TriggerModel { TableName = strTableName, TriggerName = strTriggerName, Definition = strDefinition });
                            }
                        }
                    }


                }

                return list;
            }

            public static bool IsDatabaseEmpty(System.Configuration.ConnectionStringSettings connectionString)
            {
                Models.DatabaseModel database = new Models.DatabaseModel(connectionString);
                return database.Tables.Count == 0;
            }

            #endregion

        }
    }

}