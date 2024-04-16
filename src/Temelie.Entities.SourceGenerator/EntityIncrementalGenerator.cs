using System.Collections.Immutable;
using System.Text;
using Temelie.Database.Models;
using Microsoft.CodeAnalysis;

namespace Temelie.Entities.SourceGenerator;

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

            void addTable(TableModel table)
            {
                var ns = assemblyName;
                var className = table.ClassName;

                var sb = new StringBuilder();
                sb.AppendLine(@$"using Temelie.Entities;
#nullable enable
namespace {ns};
public record {className} : IEntity<{className}>
{{
");
                foreach (var column in table.Columns)
                {
                    var propertyName = column.PropertyName;
                    var propertyType = ColumnModel.GetSystemTypeString(ColumnModel.GetSystemType(column.DbType));
                    var dft = "";

                    var fkSouceColumn = databaseModel.GetForeignKeySourceColumn(table.TableName, column.ColumnName);
                    if (fkSouceColumn is not null)
                    {
                        propertyType = fkSouceColumn.PropertyName;
                    }
                    else if (column.IsPrimaryKey)
                    {
                        propertyType = column.PropertyName;
                        pkColumns.Add(column);
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

            foreach (var table in databaseModel.Tables)
            {
                addTable(table);
            }

            foreach (var table in databaseModel.Views)
            {
                addTable(table);
            }

            foreach (var group in pkColumns.GroupBy(i => i.PropertyName))
            {
                var ns = assemblyName;
                var className = group.Key;
                var propertyType = ColumnModel.GetSystemTypeString(ColumnModel.GetSystemType(group.First().DbType));

                var sb = new StringBuilder();
                sb.AppendLine(@$"using Temelie.Entities;
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
