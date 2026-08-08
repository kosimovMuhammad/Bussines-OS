using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Tests.Unit;

public class ConditionTreeEvaluatorTests
{
    private static WorkflowCondition Cond(string field, ConditionOperator op, string? value) => new() { Field = field, Operator = op, Value = value };

    [Fact]
    public void Evaluate_NoGroups_ReturnsTrue()
    {
        Assert.True(ConditionTreeEvaluator.Evaluate([], new Dictionary<string, string?>()));
    }

    [Fact]
    public void Evaluate_AndGroup_AllMustMatch()
    {
        var group = new WorkflowConditionGroup { Id = "g1", LogicOperator = ConditionLogicOperator.And };
        group.Conditions.Add(Cond("Stage", ConditionOperator.Equals, "Won"));
        group.Conditions.Add(Cond("Value", ConditionOperator.GreaterThan, "1000"));

        var values = new Dictionary<string, string?> { ["Stage"] = "Won", ["Value"] = "500" };
        Assert.False(ConditionTreeEvaluator.Evaluate([group], values));

        values["Value"] = "1500";
        Assert.True(ConditionTreeEvaluator.Evaluate([group], values));
    }

    [Fact]
    public void Evaluate_OrGroup_AnyMayMatch()
    {
        var group = new WorkflowConditionGroup { Id = "g1", LogicOperator = ConditionLogicOperator.Or };
        group.Conditions.Add(Cond("Priority", ConditionOperator.Equals, "High"));
        group.Conditions.Add(Cond("Value", ConditionOperator.GreaterThan, "10000"));

        var values = new Dictionary<string, string?> { ["Priority"] = "Low", ["Value"] = "50000" };
        Assert.True(ConditionTreeEvaluator.Evaluate([group], values));

        values["Value"] = "5";
        Assert.False(ConditionTreeEvaluator.Evaluate([group], values));
    }

    [Fact]
    public void Evaluate_NestedGroups_CombinesCorrectly()
    {
        // Root (AND): Stage == Won  AND  (child OR: Priority == High  OR  Value > 10000)
        var root = new WorkflowConditionGroup { Id = "root", LogicOperator = ConditionLogicOperator.And };
        root.Conditions.Add(Cond("Stage", ConditionOperator.Equals, "Won"));

        var child = new WorkflowConditionGroup { Id = "child", ParentGroupId = "root", LogicOperator = ConditionLogicOperator.Or };
        child.Conditions.Add(Cond("Priority", ConditionOperator.Equals, "High"));
        child.Conditions.Add(Cond("Value", ConditionOperator.GreaterThan, "10000"));

        var allGroups = new List<WorkflowConditionGroup> { root, child };

        var matching = new Dictionary<string, string?> { ["Stage"] = "Won", ["Priority"] = "Low", ["Value"] = "50000" };
        Assert.True(ConditionTreeEvaluator.Evaluate(allGroups, matching));

        var wrongStage = new Dictionary<string, string?> { ["Stage"] = "Lead", ["Priority"] = "High", ["Value"] = "50000" };
        Assert.False(ConditionTreeEvaluator.Evaluate(allGroups, wrongStage));

        var neitherChildBranch = new Dictionary<string, string?> { ["Stage"] = "Won", ["Priority"] = "Low", ["Value"] = "5" };
        Assert.False(ConditionTreeEvaluator.Evaluate(allGroups, neitherChildBranch));
    }

    [Theory]
    [InlineData(ConditionOperator.Equals, "Won", "Won", true)]
    [InlineData(ConditionOperator.Equals, "Won", "Lead", false)]
    [InlineData(ConditionOperator.NotEquals, "Won", "Lead", true)]
    [InlineData(ConditionOperator.Contains, "acme", "This is Acme Corp", true)]
    [InlineData(ConditionOperator.IsEmpty, "", "", true)]
    [InlineData(ConditionOperator.IsEmpty, "x", "x", false)]
    [InlineData(ConditionOperator.IsNotEmpty, "x", "x", true)]
    public void EvaluateCondition_Operators_ProduceExpectedResult(ConditionOperator op, string expected, string actual, bool result)
    {
        var condition = Cond("Field", op, expected);
        var values = new Dictionary<string, string?> { ["Field"] = actual };
        Assert.Equal(result, ConditionTreeEvaluator.EvaluateCondition(condition, values));
    }

    [Fact]
    public void EvaluateCondition_MissingField_TreatedAsNull()
    {
        var condition = Cond("Missing", ConditionOperator.IsEmpty, null);
        Assert.True(ConditionTreeEvaluator.EvaluateCondition(condition, new Dictionary<string, string?>()));
    }
}
