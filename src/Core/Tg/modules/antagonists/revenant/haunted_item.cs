// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/revenant/haunted_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the haunted item component (a floating item that throws itself at its victims until a holy tool
// dispels it) and haunt_outburst (the ectoplasmic anomaly's outburst). The var block is generated (D111).

/// Makes our item SUPER spooky!
/// Adds the haunted element and some other bonuses
// tg: code/modules/antagonists/revenant/haunted_item.dm:3
public partial class datum_component_haunted_item : datum_component
{
    // tg: code/modules/antagonists/revenant/haunted_item.dm:69  var/static/list/default_dispell_types (a proc-level static, rules 10.6)
    private static ref DmList Initialize__default_dispell_types => ref TgState.Current.datum_component_haunted_item__Initialize__default_dispell_types;

    // tg: code/modules/antagonists/revenant/haunted_item.dm:70  var/static/list/default_dispell_traits (a proc-level static, rules 10.6)
    private static ref DmList Initialize__default_dispell_traits => ref TgState.Current.datum_component_haunted_item__Initialize__default_dispell_traits;

    // tg: code/modules/antagonists/revenant/haunted_item.dm:13
    [DmParams("haunt_color", "haunt_duration", "aggro_radius", "spawn_message", "despawn_message", "throw_force_bonus", "throw_force_max", "types_which_dispell_us", "traits_which_dispell_us")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object haunt_color = Dm.arg(__extra, 0);
        object haunt_duration = Dm.arg(__extra, 1);
        object aggro_radius = Dm.arg(__extra, 2);
        object spawn_message = Dm.arg(__extra, 3);
        object despawn_message = Dm.arg(__extra, 4);
        object throw_force_bonus = Dm.arg(__extra, 5) ?? 3;
        object throw_force_max = Dm.arg(__extra, 6) ?? 15;
        var types_which_dispell_us = Dm.arg(__extra, 7) as DmList;
        var traits_which_dispell_us = Dm.arg(__extra, 8) as DmList;

        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        var haunted_item = (obj_item)parent;
        if (haunted_item.ai_controller is datum_ai_controller_haunted) // already spooky
            return COMPONENT_INCOMPATIBLE;

        haunted_item.make_haunted(MAGIC_TRAIT, haunt_color);
        if (haunted_item.ai_controller == null) // failed to make spooky! don't go on
            return COMPONENT_INCOMPATIBLE;

        if (haunt_duration != null)
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_haunting)), Dm.num(haunt_duration), file: "code/modules/antagonists/revenant/haunted_item.dm", line: 47);

        if (spawn_message != null)
            haunted_item.visible_message(spawn_message);

        if (aggro_radius != null)
        {
            // We were given an aggro radius, we'll start attacking people nearby
            foreach (mob_living victim in Dm.each<mob_living>(view(aggro_radius, haunted_item)))
            {
                if (IS_UNCONSCIOUS_OR_CRIT(victim))
                    continue;
                if (Dm.truthy(Dm.bitand(victim.mob_biotypes, MOB_SPIRIT)))
                    continue;
                if (victim.invisibility >= INVISIBILITY_REVENANT)
                    continue;
                // This gives all mobs in view "5" haunt level
                // For reference picking one up gives "2"
                ((datum_ai_controller)haunted_item.ai_controller).add_blackboard_key_assoc(BB_TO_HAUNT_LIST, victim, 5);
            }
        }

        if (Dm.num(haunted_item.throwforce) < Dm.num(throw_force_max))
        {
            pre_haunt_throwforce = haunted_item.throwforce;
            haunted_item.throwforce = Math.Min(Dm.num(haunted_item.throwforce) + Dm.num(throw_force_bonus), Dm.num(throw_force_max));
        }

        Initialize__default_dispell_types ??= Dm.list(typeof(obj_item_nullrod), typeof(obj_item_book_bible));
        Initialize__default_dispell_traits ??= Dm.list(TRAIT_NULLROD_ITEM);
        this.types_which_dispell_us = types_which_dispell_us ?? Initialize__default_dispell_types;
        this.traits_which_dispell_us = traits_which_dispell_us ?? Initialize__default_dispell_traits;
        this.despawn_message = despawn_message;
        return null;
    }

    // tg: code/modules/antagonists/revenant/haunted_item.dm:75
    public override object Destroy(float? force = null)
    {
        var haunted_item = (obj_item)parent;
        // Handle these two specifically in Destroy() instead of clear_haunting(),
        // because we want to make sure they always get dealt with no matter how the component is removed
        if (pre_haunt_throwforce != null)
            haunted_item.throwforce = Dm.num(pre_haunt_throwforce);
        haunted_item.remove_haunted(MAGIC_TRAIT);
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/revenant/haunted_item.dm:84
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_hit_by_holy_tool));
    }

    // tg: code/modules/antagonists/revenant/haunted_item.dm:87
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ATTACKBY);
    }

    /// Removes the haunting, showing any despawn message we have and qdeling our component
    // tg: code/modules/antagonists/revenant/haunted_item.dm:91
    public virtual void clear_haunting()
    {
        var haunted_item = (obj_item)parent;

        if (despawn_message != null)
            haunted_item.visible_message(despawn_message);

        qdel(this);
    }

    /// Signal proc for [COMSIG_ATOM_ATTACKBY], when we get smacked by holy stuff we should stop being ghostly.
    // tg: code/modules/antagonists/revenant/haunted_item.dm:100
    [SignalHandler]
    public virtual object on_hit_by_holy_tool(obj_item source = null, obj_item attacking_item = null, mob_living attacker = null, object @params = null)
    {
        if (!Dm.truthy(is_type_in_list(attacking_item, types_which_dispell_us)))
        {
            bool has_trait = false;
            foreach (object dispell_trait in Dm.each(traits_which_dispell_us))
            {
                if (HAS_TRAIT(attacking_item, dispell_trait))
                {
                    has_trait = true;
                    break;
                }
            }

            if (!has_trait)
                return null;
        }

        attacker.visible_message(span_warning(I18n.T("[] dispells the ghostly energy from []!", attacker, source)), span_warning(I18n.T("You dispel the ghostly energy from []!", source)));
        clear_haunting();
        return COMPONENT_NO_AFTERATTACK;
    }
}

public static partial class GlobalProcs
{
    /**
     * Takes a given area and chance, applying the haunted_item component to objects in the area.
     *
     * Takes an epicenter, and within the range around it, runs a haunt_chance percent chance of
     * applying the haunted_item component to nearby objects.
     *
     * * epicenter - The center of the outburst area.
     * * range - The range of the outburst, centered around the epicenter.
     * * haunt_chance - The percent chance that an object caught in the epicenter will be haunted.
     * * duration - How long the haunting will remain for.
     */
    // tg: code/modules/antagonists/revenant/haunted_item.dm:129
    public static void haunt_outburst(object epicenter = null, object range = null, object haunt_chance = null, object duration = null)
    {
        duration ??= 1 * MINUTES;
        foreach (obj_item object_to_possess in Dm.each<obj_item>(Builtins.range(range, epicenter)))
        {
            if (!prob(Dm.num(haunt_chance)))
                continue;
            Waiting.NoSleep(object_to_possess.AddComponent(typeof(datum_component_haunted_item),
                Dm.named("haunt_color", "#52336e"),
                Dm.named("haunt_duration", duration),
                Dm.named("aggro_radius", range),
                Dm.named("spawn_message", span_revenwarning(I18n.T("[] slowly rises upward, hanging menacingly in the air...", object_to_possess))),
                Dm.named("despawn_message", span_revenwarning(I18n.T("[] settles to the floor, lifeless and unmoving.", object_to_possess)))));
        }
    }
}

public sealed partial class TgState
{
    internal DmList datum_component_haunted_item__Initialize__default_dispell_types;
    internal DmList datum_component_haunted_item__Initialize__default_dispell_traits;
}
