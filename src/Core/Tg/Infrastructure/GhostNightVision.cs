using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>DV174: the observer's and the corpse's virtual worn goggles use tg's night-vision item action and client colour.</summary>
public static class GhostNightVision
{
    private sealed class States
    {
        internal readonly ConditionalWeakTable<mob, datum_action_item_action_toggle_nv> Actions = new();
    }

    public static async DmTask Ensure(mob owner)
    {
        var states = World.Current.GetExtension(static _ => new States());
        if (states.Actions.TryGetValue(owner, out var existing))
        {
            Sync(existing);
            return;
        }
        // A corpse's goggles stay in nullspace: in its contents they would be dropped with its items.
        var goggles = Dm.@new<obj_item_clothing_glasses_night>(owner is mob_dead_observer ? owner : null);
        // The goggles are live since gaps CL1: their Initialize already made their own toggle (actions_types), and a second one would
        // attach update_icon_updates_onmob twice (tg's "overridden" stack trace). Take theirs; make one only if it is missing.
        datum_action_item_action_toggle_nv action = null;
        foreach (object a in Dm.each(goggles.actions))
            if (a is datum_action_item_action_toggle_nv own) { action = own; break; }
        action ??= Dm.@new<datum_action_item_action_toggle_nv>(goggles);
        action.check_flags = 0; // An observer or a corpse has no conscious body or hands to check.
        action.stored_cutoffs = goggles.color_cutoffs;
        action.stored_colour = goggles.glass_colour_type;
        goggles.color_cutoffs = Dm.list();
        goggles.glass_colour_type = null;
        goggles.icon_state = "night_off";
        owner.lighting_cutoff = 0;
        owner.lighting_color_cutoffs = Dm.list();
        states.Actions.Add(owner, action);
        await action.Grant(owner);
    }

    /// <summary>Takes the goggles back from a revived body; its own update_sight restores its eyes' cutoffs.</summary>
    public static async DmTask Remove(mob owner)
    {
        var states = World.Current.GetExtension(static _ => new States());
        if (!states.Actions.TryGetValue(owner, out var action)) return;
        states.Actions.Remove(owner);
        owner.remove_client_colour(action);
        await action.Remove(owner);
        GlobalProcs.qdel(action.target);
        GlobalProcs.qdel(action);
    }

    /// <summary>Reapplies the goggles' cutoffs to <paramref name="owner"/>, or none when it has no goggles.</summary>
    public static void Refresh(mob owner)
    {
        var states = World.Current.GetExtension(static _ => new States());
        if (states.Actions.TryGetValue(owner, out var action))
        {
            Sync(action);
            return;
        }
        owner.lighting_cutoff = 0;
        owner.lighting_color_cutoffs = Dm.list();
    }

    public static void Sync(datum_action_item_action_toggle_nv action)
    {
        if (action.owner is not mob owner || action.target is not obj_item_clothing_glasses goggles) return;
        var states = World.Current.GetExtension(static _ => new States());
        if (!states.Actions.TryGetValue(owner, out var granted) || granted != action) return;
        bool enabled = goggles.color_cutoffs?.Count > 0;
        owner.lighting_cutoff = 0;
        owner.lighting_color_cutoffs = goggles.color_cutoffs?.Copy() ?? Dm.list();
        if (enabled)
            owner.add_client_colour(goggles.glass_colour_type, action);
        else
            owner.remove_client_colour(action);
        goggles.icon_state = enabled ? "night" : "night_off";
    }
}
