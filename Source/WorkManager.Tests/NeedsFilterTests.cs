using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using LordKuper.Common;
using RimWorld;
using Verse;

namespace LordKuper.WorkManager.Tests;

/// <summary>
///     Unit tests for the needs filter data model, validation, evaluation, and combination on
///     <see cref="WorkTypeAssignmentRule" />.
///     Tests AC-2, AC-3, AC-4, AC-6, AC-7, AC-8, AC-10, AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-19 and AC-30.
/// </summary>
[TestFixture]
[NonParallelizable]
public class NeedsFilterTests : StateIsolationTestBase
{
    /// <summary>
    ///     A test-only implementation of <see cref="IDefProvider" /> backed by a dictionary.
    ///     Used to isolate unit tests from the game runtime's global def database.
    /// </summary>
    private class FakeDefProvider : IDefProvider
    {
        private readonly Dictionary<string, NeedDef> _needDefs = new(StringComparer.Ordinal);

        /// <summary>
        ///     Registers a need definition in this provider.
        /// </summary>
        public void RegisterNeed(NeedDef def)
        {
            _needDefs[def.defName] = def;
        }

        /// <summary>
        ///     Retrieves a need definition by name, or null if not found.
        /// </summary>
        public T? GetNamedSilentFail<T>(string? defName) where T : Def
        {
            if (string.IsNullOrEmpty(defName))
                return null;
            if (typeof(T) == typeof(NeedDef) && defName != null && _needDefs.TryGetValue(defName, out var def))
                return (T)(object)def;
            return null;
        }

        /// <summary>
        ///     Not used by the needs filter tests; required by IDefProvider.
        /// </summary>
        public IEnumerable<T> AllDefs<T>() where T : Def
        {
            if (typeof(T) == typeof(NeedDef))
                return (IEnumerable<T>)(object)_needDefs.Values;
            return [];
        }

        /// <summary>
        ///     Not used by the needs filter tests; required by IDefProvider.
        /// </summary>
        public IReadOnlyList<T> AllDefsListForReading<T>() where T : Def
        {
            if (typeof(T) == typeof(NeedDef))
                return (IReadOnlyList<T>)(object)_needDefs.Values.ToList();
            return [];
        }

        /// <summary>
        ///     Not used by the needs filter tests; required by IDefProvider.
        /// </summary>
        public IReadOnlyList<WorkTypeDef> WorkTypeDefsInPriorityOrder()
        {
            return [];
        }
    }

    private FakeDefProvider? _fakeDefProvider;
    private IDefProvider? _originalDefProvider;

    /// <summary>
    ///     Sets up the fake def provider before each test.
    /// </summary>
    [SetUp]
    public new void SnapshotState()
    {
        base.SnapshotState();
        _originalDefProvider = DefProvider.Current;
        _fakeDefProvider = new FakeDefProvider();
        DefProvider.Current = _fakeDefProvider;
    }

    /// <summary>
    ///     Restores the original def provider after each test.
    /// </summary>
    [TearDown]
    public new void RestoreState()
    {
        if (_originalDefProvider != null)
            DefProvider.Current = _originalDefProvider;
        base.RestoreState();
    }

    /// <summary>
    ///     Creates a need definition with the specified defName and optional list priority.
    /// </summary>
    private static NeedDef CreateNeedDef(string defName, int listPriority = 0)
    {
        var def = new NeedDef
        {
            defName = defName,
            label = $"{defName} (test)",
            listPriority = listPriority,
            playerMechsOnly = false
        };
        return def;
    }

    // ==================== IsNeedBlocked tests (AC-13…AC-19) ====================

    /// <summary>
    ///     AC-19: With needs state Off, the filter never blocks.
    /// </summary>
    [Test]
    public void IsNeedBlocked_StateOff_NeverBlocks()
    {
        var limits = new List<NeedLimit> { new NeedLimit("Hunger") { Threshold = 0.5f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.3f); // Below threshold

        var result = WorkTypeAssignmentRule.IsNeedBlocked(false, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-19: With needs state On but no entries, the filter never blocks.
    /// </summary>
    [Test]
    public void IsNeedBlocked_StateOnNoEntries_NeverBlocks()
    {
        var limits = new List<NeedLimit>();
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.3f);

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-19: With needs state On but list is null, the filter never blocks.
    /// </summary>
    [Test]
    public void IsNeedBlocked_StateOnNullList_NeverBlocks()
    {
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.3f);

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, null, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-13, AC-14: With state On, a pawn is blocked when level is strictly below threshold,
    ///     but equal passes.
    /// </summary>
    [Test]
    public void IsNeedBlocked_LevelBelowThreshold_Blocks()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var limits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.49f); // Strictly below

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeTrue();
    }

    /// <summary>
    ///     AC-14: A need level exactly equal to the threshold does not block.
    /// </summary>
    [Test]
    public void IsNeedBlocked_LevelEqualToThreshold_DoesNotBlock()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var limits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.5f); // Equal

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-15: An entry with threshold 0% never blocks.
    /// </summary>
    [Test]
    public void IsNeedBlocked_ZeroThreshold_NeverBlocks()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var limits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0f); // Even 0% level

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-16: An entry for a need the pawn does not have passes (null level).
    /// </summary>
    [Test]
    public void IsNeedBlocked_PawnLacksNeed_Passes()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var limits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => null); // Pawn lacks the need

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-17: A pawn with no needs tracker (all null levels) passes all entries.
    /// </summary>
    [Test]
    public void IsNeedBlocked_AllNullLevels_Passes()
    {
        var hungerDef = CreateNeedDef("Hunger");
        var beautyDef = CreateNeedDef("Beauty");
        _fakeDefProvider!.RegisterNeed(hungerDef);
        _fakeDefProvider.RegisterNeed(beautyDef);

        var limits = new[]
        {
            new NeedLimit(hungerDef) { Threshold = 0.5f },
            new NeedLimit(beautyDef) { Threshold = 0.3f }
        };
        var getLevelPercentage = new Func<NeedDef, float?>(def => null); // No needs tracker

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

    /// <summary>
    ///     AC-18: An entry whose def is unresolved (null) is skipped at evaluation.
    /// </summary>
    [Test]
    public void IsNeedBlocked_UnresolvedEntry_Skipped()
    {
        // Create a limit with an unresolved def name (not registered)
        var limit = new NeedLimit("UnknownNeed") { Threshold = 0.5f };
        var limits = new[] { limit };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.2f); // Would block if resolved

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeFalse(); // Unresolved entry is skipped
    }

    /// <summary>
    ///     AC-13: With state On and multiple entries, any entry blocking causes the overall result to block.
    /// </summary>
    [Test]
    public void IsNeedBlocked_MultipleEntries_AnyEntryBlocks()
    {
        var hungerDef = CreateNeedDef("Hunger");
        var beautyDef = CreateNeedDef("Beauty");
        _fakeDefProvider!.RegisterNeed(hungerDef);
        _fakeDefProvider.RegisterNeed(beautyDef);

        var limits = new[]
        {
            new NeedLimit(hungerDef) { Threshold = 0.5f },
            new NeedLimit(beautyDef) { Threshold = 0.6f }
        };
        var getLevelPercentage = new Func<NeedDef, float?>(def =>
            def.defName == "Hunger" ? 0.7f : 0.4f); // Beauty is below its threshold

        var result = WorkTypeAssignmentRule.IsNeedBlocked(true, limits, getLevelPercentage);

        result.Should().BeTrue(); // Beauty blocks
    }

    /// <summary>
    ///     AC-13: With state Inherit (null), the filter never blocks.
    /// </summary>
    [Test]
    public void IsNeedBlocked_StateInherit_NeverBlocks()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var limits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } };
        var getLevelPercentage = new Func<NeedDef, float?>(def => 0.3f);

        var result = WorkTypeAssignmentRule.IsNeedBlocked(null, limits, getLevelPercentage);

        result.Should().BeFalse();
    }

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

    // ==================== Combine tests (AC-7, AC-8) ====================

    /// <summary>
    ///     AC-7: When a work-type rule's FilterNeeds is Inherit (null), combined rule takes the default rule's state and a copy of its entries.
    /// </summary>
    [Test]
    public void Combine_MainInherit_TakesDefaultState()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var main = new WorkTypeAssignmentRule("Mining") { FilterNeeds = null };
        var fallback = new WorkTypeAssignmentRule(null)
        {
            FilterNeeds = true,
            NeedLimits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } }
        };

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(true);
        combined.NeedLimits.Should().HaveCount(1);
        combined.NeedLimits[0].DefName.Should().Be("Hunger");
        combined.NeedLimits[0].Threshold.Should().Be(0.5f);
        // Should be a different list instance (shallow copy)
        combined.NeedLimits.Should().NotBeSameAs(fallback.NeedLimits);
    }

    /// <summary>
    ///     AC-8: When a work-type rule's FilterNeeds is On or Off, combined rule takes that rule's own state and entries.
    /// </summary>
    [Test]
    public void Combine_MainExplicitOff_IgnoresFallback()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var main = new WorkTypeAssignmentRule("Mining")
        {
            FilterNeeds = false,
            NeedLimits = []
        };
        var fallback = new WorkTypeAssignmentRule(null)
        {
            FilterNeeds = true,
            NeedLimits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } }
        };

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(false);
        combined.NeedLimits.Should().BeEmpty();
    }

    /// <summary>
    ///     AC-8: When a work-type rule's FilterNeeds is On, combined rule takes that rule's entries.
    /// </summary>
    [Test]
    public void Combine_MainExplicitOn_UsesMainEntries()
    {
        var hungerDef = CreateNeedDef("Hunger");
        var beautyDef = CreateNeedDef("Beauty");
        _fakeDefProvider!.RegisterNeed(hungerDef);
        _fakeDefProvider.RegisterNeed(beautyDef);

        var main = new WorkTypeAssignmentRule("Mining")
        {
            FilterNeeds = true,
            NeedLimits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.3f } }
        };
        var fallback = new WorkTypeAssignmentRule(null)
        {
            FilterNeeds = true,
            NeedLimits = new List<NeedLimit> { new NeedLimit(beautyDef) { Threshold = 0.5f } }
        };

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.FilterNeeds.Should().Be(true);
        combined.NeedLimits.Should().HaveCount(1);
        combined.NeedLimits[0].DefName.Should().Be("Hunger");
    }

    /// <summary>
    ///     AC-7: Combine produces a shallow copy of the entries list, not a shared reference.
    /// </summary>
    [Test]
    public void Combine_ProducesDifferentListInstance()
    {
        var hungerDef = CreateNeedDef("Hunger");
        _fakeDefProvider!.RegisterNeed(hungerDef);

        var main = new WorkTypeAssignmentRule("Mining") { FilterNeeds = null };
        var fallback = new WorkTypeAssignmentRule(null)
        {
            FilterNeeds = true,
            NeedLimits = new List<NeedLimit> { new NeedLimit(hungerDef) { Threshold = 0.5f } }
        };

        var combined = WorkTypeAssignmentRule.Combine(main, fallback);

        combined.NeedLimits.Should().NotBeSameAs(fallback.NeedLimits);
    }

    // ==================== GetAddableNeeds tests (AC-30) ====================

    /// <summary>
    ///     AC-30: GetAddableNeeds excludes needs with playerMechsOnly set.
    /// </summary>
    [Test]
    public void GetAddableNeeds_ExcludesPlayerMechsOnly()
    {
        var normalNeed = CreateNeedDef("Hunger");
        var mechOnlyNeed = new NeedDef
        {
            defName = "MechEnergy",
            label = "Mech Energy",
            playerMechsOnly = true,
            listPriority = 0
        };

        var allNeeds = new[] { normalNeed, mechOnlyNeed };
        var existing = new List<NeedLimit>();

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().NotContain(n => n.defName == "MechEnergy");
        result.Should().Contain(n => n.defName == "Hunger");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds excludes the Authority need.
    /// </summary>
    [Test]
    public void GetAddableNeeds_ExcludesAuthority()
    {
        var normalNeed = CreateNeedDef("Hunger");
        var authorityNeed = CreateNeedDef("Authority");

        var allNeeds = new[] { normalNeed, authorityNeed };
        var existing = new List<NeedLimit>();

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().NotContain(n => n.defName == "Authority");
        result.Should().Contain(n => n.defName == "Hunger");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds excludes needs already in the list (case-insensitive).
    /// </summary>
    [Test]
    public void GetAddableNeeds_ExcludesAlreadyAdded()
    {
        var hungerNeed = CreateNeedDef("Hunger");
        var beautyNeed = CreateNeedDef("Beauty");

        var allNeeds = new[] { hungerNeed, beautyNeed };
        var existing = new List<NeedLimit> { new("Hunger") }; // Hunger already added

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().NotContain(n => n.defName == "Hunger");
        result.Should().Contain(n => n.defName == "Beauty");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds includes needs with showOnNeedList = false.
    /// </summary>
    [Test]
    public void GetAddableNeeds_IncludesHiddenNeeds()
    {
        var normalNeed = CreateNeedDef("Hunger");
        var hiddenNeed = new NeedDef
        {
            defName = "Mood",
            label = "Mood",
            showOnNeedList = false,
            listPriority = 0
        };

        var allNeeds = new[] { normalNeed, hiddenNeed };
        var existing = new List<NeedLimit>();

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().Contain(n => n.defName == "Mood");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds sorts by listPriority (highest first), then by label.
    /// </summary>
    [Test]
    public void GetAddableNeeds_SortedByPriorityThenLabel()
    {
        var needHigh = CreateNeedDef("ZebraHigh", listPriority: 100);
        var needLow = CreateNeedDef("AppleLow", listPriority: 50);
        var needSamePriorityA = CreateNeedDef("BananaName", listPriority: 50);
        var needSamePriorityB = CreateNeedDef("AnotherName", listPriority: 50);

        var allNeeds = new[] { needLow, needHigh, needSamePriorityA, needSamePriorityB };
        var existing = new List<NeedLimit>();

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().HaveCount(4);
        // ZebraHigh should be first (highest priority)
        result[0].defName.Should().Be("ZebraHigh");
        // Then sorted by label among those with priority 50
        result[1].defName.Should().Be("AnotherName"); // "AnotherName (test)" < "AppleLow (test)" < "BananaName (test)"
        result[2].defName.Should().Be("AppleLow");
        result[3].defName.Should().Be("BananaName");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds handles null entries in the existing list gracefully.
    /// </summary>
    [Test]
    public void GetAddableNeeds_HandlesNullExistingEntries()
    {
        var hungerNeed = CreateNeedDef("Hunger");
        var beautyNeed = CreateNeedDef("Beauty");

        var allNeeds = new[] { hungerNeed, beautyNeed };
        var existing = new List<NeedLimit> { null!, new("Hunger") }; // Null entry

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().NotContain(n => n.defName == "Hunger");
        result.Should().Contain(n => n.defName == "Beauty");
    }

    /// <summary>
    ///     AC-30: GetAddableNeeds excludes already-added needs case-insensitively.
    /// </summary>
    [Test]
    public void GetAddableNeeds_ExcludesAlreadyAdded_CaseInsensitive()
    {
        var hungerNeed = CreateNeedDef("Hunger");
        var beautyNeed = CreateNeedDef("Beauty");

        var allNeeds = new[] { hungerNeed, beautyNeed };
        var existing = new List<NeedLimit> { new("HUNGER") }; // Different case

        var result = Settings.GetAddableNeeds(allNeeds, existing);

        result.Should().NotContain(n => n.defName == "Hunger");
        result.Should().Contain(n => n.defName == "Beauty");
    }
}
