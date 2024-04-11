﻿using System.Data;
using System.Data.Common;
using Cornerstone.Database.Models;

namespace Cornerstone.Database.Services;
public interface ITableConverterService
{
    void Convert(IProgress<TableProgress> progress, TableModel sourceTable, ConnectionStringModel sourceConnectionString, TableModel targetTable, ConnectionStringModel targetConnectionString, bool trimStrings, Action<IDbConnection> connectionCreatedCallback = null);
    void ConvertBulk(IProgress<TableProgress> progress, TableModel sourceTable, ConnectionStringModel sourceConnectionString, TableModel targetTable, ConnectionStringModel targetConnectionString, bool trimStrings, int batchSize, bool useTransaction = true);
    void ConvertBulk(IProgress<TableProgress> progress, TableModel sourceTable, IDataReader sourceReader, int sourceRowCount, TableModel targetTable, ConnectionStringModel targetConnectionString, bool trimStrings, int batchSize, bool useTransaction = true, bool validateTargetTable = true);
    void ConvertTable(TableConverterSettings settings, TableModel sourceTable, IProgress<TableProgress> progress);
    void ConvertTable(TableConverterSettings settings, TableModel sourceTable, TableModel targetTable, IProgress<TableProgress> progress, bool throwOnFailure = false);
    void ConvertTables(TableConverterSettings settings, IProgress<TableProgress> progress, int maxDegreeOfParallelism);
    IList<ColumnModel> GetMatchedColumns(IList<ColumnModel> sourceColumns, IList<ColumnModel> targetColumns);
    int GetRowCount(TableModel table, DbConnection connection);
}