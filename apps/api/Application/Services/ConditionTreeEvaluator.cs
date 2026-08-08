using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

// Evaluates a Workflow's condition tree against the triggering record's CURRENT field
// values (not the values captured at trigger time - see the Automation Engine Phase 3
// plan's explicit note on this). Pure/static and stateless so it's trivially testable.
public static class ConditionTreeEvaluator
{
    // Top-level groups (no ParentGroupId) are implicitly AND'd together - the Create API
    // accepts a list of root groups, and every one of them must hold.
    public static bool Evaluate(IReadOnlyList<WorkflowConditionGroup> allGroups, IReadOnlyDictionary<string, string?> fieldValues)
    {
        var byParent = allGroups.ToLookup(g => g.ParentGroupId);
        var roots = byParent[null].ToList();

        return roots.Count == 0 || roots.All(g => EvaluateGroup(g, byParent, fieldValues));
    }

    public static bool EvaluateCondition(WorkflowCondition condition, IReadOnlyDictionary<string, string?> fieldValues)
    {
        fieldValues.TryGetValue(condition.Field, out var actual);

        return condition.Operator switch
        {
            ConditionOperator.Equals => string.Equals(actual, condition.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(actual, condition.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => actual is not null && condition.Value is not null &&
                actual.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.IsEmpty => string.IsNullOrEmpty(actual),
            ConditionOperator.IsNotEmpty => !string.IsNullOrEmpty(actual),
            ConditionOperator.GreaterThan => TryCompare(actual, condition.Value, out var gt) && gt > 0,
            ConditionOperator.LessThan => TryCompare(actual, condition.Value, out var lt) && lt < 0,
            _ => false,
        };
    }

    private static bool EvaluateGroup(WorkflowConditionGroup group, ILookup<string?, WorkflowConditionGroup> byParent, IReadOnlyDictionary<string, string?> fieldValues)
    {
        var results = group.Conditions.Select(c => EvaluateCondition(c, fieldValues))
            .Concat(byParent[group.Id].Select(child => EvaluateGroup(child, byParent, fieldValues)))
            .ToList();

        if (results.Count == 0)
        {
            return true;
        }

        return group.LogicOperator == ConditionLogicOperator.And ? results.All(r => r) : results.Any(r => r);
    }

    private static bool TryCompare(string? actual, string? expected, out int comparison)
    {
        if (decimal.TryParse(actual, out var actualNumber) && decimal.TryParse(expected, out var expectedNumber))
        {
            comparison = actualNumber.CompareTo(expectedNumber);
            return true;
        }

        if (DateTime.TryParse(actual, out var actualDate) && DateTime.TryParse(expected, out var expectedDate))
        {
            comparison = actualDate.CompareTo(expectedDate);
            return true;
        }

        comparison = 0;
        return false;
    }
}
