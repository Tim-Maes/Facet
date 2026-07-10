using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Generators.Fluent;

/// <summary>A state transition: from the state with canon <see cref="FromCanon"/>, one more <c>With{Nav}</c> application.</summary>
internal sealed class ChainEdge
{
    public ChainEdge(string fromCanon, ChainNode added)
    {
        FromCanon = fromCanon;
        Added = added;
    }

    public string FromCanon { get; }
    public ChainNode Added { get; }
}

/// <summary>Everything to emit for one entity, derived from base surface + discovered chains.</summary>
internal sealed class EntityPlan
{
    public EntityPlan(FluentEntityModel model)
    {
        Model = model;
        States[string.Empty] = ImmutableArray<ChainNode>.Empty;
        MarkerStates[string.Empty] = ImmutableArray<ChainNode>.Empty;
    }

    public FluentEntityModel Model { get; }

    /// <summary>Query states by canon; value is the merged set-tree. Always contains the bare state.</summary>
    public Dictionary<string, ImmutableArray<ChainNode>> States { get; } = new(StringComparer.Ordinal);

    /// <summary>Query transitions, deduplicated by (from, added).</summary>
    public List<ChainEdge> Edges { get; } = new();

    /// <summary>Marker states on THIS entity (it is the target of someone's nested lambda).</summary>
    public Dictionary<string, ImmutableArray<ChainNode>> MarkerStates { get; } = new(StringComparer.Ordinal);

    public List<ChainEdge> MarkerEdges { get; } = new();

    /// <summary>
    /// Top-level nav SETS (2+, sorted canon of nav names joined by ',') needing a composed
    /// interface declaration — and its nominal implementation on the concrete shape class.
    /// </summary>
    public Dictionary<string, ImmutableArray<string>> ComposedSets { get; } = new(StringComparer.Ordinal);

    private readonly HashSet<string> _edgeKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _markerEdgeKeys = new(StringComparer.Ordinal);

    public void AddEdge(ImmutableArray<ChainNode> fromState, ChainNode added, out ImmutableArray<ChainNode> toState)
    {
        var fromCanon = ChainCanon.Canon(fromState);
        toState = MergeInto(fromState, added);
        var toCanon = ChainCanon.Canon(toState);
        if (!States.ContainsKey(fromCanon)) States.Add(fromCanon, fromState);
        if (!States.ContainsKey(toCanon)) States.Add(toCanon, toState);
        RegisterComposed(toState);

        if (_edgeKeys.Add(fromCanon + "|" + ChainCanon.Canon(ImmutableArray.Create(added))))
        {
            Edges.Add(new ChainEdge(fromCanon, added));
        }
    }

    public void AddMarkerEdge(ImmutableArray<ChainNode> fromState, ChainNode added, out ImmutableArray<ChainNode> toState)
    {
        var fromCanon = ChainCanon.Canon(fromState);
        toState = MergeInto(fromState, added);
        if (!MarkerStates.ContainsKey(fromCanon)) MarkerStates.Add(fromCanon, fromState);
        var toCanon = ChainCanon.Canon(toState);
        if (!MarkerStates.ContainsKey(toCanon)) MarkerStates.Add(toCanon, toState);

        if (_markerEdgeKeys.Add(fromCanon + "|" + ChainCanon.Canon(ImmutableArray.Create(added))))
        {
            MarkerEdges.Add(new ChainEdge(fromCanon, added));
        }
    }

    public void RegisterComposed(ImmutableArray<ChainNode> state)
    {
        if (state.Length < 2) return;
        var navs = ChainCanon.Sort(state).Select(n => n.Nav).ToImmutableArray();
        var key = string.Join(",", navs);
        if (!ComposedSets.ContainsKey(key)) ComposedSets.Add(key, navs);
    }

    /// <summary>Set-merge: an existing node with the same nav unions children recursively; a new nav appends.</summary>
    public static ImmutableArray<ChainNode> MergeInto(ImmutableArray<ChainNode> state, ChainNode added)
    {
        for (var i = 0; i < state.Length; i++)
        {
            if (state[i].Nav != added.Nav) continue;
            var merged = state[i];
            foreach (var child in added.Children)
            {
                merged = new ChainNode(merged.Nav, MergeInto(merged.Children, child));
            }

            return state.SetItem(i, merged);
        }

        return state.Add(added);
    }
}

/// <summary>
/// Builds per-entity emission plans: the always-emitted base surface (bare state, one
/// single-step edge and marker method per navigation — what makes the API discoverable
/// before any chain exists) plus everything the discovered chains require, validated
/// against the models so an unknown navigation contributes nothing (the missing method is
/// then an ordinary compile error at the call site).
/// </summary>
internal static class ChainPlanner
{
    public sealed class Result
    {
        public Result(ImmutableDictionary<string, EntityPlan> plans, ImmutableArray<DiscoveredChain> cappedChains)
        {
            Plans = plans;
            CappedChains = cappedChains;
        }

        public ImmutableDictionary<string, EntityPlan> Plans { get; }

        /// <summary>Chains that exceeded the depth cap (for FAC121), pruned in the plans.</summary>
        public ImmutableArray<DiscoveredChain> CappedChains { get; }
    }

    public static Result Build(
        ImmutableDictionary<string, FluentEntityModel> models,
        ImmutableArray<DiscoveredChain> chains,
        int maxChainDepth)
    {
        var plans = models.ToDictionary(
            m => m.Key,
            m => new EntityPlan(m.Value),
            StringComparer.Ordinal);

        // Base surface: one bare single-step edge + marker method per navigation.
        foreach (var plan in plans.Values)
        {
            foreach (var nav in plan.Model.Navs)
            {
                var node = new ChainNode(nav.Name, ImmutableArray<ChainNode>.Empty);
                plan.AddEdge(ImmutableArray<ChainNode>.Empty, node, out _);
                plan.AddMarkerEdge(ImmutableArray<ChainNode>.Empty, node, out _);
            }
        }

        var bySimpleName = models.Values
            .GroupBy(m => m.SimpleName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var capped = ImmutableArray.CreateBuilder<DiscoveredChain>();

        foreach (var chain in chains)
        {
            if (!bySimpleName.TryGetValue(chain.EntitySimpleName, out var candidates)) continue;

            var wasCapped = false;
            foreach (var model in candidates)
            {
                var plan = plans[model.ClrName];
                var state = ImmutableArray<ChainNode>.Empty;
                foreach (var step in chain.Steps)
                {
                    var nav = model.FindNav(step.Nav);
                    if (nav == null) continue; // unknown nav: the With method won't exist; compile error at call site

                    var node = Normalize(step, nav, models, 1, maxChainDepth, ref wasCapped);
                    plan.AddEdge(state, node, out state);
                    RegisterMarkers(plans, models, nav, node);
                }
            }

            if (wasCapped) capped.Add(chain);
        }

        return new Result(plans.ToImmutableDictionary(StringComparer.Ordinal), capped.ToImmutable());
    }

    /// <summary>
    /// Validates a step's nested children against the target entity's model and prunes
    /// anything past the depth cap. Depth counts nesting: a bare step is 1, Items(Product) is 2.
    /// </summary>
    private static ChainNode Normalize(
        ChainNode step,
        FluentNav nav,
        ImmutableDictionary<string, FluentEntityModel> models,
        int depth,
        int maxDepth,
        ref bool wasCapped)
    {
        if (step.Children.Length == 0) return new ChainNode(step.Nav, ImmutableArray<ChainNode>.Empty);
        if (depth >= maxDepth)
        {
            wasCapped = true;
            return new ChainNode(step.Nav, ImmutableArray<ChainNode>.Empty);
        }

        if (!models.TryGetValue(nav.TargetClrName, out var target))
        {
            return new ChainNode(step.Nav, ImmutableArray<ChainNode>.Empty);
        }

        var children = ImmutableArray.CreateBuilder<ChainNode>();
        foreach (var child in step.Children)
        {
            var childNav = target.FindNav(child.Nav);
            if (childNav == null) continue;
            children.Add(Normalize(child, childNav, models, depth + 1, maxDepth, ref wasCapped));
        }

        return new ChainNode(step.Nav, children.ToImmutable());
    }

    /// <summary>
    /// A node with children means a marker lambda was written on the nav's target: register
    /// the marker states/edges (in written order) on the target entity, its composed-set
    /// requirements, and recurse for deeper lambdas.
    /// </summary>
    private static void RegisterMarkers(
        Dictionary<string, EntityPlan> plans,
        ImmutableDictionary<string, FluentEntityModel> models,
        FluentNav nav,
        ChainNode node)
    {
        if (node.Children.Length == 0) return;
        if (!plans.TryGetValue(nav.TargetClrName, out var targetPlan)) return;

        var state = ImmutableArray<ChainNode>.Empty;
        foreach (var child in node.Children)
        {
            targetPlan.AddMarkerEdge(state, child, out state);
            var childNav = targetPlan.Model.FindNav(child.Nav);
            if (childNav != null) RegisterMarkers(plans, models, childNav, child);
        }

        // The nested state's interface view (the type argument the parent's capability
        // interface is instantiated with) must be a declared interface of the target's
        // concrete shape class, so multi-nav nested sets need the composed declaration.
        targetPlan.RegisterComposed(state);
    }
}
