using JetBrains.Annotations;
using LordKuper.Common.Cache;
using RimWorld;
using Verse;

namespace LordKuper.WorkManager;

/// <summary>
///     A threshold for a single <see cref="NeedDef" />: a pawn whose level for the need is strictly below
///     <see cref="Threshold" /> is not assigned the work type. Only the def name and the threshold are persisted.
/// </summary>
internal sealed class NeedLimit : DefCache<NeedDef>, IExposable
{
    /// <summary>
    ///     The threshold given to a newly added need limit (50%).
    /// </summary>
    internal const float DefaultThreshold = 0.5f;

    /// <summary>
    ///     The minimum need level, as a fraction in [0, 1] of the need's maximum, below which the work type is blocked.
    /// </summary>
    public float Threshold = DefaultThreshold;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NeedLimit" /> class for deserialization.
    /// </summary>
    [UsedImplicitly]
    public NeedLimit() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NeedLimit" /> class for the need with the specified def name.
    /// </summary>
    /// <param name="defName">The def name of the need.</param>
    public NeedLimit(string defName) : base(defName) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NeedLimit" /> class for the specified need.
    /// </summary>
    /// <param name="def">The need definition.</param>
    public NeedLimit(NeedDef def) : base(GetDefName(def)) { }

    /// <summary>
    ///     Serializes the def name and the threshold.
    /// </summary>
    public new void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Threshold, nameof(Threshold), DefaultThreshold);
    }
}
