﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseTools.Processes
{
    public class TableConverter
    {


        public void ConvertTables(TableConverterSettings settings,
            IProgress<TableProgress> progress,
            int maxDegreeOfParallelism)
        {

            var tables = settings.SourceTables.OrderBy(i => i.TableName);

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            Parallel.ForEach(tables, options, (sourceTable) =>
            {
                this.ConvertTable(settings, sourceTable, progress);
            });
        }

        public void ConvertTable(TableConverterSettings settings, Models.TableModel sourceTable, IProgress<TableProgress> progress)
        {

            var targetTable = (
                       from i in settings.TargetTables
                       where i.TableName.Equals(sourceTable.TableName, StringComparison.InvariantCultureIgnoreCase)
                       select i).FirstOrDefault();

            if (targetTable != null)
            {
                try
                {

                    if (settings.UseBulkCopy)
                    {
                        this.ConvertBulk(progress, sourceTable, settings.SourceConnectionString, targetTable, settings.TargetConnectionString, settings.TrimStrings, settings.BatchSize);
                    }
                    else
                    {
                        this.Convert(progress, sourceTable, settings.SourceConnectionString, targetTable, settings.TargetConnectionString, settings.TrimStrings);
                    }
                }
                catch (Exception ex)
                {
                    string strException = string.Empty;
                    if (!(ex.Message.Equals("ERROR [00000] [QODBC] Error: 3250 - This feature is not enabled or not available in this version of QuickBooks.")))
                    {
                        strException = ex.ToString();
                    }

                    if (progress == null)
                    {
                        if (ex as TableConverterException == null)
                        {
                            throw;
                        }
                        else
                        {
                            throw new TableConverterException(sourceTable, strException, ex);
                        }
                    }
                    else
                    {
                        progress.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable, ErrorMessage = strException });
                    }

                }
            }
        }

        private void ConvertBulk(IProgress<TableProgress> progress,
            Models.TableModel sourceTable,
            System.Configuration.ConnectionStringSettings sourceConnectionString,
            Models.TableModel targetTable,
            System.Configuration.ConnectionStringSettings targetConnectionString,
            bool trimStrings,
            int batchSize)
        {
            if (progress != null)
            {
                progress.Report(new TableProgress() { ProgressPercentage = 0, Table = sourceTable });
            }


            System.Data.Common.DbProviderFactory sourceFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(sourceConnectionString);
            System.Data.Common.DbProviderFactory targetFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(targetConnectionString);

            var sourceDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(sourceConnectionString);
            var targetDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(targetConnectionString);

            int intProgress = 0;
            int intTargetRowCount = 0;

            using (System.Data.Common.DbConnection targetConnection = DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
            {
                intTargetRowCount = this.GetRowCount(targetConnection, targetTable.TableName, targetDatabaseType);
            }

            if (intTargetRowCount == 0)
            {
                int? intSourceRowCount = null;

                if (progress != null)
                {
                    using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
                    {

                        intSourceRowCount = this.GetRowCount(sourceConnection, sourceTable.TableName, sourceDatabaseType);
                    }
                }

                if (!intSourceRowCount.HasValue || intSourceRowCount.Value > 0)
                {

                    int intRowIndex = 0;

                    System.Text.StringBuilder sbColumns = new System.Text.StringBuilder();

                    var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);
                    var targetMatchedColumns = this.GetMatchedColumns(targetTable.Columns, sourceTable.Columns);

                    foreach (var sourceColumn in sourceMatchedColumns)
                    {
                        if (sbColumns.Length > 0)
                        {
                            sbColumns.Append(", ");
                        }
                        sbColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
                    }

                    using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
                    {
                        using (var command = DatabaseTools.Processes.Database.CreateDbCommand(sourceConnection))
                        {
                            this.SetReadTimeout(command);
                            command.CommandText = this.FormatCommandText($"SELECT {sbColumns.ToString()} FROM [{sourceTable.TableName}]", sourceDatabaseType);
                            using (var reader = command.ExecuteReader())
                            {
                                var reader2 = new TableConverterReader(reader, sourceMatchedColumns, targetMatchedColumns, trimStrings);

                                if (targetDatabaseType == Models.DatabaseType.MySql)
                                {

                                    using (MySql.Data.MySqlClient.MySqlConnection targetConnection = (MySql.Data.MySqlClient.MySqlConnection)DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
                                    {

                                        var tempFile = System.IO.Path.GetTempFileName();

                                        using (System.IO.StreamWriter stringWriter = new System.IO.StreamWriter(tempFile))
                                        {
                                            while (reader2.Read())
                                            {
                                                foreach (var column in sourceMatchedColumns)
                                                {
                                                    int index = reader2.GetOrdinal(column.ColumnName);
                                                    object value = reader2.GetValue(index);
                                                    if (value == DBNull.Value)
                                                    {
                                                        value = "NULL";
                                                    }
                                                    stringWriter.Write($"{value.ToString()}\t");
                                                }
                                                stringWriter.WriteLine();
                                            }
                                        }
                                        
                                      
                                        var bcp = new MySql.Data.MySqlClient.MySqlBulkLoader(targetConnection);

                                        bcp.Columns.Clear();
                                        foreach (var col in targetMatchedColumns)
                                        {
                                            bcp.Columns.Add(col.ColumnName);
                                        }

                                        bcp.TableName = sourceTable.TableName;
                                        bcp.FieldTerminator = "\t";
                                        bcp.LineTerminator = "\r\n";
                                        bcp.FileName = tempFile;
                                        bcp.NumberOfLinesToSkip = 0;

                                        bcp.Load();

                                        System.IO.File.Delete(tempFile);

                                    }

                                }
                                else
                                {
                                    using (System.Data.SqlClient.SqlBulkCopy bcp = new System.Data.SqlClient.SqlBulkCopy(targetConnectionString.ConnectionString, SqlBulkCopyOptions.KeepIdentity))
                                    {
                                        bcp.DestinationTableName = $"[{targetTable.TableName}]";
                                        bcp.BatchSize = batchSize == 0 ? 10000 : batchSize;
                                        bcp.BulkCopyTimeout = 600;
                                        bcp.NotifyAfter = bcp.BatchSize;

                                        bcp.SqlRowsCopied += (object sender, System.Data.SqlClient.SqlRowsCopiedEventArgs e) =>
                                        {
                                            if (progress != null &&
                                                intSourceRowCount.HasValue)
                                            {
                                                intRowIndex += bcp.BatchSize;

                                                if (intRowIndex > intSourceRowCount)
                                                {
                                                    intRowIndex = intSourceRowCount.Value;
                                                }

                                                int intNewProgress = System.Convert.ToInt32(intRowIndex / (double)intSourceRowCount * 100);

                                                if (intProgress != intNewProgress &&
                                                    intNewProgress < 100)
                                                {
                                                    intProgress = intNewProgress;
                                                    progress.Report(new TableProgress() { ProgressPercentage = intProgress, Table = sourceTable });
                                                }
                                            }
                                        };

                                        bcp.WriteToServer(reader2);

                                    }
                                }


                            }
                        }
                    }

                }
            }


            if (progress != null &&
                intProgress != 100)
            {
                progress.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable });
            }

        }

        private void Convert(IProgress<TableProgress> progress,
            Models.TableModel sourceTable,
            System.Configuration.ConnectionStringSettings sourceConnectionString,
            Models.TableModel targetTable,
            System.Configuration.ConnectionStringSettings targetConnectionString,
            bool trimStrings)
        {
            if (progress != null)
            {
                progress.Report(new TableProgress() { ProgressPercentage = 0, Table = sourceTable });
            }

            System.Data.Common.DbProviderFactory sourceFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(sourceConnectionString);
            System.Data.Common.DbProviderFactory targetFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(targetConnectionString);

            var sourceDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(sourceConnectionString);
            var targetDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(targetConnectionString);

            int intProgress = 0;

            var intTargetRowCount = 0;

            using (System.Data.Common.DbConnection targetConnection = DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
            {
                intTargetRowCount = this.GetRowCount(targetConnection, targetTable.TableName, targetDatabaseType);
            }

            if (intTargetRowCount == 0)
            {

                bool blnContainsIdentity = false;

                var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);
                var targetMatchedColumns = this.GetMatchedColumns(targetTable.Columns, sourceTable.Columns);

                using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
                {

                    using (System.Data.Common.DbCommand sourceCommand = DatabaseTools.Processes.Database.CreateDbCommand(sourceConnection))
                    {
                        this.SetReadTimeout(sourceCommand);

                        int? intSourceRowCount = null;

                        if (progress != null)
                        {
                            intSourceRowCount = this.GetRowCount(sourceConnection, sourceTable.TableName, sourceDatabaseType);
                        }

                        if (!intSourceRowCount.HasValue || intSourceRowCount.Value > 0)
                        {

                            System.Text.StringBuilder sbSelectColumns = new System.Text.StringBuilder();

                            foreach (var sourceColumn in sourceMatchedColumns)
                            {
                                if (sbSelectColumns.Length > 0)
                                {
                                    sbSelectColumns.Append(", ");
                                }
                                sbSelectColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
                            }

                            sourceCommand.CommandText = this.FormatCommandText($"SELECT {sbSelectColumns.ToString()} FROM [{sourceTable.TableName}]", sourceDatabaseType);

                            using (System.Data.Common.DbDataReader sourceReader = sourceCommand.ExecuteReader())
                            {

                                var converterReader = new TableConverterReader(sourceReader, sourceMatchedColumns, targetMatchedColumns, trimStrings);

                                var intFieldCount = converterReader.FieldCount;

                                using (System.Data.Common.DbConnection targetConnection = DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
                                {
                                    using (var targetCommand = DatabaseTools.Processes.Database.CreateDbCommand(targetConnection))
                                    {

                                        System.Text.StringBuilder sbColumns = new System.Text.StringBuilder();
                                        System.Text.StringBuilder sbParamaters = new System.Text.StringBuilder();

                                        foreach (DatabaseTools.Models.ColumnModel targetColumn in targetMatchedColumns)
                                        {
                                            if (targetColumn.IsIdentity)
                                            {
                                                if (targetCommand as SqlCommand != null)
                                                {
                                                    blnContainsIdentity = true;
                                                }
                                            }

                                            if (sbColumns.Length > 0)
                                            {
                                                sbColumns.Append(", ");
                                            }
                                            sbColumns.AppendFormat("[{0}]", targetColumn.ColumnName);
                                            if (sbParamaters.Length > 0)
                                            {
                                                sbParamaters.Append(", ");
                                            }

                                            System.Data.Common.DbParameter paramater = targetFactory.CreateParameter();
                                            paramater.ParameterName = string.Concat("@", this.GetParameterNameFromColumn(targetColumn.ColumnName));

                                            paramater.DbType = targetColumn.DbType;

                                            switch (paramater.DbType)
                                            {
                                                case System.Data.DbType.StringFixedLength:
                                                case System.Data.DbType.String:
                                                    paramater.Size = targetColumn.Precision;
                                                    break;
                                                case System.Data.DbType.Time:
                                                    if ((paramater) is System.Data.SqlClient.SqlParameter)
                                                    {
                                                        ((System.Data.SqlClient.SqlParameter)paramater).SqlDbType = System.Data.SqlDbType.Time;
                                                    }
                                                    break;
                                                case DbType.DateTime2:
                                                    if ((paramater) is MySql.Data.MySqlClient.MySqlParameter)
                                                    {
                                                        ((MySql.Data.MySqlClient.MySqlParameter)paramater).MySqlDbType = MySql.Data.MySqlClient.MySqlDbType.DateTime;
                                                    }
                                                    break;
                                            }

                                            sbParamaters.Append(paramater.ParameterName);

                                            targetCommand.Parameters.Add(paramater);

                                        }

                                        targetCommand.CommandText = this.FormatCommandText(string.Format("INSERT INTO [{0}] ({1}) VALUES ({2})", targetTable.TableName, sbColumns.ToString(), sbParamaters.ToString()), targetDatabaseType);

                                        if (blnContainsIdentity)
                                        {
                                            targetCommand.CommandText = this.FormatCommandText(string.Format("SET IDENTITY_INSERT [{0}] ON;" + Environment.NewLine + targetCommand.CommandText + Environment.NewLine + "SET IDENTITY_INSERT [{0}] OFF;", targetTable.TableName), targetDatabaseType);
                                        }

                                        int rowIndex = 0;

                                        while (converterReader.Read())
                                        {

                                            for (int intIndex = 0; intIndex < targetCommand.Parameters.Count; intIndex++)
                                            {
                                                var parameter = targetCommand.Parameters[intIndex];
                                                parameter.Value = converterReader.GetValue(intIndex);
                                            }

                                            rowIndex += 1;

                                            int intNewProgress = intProgress;

                                            if (intSourceRowCount.HasValue)
                                            {
                                                intNewProgress = System.Convert.ToInt32(rowIndex / (double)intSourceRowCount.Value * 100);
                                            }

                                            try
                                            {
                                                targetCommand.ExecuteNonQuery();

                                                if (progress != null)
                                                {
                                                    if (intProgress != intNewProgress &&
                                                        intNewProgress != 100)
                                                    {
                                                        intProgress = intNewProgress;
                                                        progress.Report(new TableProgress() { ProgressPercentage = intNewProgress, Table = sourceTable });
                                                    }
                                                }

                                            }
                                            catch (Exception ex)
                                            {
                                                string strRowErrorMessage = this.GetRowErrorMessage(targetCommand, targetMatchedColumns, targetCommand.Parameters.Count - 1, ex);

                                                string strErrorMessage = string.Format("could not insert row on table: {0} at row: {1}", sourceTable.TableName, strRowErrorMessage);

                                                throw new TableConverterException(sourceTable, strErrorMessage, ex);
                                            }

                                            intProgress = intNewProgress;


                                        }
                                    }
                                }
                            }

                        }
                    }
                }

            }

            if (progress != null &&
                intProgress != 100)
            {
                progress.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable });
            }

        }

        private object GetColumnValue(Models.ColumnModel targetColumn, object value, bool trimStrings)
        {
            object returnValue = value;

            if (value != DBNull.Value)
            {
                var dbType = targetColumn.DbType;

                switch (dbType)
                {
                    case System.Data.DbType.Date:
                    case System.Data.DbType.DateTime:
                        try
                        {
                            DateTime dt = System.Convert.ToDateTime(value);

                            if (dt <= new DateTime(1753, 1, 1))
                            {
                                returnValue = new DateTime(1753, 1, 1);
                            }
                            else if (dt > new DateTime(9999, 12, 31))
                            {
                                returnValue = new DateTime(9999, 12, 31);
                            }
                            else
                            {
                                returnValue = dt;
                            }
                        }
                        catch
                        {
                            returnValue = new DateTime(1753, 1, 1);
                        }
                        break;
                    case System.Data.DbType.DateTime2:
                        try
                        {
                            DateTime dt = System.Convert.ToDateTime(value);
                            returnValue = dt;
                        }
                        catch
                        {
                            returnValue = DateTime.MinValue;
                        }
                        break;
                    case System.Data.DbType.Time:
                        {
                            if ((value) is TimeSpan)
                            {
                                returnValue = (new DateTime(1753, 1, 1)).Add((TimeSpan)value);
                            }
                            else if ((value) is DateTime)
                            {
                                DateTime dt = System.Convert.ToDateTime(value);

                                if (dt <= new DateTime(1753, 1, 1))
                                {
                                    returnValue = DBNull.Value;
                                }
                                else if (dt > new DateTime(9999, 12, 31))
                                {
                                    returnValue = DBNull.Value;
                                }
                                else
                                {
                                    returnValue = dt;
                                }
                            }
                            else
                            {
                                returnValue = value;
                            }
                            break;
                        }
                    case System.Data.DbType.AnsiString:
                    case System.Data.DbType.AnsiStringFixedLength:
                    case System.Data.DbType.String:
                    case System.Data.DbType.StringFixedLength:
                        {
                            if (trimStrings)
                            {
                                returnValue = System.Convert.ToString(value).TrimEnd();
                            }
                            else
                            {
                                returnValue = System.Convert.ToString(value);
                            }
                            break;
                        }
                }
            }


            return returnValue;
        }

        private DataTable GetTableValues(System.Data.Common.DbProviderFactory sourceFactory, System.Configuration.ConnectionStringSettings sourceConnectionString, Models.TableModel sourceTable, Models.TableModel targetTable, bool trimStrings,
            int take, int skip)
        {
            var sourceDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(sourceConnectionString);

            System.Text.StringBuilder sbColumns = new System.Text.StringBuilder();
            System.Text.StringBuilder sbKeyColumns = new System.Text.StringBuilder();

            var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);
            var targetMatchedColumns = this.GetMatchedColumns(targetTable.Columns, sourceTable.Columns);

            foreach (var sourceColumn in sourceMatchedColumns)
            {
                if (sbColumns.Length > 0)
                {
                    sbColumns.Append(", ");
                }
                sbColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
                if (sourceColumn.IsPrimaryKey)
                {
                    if (sbKeyColumns.Length > 0)
                    {
                        sbKeyColumns.Append(", ");
                    }
                    sbKeyColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
                }
            }

            var dataTable = new System.Data.DataTable($"[{targetTable.TableName}]");

            foreach (var targetColumn in targetMatchedColumns)
            {
                var column = new System.Data.DataColumn($"[{targetColumn.ColumnName}]");
                column.AutoIncrement = targetColumn.IsIdentity;
                column.AllowDBNull = targetColumn.IsNullable;

                column.DataType = Database.GetSystemType(targetColumn.DbType);

                if (column.DataType == typeof(string))
                {
                    column.MaxLength = targetColumn.Precision;
                }

                dataTable.Columns.Add(column);
            }

            using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
            {

                using (System.Data.Common.DbCommand sourceCommand = DatabaseTools.Processes.Database.CreateDbCommand(sourceConnection))
                {
                    this.SetReadTimeout(sourceCommand);

                    if (take != 0)
                    {
                        if (sourceDatabaseType == Models.DatabaseType.MySql)
                        {
                            sourceCommand.CommandText = this.FormatCommandText($"SELECT {sbColumns.ToString()} FROM [{sourceTable.TableName}] LIMIT {take} OFFSET {skip}", sourceDatabaseType);
                        }
                        else
                        {
                            sourceCommand.CommandText = this.FormatCommandText($"SELECT {sbColumns.ToString()} FROM [{sourceTable.TableName}] ORDER BY {sbKeyColumns.ToString()} OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY", sourceDatabaseType);
                        }
                    }
                    else
                    {
                        sourceCommand.CommandText = this.FormatCommandText($"SELECT {sbColumns.ToString()} FROM [{sourceTable.TableName}]", sourceDatabaseType);
                    }

                    using (System.Data.Common.DbDataReader sourceReader = sourceCommand.ExecuteReader())
                    {

                        var intFieldCount = sourceReader.FieldCount;

                        while (sourceReader.Read())
                        {
                            var dataRow = dataTable.NewRow();

                            for (int i = 0; i < intFieldCount; i++)
                            {

                                var sourceColumn = sourceMatchedColumns[i];
                                var targetColumn = targetMatchedColumns[i];

                                object value;

                                try
                                {
                                    value = sourceReader.GetValue(i);
                                }
#pragma warning disable CS0168 // Variable is declared but never used
                                catch (MySql.Data.Types.MySqlConversionException ex)
#pragma warning restore CS0168 // Variable is declared but never used
                                {
                                    if (sourceColumn.DbType == System.Data.DbType.DateTime2 ||
                                        sourceColumn.DbType == System.Data.DbType.Date ||
                                        sourceColumn.DbType == System.Data.DbType.DateTime ||
                                        sourceColumn.DbType == System.Data.DbType.DateTimeOffset)
                                    {
                                        value = DateTime.MinValue;
                                    }
                                    else
                                    {
                                        throw;
                                    }
                                }

                                dataRow[i] = this.GetColumnValue(targetColumn, value, trimStrings);
                            }

                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }

            return dataTable;
        }

        private IList<Models.ColumnModel> GetMatchedColumns(IList<Models.ColumnModel> sourceColumns, IList<Models.ColumnModel> targetColumns)
        {
            var list = new List<Models.ColumnModel>();

            foreach (DatabaseTools.Models.ColumnModel sourceColumn in sourceColumns)
            {
                string strColumnName = sourceColumn.ColumnName;
                DatabaseTools.Models.ColumnModel targetColumn = (
                    from c in targetColumns
                    where c.ColumnName.Equals(strColumnName, StringComparison.InvariantCultureIgnoreCase)
                    select c).FirstOrDefault();

                if (targetColumn != null && !targetColumn.IsComputed)
                {

                    list.Add(sourceColumn);
                }
            }

            return list;
        }

        private void SetReadTimeout(System.Data.Common.DbCommand sourceCommand)
        {
            var command = sourceCommand as MySql.Data.MySqlClient.MySqlCommand;
            if (command != null)
            {
                command.CommandText = "set net_write_timeout=99999;set net_read_timeout=99999;";
                command.ExecuteNonQuery();
            }
        }

        private string FormatCommandText(string commandText, DatabaseTools.Models.DatabaseType databaseType)
        {
            switch (databaseType)
            {
                case Models.DatabaseType.Odbc:
                    commandText = commandText.Replace("[", "\"").Replace("]", "\"");
                    break;
                case Models.DatabaseType.OLE:
                    commandText = commandText.Replace("[", "\"").Replace("]", "\"");
                    break;
                case Models.DatabaseType.AccessOLE:
                    //Do nothing, access likes brackets
                    break;
                case Models.DatabaseType.MySql:
                    commandText = commandText.Replace("[", "`").Replace("]", "`");
                    break;
            }
            return commandText;
        }

        private int GetRowCount(System.Data.Common.DbConnection connection, string tableName, DatabaseTools.Models.DatabaseType databaseType)
        {
            int rowCount = 0;

            try
            {
                using (var command = DatabaseTools.Processes.Database.CreateDbCommand(connection))
                {

                    switch (databaseType)
                    {
                        case DatabaseTools.Models.DatabaseType.MicrosoftSQLServer:
                            command.CommandText = string.Format("(SELECT sys.sysindexes.rows FROM sys.tables INNER JOIN sys.sysindexes ON sys.tables.object_id = sys.sysindexes.id AND sys.sysindexes.indid < 2 WHERE sys.tables.name = '{0}')", tableName);
                            break;
                        default:
                            command.CommandText = this.FormatCommandText(string.Format("SELECT COUNT(1) FROM [{0}]", tableName), databaseType);
                            break;
                    }
                    rowCount = System.Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch
            {

            }

            return rowCount;
        }

        private string GetParameterNameFromColumn(string columnName)
        {
            return columnName.Replace("-", "").Replace(" ", "");
        }

        private string GetRowErrorMessage(System.Data.Common.DbCommand command, IList<DatabaseTools.Models.ColumnModel> columns, int columnIndex, System.Exception ex)
        {
            System.Text.StringBuilder sbRow = new System.Text.StringBuilder();

            for (int intErrorIndex = 0; intErrorIndex < columnIndex; intErrorIndex++)
            {
                DatabaseTools.Models.ColumnModel targetColumn = columns[intErrorIndex];
                if (targetColumn != null)
                {
                    if (!targetColumn.IsNullable)
                    {
                        string strColumnName = targetColumn.ColumnName;
                        object objTargetValue = command.Parameters[intErrorIndex].Value;
                        if (objTargetValue == null || objTargetValue == DBNull.Value)
                        {
                            objTargetValue = "NULL";
                        }
                        if (sbRow.Length > 0)
                        {
                            sbRow.Append(" AND ");
                        }
                        sbRow.AppendFormat(" {0} = '{1}'", strColumnName, System.Convert.ToString(objTargetValue));
                    }
                }
            }
            sbRow.AppendLine();

            sbRow.AppendLine("ERROR:");
            sbRow.AppendLine(ex.Message);

            return sbRow.ToString();
        }


    }
}
