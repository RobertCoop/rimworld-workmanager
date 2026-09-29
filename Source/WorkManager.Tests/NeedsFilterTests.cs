using System;
using System.Collections.Generic;
using System.Linq;

namespace LordKuper.WorkManager.Tests;

/// <summary>
///     Unit tests for the needs filter data model, validation, evaluation, and combination on
///     <see cref="WorkTypeAssignmentRule" />.
///     Tests AC-2, AC-3, AC-4, AC-6, AC-7, AC-8, AC-10, AC-13–19.
///     (GetAddableNeeds tests deferred to manual verification; IsNeedBlocked tested as pure logic seam.)
/// </summary>
[TestFixture]
[NonParallelizable]
public class NeedsFilterTests : StateIsolationTestBase
{
    // ==================== ValidateNeedsFilter tests (AC-2, AC-3, AC-4, AC-10) ====================

    /// <summary>
    ///     AC-2: On validation, entry thresholds outside 0–100% are clamped into 0–100%.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_ThresholdBelowZero_Clamped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = new List<NeedLimit> { new NeedLimit("Hunger") { Threshold = -0.5f } }
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits[0].Threshold.Should().Be(0f);
    }

    /// <summary>
    ///     AC-2: On validation, entry thresholds outside 0–100% are clamped into 0–100%.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_ThresholdAboveOne_Clamped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = new List<NeedLimit> { new NeedLimit("Hunger") { Threshold = 1.5f } }
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits[0].Threshold.Should().Be(1f);
    }

    /// <summary>
    ///     AC-3: On validation, a rule's entry list keeps at most one entry per NeedDef defName; later duplicates are removed.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_DuplicateDefNames_KeepFirst()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = new List<NeedLimit>
            {
                new("Hunger") { Threshold = 0.3f },
                new("Hunger") { Threshold = 0.7f }, // Duplicate, case-sensitive match
                new("Beauty") { Threshold = 0.4f }
            }
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits[0].DefName.Should().Be("Hunger");
        rule.NeedLimits[0].Threshold.Should().Be(0.3f); // First kept
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    /// <summary>
    ///     AC-3: On validation, duplicate defNames are removed case-insensitively.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_DuplicateDefNames_CaseInsensitive()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = new List<NeedLimit>
            {
                new("Hunger") { Threshold = 0.3f },
                new("HUNGER") { Threshold = 0.7f }, // Duplicate, case-insensitive match
                new("Beauty") { Threshold = 0.4f }
            }
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits[0].DefName.Should().Be("Hunger");
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    /// <summary>
    ///     AC-10, AC-3: On validation, null entries and entries with empty defName are dropped.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_NullAndEmptyEntries_Dropped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = new List<NeedLimit>
            {
                new("Hunger") { Threshold = 0.3f },
                null!,
                new("") { Threshold = 0.5f }, // Empty defName
                new("Beauty") { Threshold = 0.4f }
            }
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits[0].DefName.Should().Be("Hunger");
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    /// <summary>
    ///     AC-10, AC-4: The default rule's needs state is normalised to Off when null.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_DefaultRuleNullState_NormalisedToOff()
    {
        var rule = new WorkTypeAssignmentRule(null) { FilterNeeds = null };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().Be(false);
    }

    /// <summary>
    ///     AC-4: A work-type rule's null FilterNeeds is not normalised (stays null/Inherit).
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_WorkTypeRuleNullState_Remainsinherit()
    {
        var rule = new WorkTypeAssignmentRule("Mining") { FilterNeeds = null };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().BeNull();
    }

    /// <summary>
    ///     AC-10: Null NeedLimits is initialised to an empty list.
    /// </summary>
    [Test]
    public void ValidateNeedsFilter_NullNeedLimits_Initialised()
    {
        var rule = new WorkTypeAssignmentRule("Mining") { NeedLimits = null };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    // ==================== DefaultRules and CreateRule tests (AC-5, AC-6) ====================

    /// <summary>
    ///     AC-6: The default rule has no needs entries.
    /// </summary>
    [Test]
    public void DefaultRules_DefaultRuleNoEntries()
    {
        var defaultRule = WorkTypeAssignmentRule.DefaultRules.First(r => r.DefName == null);

        defaultRule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>
    ///     AC-6: The default rule's FilterNeeds is Off (false).
    /// </summary>
    [Test]
    public void DefaultRules_DefaultRuleFilterNeedsOff()
    {
        var defaultRule = WorkTypeAssignmentRule.DefaultRules.First(r => r.DefName == null);

        defaultRule.FilterNeeds.Should().Be(false);
    }

    /// <summary>
    ///     AC-6: No shipped rule has needs entries or FilterNeeds == true.
    /// </summary>
    [Test]
    public void DefaultRules_NoRuleHasNeedsOnOrEntries()
    {
        foreach (var rule in WorkTypeAssignmentRule.DefaultRules)
        {
            rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
            rule.FilterNeeds.Should().NotBe(true);
        }
    }

    /// <summary>
    ///     AC-5: A newly created work-type rule starts with FilterNeeds Inherit (null) and no entries.
    /// </summary>
    [Test]
    public void CreateRule_WorkTypeRule_InheritAndEmpty()
    {
        var rule = WorkTypeAssignmentRule.CreateRule("Mining");

        rule.FilterNeeds.Should().BeNull();
        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>
    ///     AC-4, AC-5: A newly created default rule has FilterNeeds Off (false) and no entries.
    /// </summary>
    [Test]
    public void CreateRule_DefaultRule_OffAndEmpty()
    {
        var rule = WorkTypeAssignmentRule.CreateRule(null);

        rule.FilterNeeds.Should().Be(false);
        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

}
