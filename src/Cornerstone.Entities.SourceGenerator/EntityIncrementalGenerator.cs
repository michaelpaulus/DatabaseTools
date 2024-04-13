using System.Collections.Immutable;
using System.Text;
using Cornerstone.Database.Models;
using Microsoft.CodeAnalysis;

namespace Cornerstone.Entities.SourceGenerator;

[Generator]
public class EntityIncrementalGenerator : IIncrementalGenerator
{

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyNames = context.CompilationProvider.Select((c, _) => c.AssemblyName);

        var files = context.AdditionalTextsProvider
            .Where(a => a.Path.EndsWith(".sql.json"))
            .Select((a, c) => (a.Path, a.GetText(c)!.ToString()))
            .Collect();

        var result = assemblyNames.Combine(files);

        context.RegisterImplementationSourceOutput(result, Generate);
    }

    void Generate(SourceProductionContext context, (string assemblyName, ImmutableArray<(string FileName, string Text)> files) result)
    {
        var assemblyName = result.assemblyName;

        try
        {

            var databaseModel = DatabaseModel.CreateFromFiles(result.files);

            var pkColumns = new List<ColumnModel>();

            foreach (var table in databaseModel.Tables)
            {
                var ns = assemblyName;
                var className = table.TableName;

                var pk = databaseModel.PrimaryKeys.FirstOrDefault(i => i.TableName == table.TableName && i.SchemaName == table.SchemaName);

                var sb = new StringBuilder();
                sb.AppendLine(@$"using Cornerstone.Entities;
#nullable enable
namespace {ns};
public record {className} : IEntity<{className}>
{{
");
                foreach (var column in table.Columns)
                {
                    var propertyName = NormalizeColumnName(column.ColumnName);
                    var propertyType = ColumnModel.GetSystemTypeString(ColumnModel.GetSystemType(column.DbType));
                    var dft = "";

                    if (pk is not null)
                    {
                        var pkColumn = pk.Columns.FirstOrDefault(i => i.ColumnName == column.ColumnName);
                        if (pkColumn is not null)
                        {
                            pkColumns.Add(column);
                            propertyType = NormalizeColumnName(column.ColumnName);
                        }
                    }

                    if (propertyName == className)
                    {
                        propertyName += "_Id";
                    }

                    if (column.IsNullable)
                    {
                        propertyType += "?";
                    }
                    else
                    {
                        dft = $" = {GetTypeDefault(propertyType)};";
                    }

                    sb.AppendLine($"    public {propertyType} {propertyName} {{ get; set; }}{dft}");
                }

                sb.AppendLine(@$"
}}
");
                context.AddSource($"{ns}.{className}.g", sb.ToString());
            }

            foreach (var column in pkColumns.GroupBy(i => i.ColumnName))
            {
                var ns = assemblyName;
                var className = NormalizeColumnName(column.Key);
                var propertyType = ColumnModel.GetSystemTypeString(ColumnModel.GetSystemType(column.First().DbType));

                var sb = new StringBuilder();
                sb.AppendLine(@$"using Cornerstone.Entities;
#nullable enable
namespace {ns};
public record struct {className}({propertyType} Value = {GetTypeDefault(propertyType)}) : IEntityId, IComparable<{className}>
{{
    public int CompareTo({className} other)
    {{
        return Value.CompareTo(other.Value);
    }}
}}
");
                context.AddSource($"{ns}.{className}.g", sb.ToString());
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }


    }

    private string NormalizeColumnName(string columnName)
    {
        columnName = columnName.Replace(" ", "");

        if (columnName.Contains("ID"))
        {
            var index = columnName.IndexOf("ID");
            if (index > 0)
            {
                var previous = columnName.Substring(index - 1, 1);
                if (previous.Equals(previous.ToLower()))
                {
                    var first = columnName.Substring(0, index);
                    var last = "";
                    if (index > columnName.Length)
                    {
                        last = columnName.Substring(index + 3);
                    }
                    columnName = $"{first}Id{last}";

                }
            }
        }

        return columnName;
    }

    private string GetTypeDefault(string propertyType)
    {
        if (propertyType == "string")
        {
            return "\"\"";
        }
        else if (propertyType == "int")
        {
            return "0";
        }
        else if (propertyType == "long")
        {
            return "0";
        }
        else if (propertyType == "short")
        {
            return "0";
        }
        return "default";
    }

}
