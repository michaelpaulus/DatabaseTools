using System.Text;

namespace Cornerstone.Database.Models;

public class SecurityPolicyModel : DatabaseObjectModel
{
    public string PolicySchema { get; set; }
    public string PolicyName { get; set; }
    public IList<SecurityPolicyPredicate> Predicates { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsSchemaBound { get; set; }

    public void AppendCreateScript(System.Text.StringBuilder sb, string quoteCharacterStart, string quoteCharacterEnd, bool includeIfNotExists)
    {
        if (sb.Length > 0)
        {
            sb.AppendLine();
        }
        if (includeIfNotExists)
        {
            sb.AppendLine($@"IF NOT EXISTS
    (
        SELECT
            1
        FROM
            sys.security_policies INNER JOIN
            sys.schemas ON
                schemas.schema_id = security_policies.schema_id
        WHERE
            schemas.name = '{this.PolicySchema}' AND
            security_policies.name = '{this.PolicyName}'
    )");
        }
        sb.AppendLine($"    CREATE SECURITY POLICY {quoteCharacterStart}{PolicySchema}{quoteCharacterEnd}.{quoteCharacterStart}{PolicyName}{quoteCharacterEnd}");
        var predicateScripts = new List<StringBuilder>();
        foreach (var predicate in Predicates)
        {
            var predicateSql = new StringBuilder();
            predicateSql.AppendLine($"    ADD {predicate.PredicateType} PREDICATE {predicate.PredicateDefinition.Substring(1, predicate.PredicateDefinition.Length - 2)}");
            predicateSql.Append($"    ON {quoteCharacterStart}{predicate.TargetSchema}{quoteCharacterEnd}.{quoteCharacterStart}{predicate.TargetName}{quoteCharacterEnd}");
            if (predicate.Operation != null && predicate.Operation.Length > 0)
            {
                predicateSql.AppendLine();
                predicateSql.Append(predicate.Operation);
            }
            predicateScripts.Add(predicateSql);
        }
        sb.AppendLine(string.Join(",", predicateScripts));
        var suffixes = new List<string>();

        if (!IsEnabled)
        {
            suffixes.Add("STATE = OFF");
        }

        if (!IsSchemaBound)
        {
            suffixes.Add("SCHEMABINDING = OFF");
        }

        if (suffixes.Count > 0)
        {
            sb.AppendLine("    WITH (" + string.Join(", ", suffixes) + ")");
        }

        sb.AppendLine();
        sb.AppendLine("GO");
    }

    public override void AppendCreateScript(StringBuilder sb, string quoteCharacterStart, string quoteCharacterEnd)
    {
        this.AppendCreateScript(sb, quoteCharacterStart, quoteCharacterEnd, true);
    }

    public override void AppendDropScript(StringBuilder sb, string quoteCharacterStart, string quoteCharacterEnd)
    {
        sb.AppendLine($@"IF EXISTS
    (
        SELECT
            1
        FROM
            sys.security_policies INNER JOIN
            sys.schemas ON
                schemas.schema_id = security_policies.schema_id
        WHERE
            schemas.name = '{this.PolicySchema}' AND
            security_policies.name = '{this.PolicyName}'
    )");
        sb.AppendLine($"    DROP SECURITY POLICY {quoteCharacterStart}{PolicySchema}{quoteCharacterEnd}.{quoteCharacterStart}{PolicyName}{quoteCharacterEnd}");
        sb.AppendLine("GO");
    }
}
