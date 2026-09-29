using System.Collections.Generic;
using System.Linq;
using LordKuper.Common;
using RimWorld;

namespace LordKuper.WorkManager.Tests;

/// <summary>
///     Tests for the per-work-type needs filter on <see cref="WorkTypeAssignmentRule" />: validation, shipped and new
///     rule defaults, inheritance through <see cref="WorkTypeAssignmentRule.Combine" />, evaluation through the static
///     IsNeedBlocked seam, and the add-need menu contents.
/// </summary>
[TestFixture]
[NonParallelizable]
public class NeedsFilterTests : StateIsolationTestBase
{
    private FakeDefProvider _defProvider = null!;
    private IDefProvider _originalDefProvider = null!;

    /// <summary>
    ///     Builds a rule pair the way the game combines them: a freshly created work-type rule and the shipped default
    ///     rule as the fallback.
    /// </summary>
    private static (WorkTypeAssignmentRule Main, WorkTypeAssignmentRule Fallback) CreateCombinablePair()
    {
        var main = WorkTypeAssignmentRule.CreateRule("Mining");
        var fallback = WorkTypeAssignmentRule.DefaultRules.First(r => r.DefName == null);
        return (main, fallback);
    }

    /// <summary>
    ///     Creates a need def with a label derived from its def name and registers it with the fake def provider.
    /// </summary>
    private NeedDef RegisterNeed(string defName, int listPriority = 0)
    {
        var def = new NeedDef { defName = defName, label = $"{defName} (test)", listPriority = listPriority };
        _defProvider.Register(def);
        return def;
    }

    /// <summary>
    ///     Installs a fresh fake def provider so need defs resolve without the game's def database.
    /// </summary>
    [SetUp]
    public void InstallFakeDefProvider()
    {
        _originalDefProvider = DefProvider.Current;
        _defProvider = new FakeDefProvider();
        DefProvider.Current = _defProvider;
    }

    /// <summary>
    ///     Restores the def provider that was current before the test.
    /// </summary>
    [TearDown]
    public void RestoreDefProvider()
    {
        DefProvider.Current = _originalDefProvider;
    }

    // ==================== ValidateNeedsFilter ====================

    [Test]
    public void ValidateNeedsFilter_ThresholdBelowZero_Clamped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = [new NeedLimit("Hunger") { Threshold = -0.5f }]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits![0].Threshold.Should().Be(0f);
    }

    [Test]
    public void ValidateNeedsFilter_ThresholdAboveOne_Clamped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = [new NeedLimit("Hunger") { Threshold = 1.5f }]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits![0].Threshold.Should().Be(1f);
    }

    [TestCase(0f)]
    [TestCase(0.37f)]
    [TestCase(1f)]
    public void ValidateNeedsFilter_ThresholdInRange_Unchanged(float threshold)
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = [new NeedLimit("Hunger") { Threshold = threshold }]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits![0].Threshold.Should().Be(threshold);
    }

    [Test]
    public void ValidateNeedsFilter_DuplicateDefNames_KeepFirst()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits =
            [
                new NeedLimit("Hunger") { Threshold = 0.3f },
                new NeedLimit("Hunger") { Threshold = 0.7f },
                new NeedLimit("Beauty") { Threshold = 0.4f }
            ]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits![0].DefName.Should().Be("Hunger");
        rule.NeedLimits[0].Threshold.Should().Be(0.3f);
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    [Test]
    public void ValidateNeedsFilter_DuplicateDefNames_CaseInsensitive()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits =
            [
                new NeedLimit("Hunger") { Threshold = 0.3f },
                new NeedLimit("HUNGER") { Threshold = 0.7f },
                new NeedLimit("Beauty") { Threshold = 0.4f }
            ]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits![0].DefName.Should().Be("Hunger");
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    [Test]
    public void ValidateNeedsFilter_NullAndEmptyEntries_Dropped()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits =
            [
                new NeedLimit("Hunger") { Threshold = 0.3f },
                null!,
                new NeedLimit("") { Threshold = 0.5f },
                new NeedLimit("Beauty") { Threshold = 0.4f }
            ]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().HaveCount(2);
        rule.NeedLimits![0].DefName.Should().Be("Hunger");
        rule.NeedLimits[1].DefName.Should().Be("Beauty");
    }

    [Test]
    public void ValidateNeedsFilter_UnresolvedDefName_Kept()
    {
        var rule = new WorkTypeAssignmentRule("Mining")
        {
            NeedLimits = [new NeedLimit("NeedFromRemovedMod") { Threshold = 0.4f }]
        };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().ContainSingle().Which.DefName.Should().Be("NeedFromRemovedMod");
    }

    [Test]
    public void ValidateNeedsFilter_DefaultRuleNullState_NormalisedToOff()
    {
        var rule = new WorkTypeAssignmentRule(null) { FilterNeeds = null };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().Be(false);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ValidateNeedsFilter_DefaultRuleExplicitState_Kept(bool state)
    {
        var rule = new WorkTypeAssignmentRule(null) { FilterNeeds = state };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().Be(state);
    }

    [Test]
    public void ValidateNeedsFilter_WorkTypeRuleNullState_RemainsInherit()
    {
        var rule = new WorkTypeAssignmentRule("Mining") { FilterNeeds = null };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().BeNull();
    }

    [Test]
    public void ValidateNeedsFilter_NullNeedLimits_Initialised()
    {
        var rule = new WorkTypeAssignmentRule("Mining") { NeedLimits = null };

        rule.ValidateNeedsFilter();

        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void ValidateNeedsFilter_OlderSettingsWithBothFieldsNull_Normalised()
    {
        // Settings saved before the needs filter existed load with both fields null.
        var rule = new WorkTypeAssignmentRule(null) { FilterNeeds = null, NeedLimits = null };

        rule.ValidateNeedsFilter();

        rule.FilterNeeds.Should().Be(false);
        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    // ==================== Shipped and newly created rules ====================

    [Test]
    public void DefaultRules_DefaultRuleNoEntries()
    {
        var defaultRule = WorkTypeAssignmentRule.DefaultRules.First(r => r.DefName == null);

        defaultRule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void DefaultRules_DefaultRuleFilterNeedsOff()
    {
        var defaultRule = WorkTypeAssignmentRule.DefaultRules.First(r => r.DefName == null);

        defaultRule.FilterNeeds.Should().Be(false);
    }

    [Test]
    public void DefaultRules_NoRuleHasNeedsOnOrEntries()
    {
        foreach (var rule in WorkTypeAssignmentRule.DefaultRules)
        {
            rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
            rule.FilterNeeds.Should().NotBe(true);
        }
    }

    [Test]
    public void CreateRule_WorkTypeRule_InheritAndEmpty()
    {
        var rule = WorkTypeAssignmentRule.CreateRule("Mining");

        rule.FilterNeeds.Should().BeNull();
        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void CreateRule_DefaultRule_OffAndEmpty()
    {
        var rule = WorkTypeAssignmentRule.CreateRule(null);

        rule.FilterNeeds.Should().Be(false);
        rule.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    // ==================== Combine ====================

    [TestCase(true)]
    [TestCase(false)]
    public void Combine_MainInherit_TakesFallbackState(bool fallbackState)
    {
        var (main, fallback) = CreateCombinablePair();
        fallback.FilterNeeds = fallbackState;

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(fallbackState);
    }

    [Test]
    public void Combine_MainInherit_TakesFallbackEntries()
    {
        var (main, fallback) = CreateCombinablePair();
        var hunger = new NeedLimit("Hunger") { Threshold = 0.5f };
        var rest = new NeedLimit("Rest") { Threshold = 0.2f };
        main.NeedLimits = [new NeedLimit("Beauty") { Threshold = 0.9f }];
        fallback.FilterNeeds = true;
        fallback.NeedLimits = [hunger, rest];

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.NeedLimits.Should().Equal(hunger, rest);
    }

    [Test]
    public void Combine_MainInherit_CopiesFallbackList()
    {
        var (main, fallback) = CreateCombinablePair();
        fallback.FilterNeeds = true;
        fallback.NeedLimits = [new NeedLimit("Hunger")];

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);
        combined.NeedLimits!.Add(new NeedLimit("Rest"));

        combined.NeedLimits.Should().NotBeSameAs(fallback.NeedLimits);
        fallback.NeedLimits.Should().ContainSingle();
    }

    [Test]
    public void Combine_MainInheritFallbackListNull_EmptyEntries()
    {
        var (main, fallback) = CreateCombinablePair();
        fallback.NeedLimits = null;

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.NeedLimits.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void Combine_MainOff_IgnoresFallback()
    {
        var (main, fallback) = CreateCombinablePair();
        main.FilterNeeds = false;
        fallback.FilterNeeds = true;
        fallback.NeedLimits = [new NeedLimit("Hunger") { Threshold = 0.5f }];

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(false);
        combined.NeedLimits.Should().BeEmpty();
    }

    [Test]
    public void Combine_MainOn_UsesMainEntriesOnly()
    {
        var (main, fallback) = CreateCombinablePair();
        var hunger = new NeedLimit("Hunger") { Threshold = 0.3f };
        main.FilterNeeds = true;
        main.NeedLimits = [hunger];
        fallback.FilterNeeds = true;
        fallback.NeedLimits = [new NeedLimit("Beauty") { Threshold = 0.5f }];

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(true);
        combined.NeedLimits.Should().Equal(hunger);
        combined.NeedLimits.Should().NotBeSameAs(main.NeedLimits);
    }

    [Test]
    public void Combine_MainOnWithNoEntries_DoesNotInheritFallbackEntries()
    {
        var (main, fallback) = CreateCombinablePair();
        main.FilterNeeds = true;
        fallback.FilterNeeds = true;
        fallback.NeedLimits = [new NeedLimit("Hunger") { Threshold = 0.5f }];

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(true);
        combined.NeedLimits.Should().BeEmpty();
    }

    // ==================== IsNeedBlocked ====================

    [Test]
    public void IsNeedBlocked_StateOff_NeverBlocks()
    {
        var hunger = RegisterNeed("Hunger");
        var limits = new List<NeedLimit> { new(hunger) { Threshold = 0.5f } };

        WorkTypeAssignmentRule.IsNeedBlocked(false, limits, _ => 0.1f).Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_StateInherit_NeverBlocks()
    {
        var hunger = RegisterNeed("Hunger");
        var limits = new List<NeedLimit> { new(hunger) { Threshold = 0.5f } };

        WorkTypeAssignmentRule.IsNeedBlocked(null, limits, _ => 0.1f).Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_StateOnNoEntries_NeverBlocks()
    {
        WorkTypeAssignmentRule.IsNeedBlocked(true, [], _ => 0f).Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_StateOnNullList_NeverBlocks()
    {
        WorkTypeAssignmentRule.IsNeedBlocked(true, null, _ => 0f).Should().BeFalse();
    }

    [TestCase(0.49f, true)]
    [TestCase(0.5f, false)]
    [TestCase(0.51f, false)]
    [TestCase(0f, true)]
    public void IsNeedBlocked_SingleEntry_BlocksOnlyWhenStrictlyBelowThreshold(float level, bool expected)
    {
        var hunger = RegisterNeed("Hunger");
        var limits = new List<NeedLimit> { new(hunger) { Threshold = 0.5f } };

        WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => level).Should().Be(expected);
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    public void IsNeedBlocked_ZeroThreshold_NeverBlocks(float level)
    {
        var hunger = RegisterNeed("Hunger");
        var limits = new List<NeedLimit> { new(hunger) { Threshold = 0f } };

        WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => level).Should().BeFalse();
    }

    [TestCase(0.99f, true)]
    [TestCase(1f, false)]
    public void IsNeedBlocked_FullThreshold_PassesOnlyAtFullLevel(float level, bool expected)
    {
        var hunger = RegisterNeed("Hunger");
        var limits = new List<NeedLimit> { new(hunger) { Threshold = 1f } };

        WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => level).Should().Be(expected);
    }

    [Test]
    public void IsNeedBlocked_PawnLacksListedNeed_Passes()
    {
        var hunger = RegisterNeed("Hunger");
        var rest = RegisterNeed("Rest");
        var levels = new Dictionary<NeedDef, float> { [rest] = 0.9f };
        var limits = new List<NeedLimit>
        {
            new(hunger) { Threshold = 0.5f },
            new(rest) { Threshold = 0.5f }
        };

        var blocked = WorkTypeAssignmentRule.IsNeedBlocked(true, limits,
            def => levels.TryGetValue(def, out var level) ? level : null);

        blocked.Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_PawnWithoutNeeds_Passes()
    {
        var hunger = RegisterNeed("Hunger");
        var rest = RegisterNeed("Rest");
        var limits = new List<NeedLimit>
        {
            new(hunger) { Threshold = 0.5f },
            new(rest) { Threshold = 1f }
        };

        WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => null).Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_UnresolvedEntry_Skipped()
    {
        var limits = new List<NeedLimit> { new("NeedFromRemovedMod") { Threshold = 1f } };
        var lookups = 0;

        var blocked = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ =>
        {
            lookups++;
            return 0f;
        });

        blocked.Should().BeFalse();
        lookups.Should().Be(0);
    }

    [Test]
    public void IsNeedBlocked_UnresolvedEntryBeforeBlockingEntry_StillBlocks()
    {
        var rest = RegisterNeed("Rest");
        var limits = new List<NeedLimit>
        {
            new("NeedFromRemovedMod") { Threshold = 1f },
            new(rest) { Threshold = 0.5f }
        };

        WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => 0.2f).Should().BeTrue();
    }

    [Test]
    public void IsNeedBlocked_AnyOneEntryBelowThreshold_Blocks()
    {
        var hunger = RegisterNeed("Hunger");
        var beauty = RegisterNeed("Beauty");
        var joy = RegisterNeed("Joy");
        var levels = new Dictionary<NeedDef, float> { [hunger] = 0.9f, [beauty] = 0.4f, [joy] = 0.9f };
        var limits = new List<NeedLimit>
        {
            new(hunger) { Threshold = 0.5f },
            new(beauty) { Threshold = 0.6f },
            new(joy) { Threshold = 0.5f }
        };

        var blocked = WorkTypeAssignmentRule.IsNeedBlocked(true, limits,
            def => levels.TryGetValue(def, out var level) ? level : null);

        blocked.Should().BeTrue();
    }

    [Test]
    public void IsNeedBlocked_AllEntriesAtOrAboveThreshold_Passes()
    {
        var hunger = RegisterNeed("Hunger");
        var beauty = RegisterNeed("Beauty");
        var levels = new Dictionary<NeedDef, float> { [hunger] = 0.5f, [beauty] = 0.8f };
        var limits = new List<NeedLimit>
        {
            new(hunger) { Threshold = 0.5f },
            new(beauty) { Threshold = 0.6f }
        };

        var blocked = WorkTypeAssignmentRule.IsNeedBlocked(true, limits,
            def => levels.TryGetValue(def, out var level) ? level : null);

        blocked.Should().BeFalse();
    }

    [Test]
    public void IsNeedBlocked_RepeatedCalls_ReflectCurrentLevels()
    {
        var rest = RegisterNeed("Rest");
        var limits = new List<NeedLimit> { new(rest) { Threshold = 0.5f } };
        var level = 0.2f;

        var firstVerdict = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => level);
        level = 0.8f;
        var secondVerdict = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, _ => level);

        firstVerdict.Should().BeTrue();
        secondVerdict.Should().BeFalse();
    }

    // ==================== GetAddableNeeds ====================

    [Test]
    public void GetAddableNeeds_NoNeeds_Empty()
    {
        Settings.GetAddableNeeds([], []).Should().BeEmpty();
    }

    [Test]
    public void GetAddableNeeds_ExcludesPlayerMechsOnly()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var mechEnergy = new NeedDef { defName = "MechEnergy", label = "energy", playerMechsOnly = true };

        var result = Settings.GetAddableNeeds([hunger, mechEnergy], []);

        result.Should().Equal(hunger);
    }

    [Test]
    public void GetAddableNeeds_ExcludesAuthority()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var authority = new NeedDef { defName = "Authority", label = "authority" };

        var result = Settings.GetAddableNeeds([hunger, authority], []);

        result.Should().Equal(hunger);
    }

    [Test]
    public void GetAddableNeeds_IncludesNeedsHiddenFromNeedList()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var hidden = new NeedDef { defName = "HiddenNeed", label = "hidden", showOnNeedList = false };

        var result = Settings.GetAddableNeeds([hunger, hidden], []);

        result.Should().Contain(hidden);
    }

    [Test]
    public void GetAddableNeeds_ExcludesAlreadyAdded()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var beauty = new NeedDef { defName = "Beauty", label = "beauty" };

        var result = Settings.GetAddableNeeds([hunger, beauty], [new NeedLimit("Hunger")]);

        result.Should().Equal(beauty);
    }

    [Test]
    public void GetAddableNeeds_ExcludesAlreadyAdded_CaseInsensitive()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var beauty = new NeedDef { defName = "Beauty", label = "beauty" };

        var result = Settings.GetAddableNeeds([hunger, beauty], [new NeedLimit("HUNGER")]);

        result.Should().Equal(beauty);
    }

    [Test]
    public void GetAddableNeeds_AllAlreadyAdded_Empty()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };

        var result = Settings.GetAddableNeeds([hunger], [new NeedLimit("Hunger")]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetAddableNeeds_NullExistingEntry_Ignored()
    {
        var hunger = new NeedDef { defName = "Hunger", label = "hunger" };
        var beauty = new NeedDef { defName = "Beauty", label = "beauty" };

        var result = Settings.GetAddableNeeds([hunger, beauty], [null!, new NeedLimit("Hunger")]);

        result.Should().Equal(beauty);
    }

    [Test]
    public void GetAddableNeeds_SortedByListPriorityDescendingThenLabel()
    {
        var high = new NeedDef { defName = "ZebraHigh", label = "zebra", listPriority = 100 };
        var appleLow = new NeedDef { defName = "AppleLow", label = "apple", listPriority = 50 };
        var bananaLow = new NeedDef { defName = "BananaLow", label = "banana", listPriority = 50 };
        var anotherLow = new NeedDef { defName = "AnotherLow", label = "another", listPriority = 50 };
        var lowest = new NeedDef { defName = "Aardvark", label = "aardvark", listPriority = 0 };

        var result = Settings.GetAddableNeeds([lowest, appleLow, high, bananaLow, anotherLow], []);

        result.Should().Equal(high, anotherLow, appleLow, bananaLow, lowest);
    }
}
