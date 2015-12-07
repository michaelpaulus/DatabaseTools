﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseTools.Processes
{
    public class TableConverter
    {

        public void ConvertBulk(System.ComponentModel.BackgroundWorker worker, 
            string tableName, 
            System.Configuration.ConnectionStringSettings sourceConnectionString, 
            IList<DatabaseTools.Models.ColumnModel> sourceTableColumns, 
            System.Configuration.ConnectionStringSettings targetConnectionString, 
            IList<DatabaseTools.Models.ColumnModel> targetTableColumns)
        {
            System.Data.Common.DbProviderFactory sourceFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(sourceConnectionString);
            System.Data.Common.DbProviderFactory targetFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(targetConnectionString);

            var sourceDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(sourceConnectionString);
            var targetDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(targetConnectionString);

            using (System.Data.Common.DbConnection targetConnection = DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
            {

                var intTargetRowCount = this.GetRowCount(targetConnection, tableName, targetDatabaseType);

                if (intTargetRowCount == 0L)
                {
                    using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
                    {

                        var intSourceRowCount = this.GetRowCount(sourceConnection, tableName, sourceDatabaseType);

                        if (intSourceRowCount > 0L)
                        {
                            using (System.Data.SqlClient.SqlBulkCopy bcp = new System.Data.SqlClient.SqlBulkCopy((System.Data.SqlClient.SqlConnection)targetConnection))
                            {
                                bcp.DestinationTableName = tableName;
                                bcp.BatchSize = 1000;
                                bcp.BulkCopyTimeout = 600;
                                bcp.NotifyAfter = bcp.BatchSize;

                                long intRowIndex = 0L;
                                int intProgress = 0;

                                bcp.SqlRowsCopied += (object sender, System.Data.SqlClient.SqlRowsCopiedEventArgs e) =>
                                {
                                    intRowIndex += 1L;

                                    int intNewProgress = System.Convert.ToInt32(intRowIndex / (double)intSourceRowCount * 100);

                                    if (intProgress != intNewProgress)
                                    {
                                        intProgress = intNewProgress;
                                        worker.ReportProgress(intProgress, tableName);
                                    }
                                };

                                using (var command = DatabaseTools.Processes.Database.CreateDbCommand(sourceConnection))
                                {
                                    command.CommandText = this.FormatCommandText(string.Format("SELECT * FROM [{0}]", tableName), sourceDatabaseType);
                                    using (var reader = command.ExecuteReader())
                                    {
                                        bcp.WriteToServer(reader);
                                    }
                                }
                            }
                        }


                    }
                }
            }

            worker.ReportProgress(100, tableName);
        }

        public void Convert(System.ComponentModel.BackgroundWorker worker,
            Models.TableModel sourceTable,
            System.Configuration.ConnectionStringSettings sourceConnectionString,
            Models.TableModel targetTable,
            System.Configuration.ConnectionStringSettings targetConnectionString,
            bool trimStrings)
        {
            System.Data.Common.DbProviderFactory sourceFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(sourceConnectionString);
            System.Data.Common.DbProviderFactory targetFactory = DatabaseTools.Processes.Database.CreateDbProviderFactory(targetConnectionString);

            var sourceDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(sourceConnectionString);
            var targetDatabaseType = DatabaseTools.Processes.Database.GetDatabaseType(targetConnectionString);

            int intProgress = 0;

            using (System.Data.Common.DbConnection targetConnection = DatabaseTools.Processes.Database.CreateDbConnection(targetFactory, targetConnectionString))
            {
                var intTargetRowCount = this.GetRowCount(targetConnection, targetTable.TableName, targetDatabaseType);
                if (intTargetRowCount == 0L)
                {
                    using (System.Data.Common.DbCommand targetCommand = DatabaseTools.Processes.Database.CreateDbCommand(targetConnection))
                    {
                        System.Text.StringBuilder sbColumns = new System.Text.StringBuilder();
                        System.Text.StringBuilder sbParamaters = new System.Text.StringBuilder();

                        bool blnContainsIdentity = false;

                        var sourceColumns = sourceTable.Columns.ToList();
                        List<DatabaseTools.Models.ColumnModel> targetColumns = new List<DatabaseTools.Models.ColumnModel>();

                        foreach (DatabaseTools.Models.ColumnModel sourceColumn in sourceColumns)
                        {
                            string strColumnName = sourceColumn.ColumnName;
                            DatabaseTools.Models.ColumnModel targetColumn = (
                                from c in targetTable.Columns
                                where c.ColumnName.Equals(strColumnName, StringComparison.InvariantCultureIgnoreCase)
                                select c).FirstOrDefault();

                            if (targetColumn != null && !targetColumn.IsComputed)
                            {

                                if (targetColumn.IsIdentity)
                                {
                                    blnContainsIdentity = true;
                                }

                                targetColumns.Add(targetColumn);

                                if (sbColumns.Length > 0)
                                {
                                    sbColumns.Append(", ");
                                }
                                sbColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
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
                                        paramater.Size = sourceColumn.Precision;
                                        break;
                                    case System.Data.DbType.Time:
                                        if ((paramater) is System.Data.SqlClient.SqlParameter)
                                        {
                                            ((System.Data.SqlClient.SqlParameter)paramater).SqlDbType = System.Data.SqlDbType.Time;
                                        }
                                        break;
                                }

                                sbParamaters.Append(paramater.ParameterName);

                                targetCommand.Parameters.Add(paramater);

                            }
                            else
                            {
                                sourceColumns.Remove(sourceColumn);
                            }
                        }

                        targetCommand.CommandText = this.FormatCommandText(string.Format("INSERT INTO [{0}] ({1}) VALUES ({2})", targetTable.TableName, sbColumns.ToString(), sbParamaters.ToString()), targetDatabaseType);

                        if (blnContainsIdentity)
                        {
                            targetCommand.CommandText = this.FormatCommandText(string.Format("SET IDENTITY_INSERT [{0}] ON;" + Environment.NewLine + targetCommand.CommandText + Environment.NewLine + "SET IDENTITY_INSERT {0} OFF;", targetTable.TableName), targetDatabaseType);
                        }

                        using (System.Data.Common.DbConnection sourceConnection = DatabaseTools.Processes.Database.CreateDbConnection(sourceFactory, sourceConnectionString))
                        {
                            var intRowCount = this.GetRowCount(sourceConnection, targetTable.TableName, sourceDatabaseType);

                            using (System.Data.Common.DbCommand sourceCommand = DatabaseTools.Processes.Database.CreateDbCommand(sourceConnection))
                            {
                                sourceCommand.CommandText = this.FormatCommandText(string.Format("SELECT COUNT(*) FROM [{1}]", sbColumns.ToString(), sourceTable.TableName), sourceDatabaseType);

                                Int64 intRowIndex = 0L;

                                if (intRowCount > 0L)
                                {
                                    sourceCommand.CommandText = this.FormatCommandText(string.Format("SELECT {0} FROM [{1}]", sbColumns.ToString(), sourceTable.TableName), sourceDatabaseType);

                                    System.Data.Common.DbDataReader sourceReader = sourceCommand.ExecuteReader();

                                    while (sourceReader.Read())
                                    {

                                        for (int intIndex = 0; intIndex < targetCommand.Parameters.Count; intIndex++)
                                        {
                                            var parameter = targetCommand.Parameters[intIndex];

                                            parameter.Value = DBNull.Value;

                                            object objValue = DBNull.Value;

                                            try
                                            {
                                                objValue = sourceReader.GetValue(intIndex);
                                            }
                                            catch (MySql.Data.Types.MySqlConversionException ex)
                                            {
                                                if (parameter.DbType == System.Data.DbType.DateTime2)
                                                {
                                                    objValue = DateTime.MinValue;
                                                }
                                                else if (parameter.DbType == System.Data.DbType.DateTime)
                                                {
                                                    objValue = DateTime.MinValue;
                                                }
                                                else
                                                {
                                                    throw;
                                                }
                                            }

                                            var sourceColumn = sourceColumns[intIndex];


                                            if (System.Convert.IsDBNull(objValue))
                                            {
                                                parameter.Value = DBNull.Value;
                                            }
                                            else
                                            {
                                                switch (sourceColumn.DbType)
                                                {
                                                    case System.Data.DbType.Date:
                                                    case System.Data.DbType.DateTime:

                                                        try
                                                        {
                                                            DateTime dt = System.Convert.ToDateTime(objValue);

                                                            if (dt <= new DateTime(1753, 1, 1))
                                                            {
                                                                parameter.Value = new DateTime(1753, 1, 1);
                                                            }
                                                            else if (dt > new DateTime(9999, 12, 31))
                                                            {
                                                                parameter.Value = new DateTime(9999, 12, 31);
                                                            }
                                                            else
                                                            {
                                                                parameter.Value = dt;
                                                            }
                                                        }
                                                        catch
                                                        {
                                                            parameter.Value = new DateTime(1753, 1, 1);
                                                        }
                                                        break;
                                                    case System.Data.DbType.DateTime2:
                                                        try
                                                        {
                                                            DateTime dt = System.Convert.ToDateTime(objValue);
                                                            parameter.Value = dt;
                                                        }
                                                        catch
                                                        {
                                                            parameter.Value = DateTime.MinValue;
                                                        }
                                                        break;
                                                    case System.Data.DbType.Time:
                                                        {
                                                            if ((objValue) is TimeSpan)
                                                            {
                                                                parameter.Value = (new DateTime(1753, 1, 1)).Add((TimeSpan)objValue);
                                                            }
                                                            else if ((objValue) is DateTime)
                                                            {
                                                                DateTime dt = System.Convert.ToDateTime(objValue);

                                                                if (dt.Year <= 300 && dt.Year >= 200)
                                                                {
                                                                    int newYear = dt.Year * 10;
                                                                    dt = dt.AddYears(newYear - dt.Year);
                                                                }

                                                                if (dt <= new DateTime(1753, 1, 1))
                                                                {
                                                                    parameter.Value = DBNull.Value;
                                                                }
                                                                else if (dt > new DateTime(9999, 12, 31))
                                                                {
                                                                    parameter.Value = DBNull.Value;
                                                                }
                                                                else
                                                                {
                                                                    parameter.Value = dt;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                parameter.Value = objValue;
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
                                                                parameter.Value = System.Convert.ToString(objValue).TrimEnd();
                                                            }
                                                            else
                                                            {
                                                                parameter.Value = System.Convert.ToString(objValue);
                                                            }
                                                            break;
                                                        }
                                                    default:
                                                        {
                                                            parameter.Value = objValue;
                                                            break;
                                                        }
                                                }
                                            }

                                        }

                                        intRowIndex += 1L;

                                        try
                                        {
                                            targetCommand.ExecuteNonQuery();
                                            
                                            int intNewProgress = System.Convert.ToInt32(intRowIndex / (double)intRowCount * 100);

                                            if (intProgress != intNewProgress)
                                            {
                                                intProgress = intNewProgress;
                                                worker.ReportProgress(intProgress, sourceTable.TableName);
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            string strRowErrorMessage = this.GetRowErrorMessage(targetCommand, targetColumns, targetCommand.Parameters.Count - 1, ex);

                                            string strErrorMessage = string.Format("could not insert row on table: {0} at row: {1}", sourceTable.TableName, strRowErrorMessage);

                                            int intNewProgress = System.Convert.ToInt32(intRowIndex / (double)intRowCount * 100);

                                            intProgress = intNewProgress;

                                            worker.ReportProgress(intProgress, string.Concat(sourceTable.TableName, "|", strErrorMessage));

                                        }

                                    }
                                }

                            }
                        }
                    }
                }

            }

            worker.ReportProgress(100, sourceTable.TableName);
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

        private Int64 GetRowCount(System.Data.Common.DbConnection connection, string tableName, DatabaseTools.Models.DatabaseType databaseType)
        {
            long lngRowCount = 0;

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
                            command.CommandText = this.FormatCommandText(string.Format("SELECT COUNT(*) FROM [{0}]", tableName), databaseType);
                            break;
                    }
                    lngRowCount = System.Convert.ToInt64(command.ExecuteScalar());
                }
            }
            catch
            {

            }

            return lngRowCount;
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
