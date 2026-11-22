// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/rust.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps RG3, D339): the rust element (:1-95; hydrogen peroxide rusting a wall or plating: the rust overlay, the examine line,
// burned off with a welder or scraped off, washed off by space cola, no floor tiles on it). The heretic's rust (:97-139, its
// corruption of whoever walks on it) waits for the heretic (D333; not-translated.md section 226). The var block is generated.

/**
 * Adding this element to an atom will have it automatically render an overlay.
 * The overlay can be specified in new as the first paramter; if not set it defaults to rust_overlay's rust_default
 */
// tg: code/datums/elements/rust.dm:5
public partial class datum_element_rust : datum_element
{
    // tg: code/datums/elements/rust.dm:11
    [DmParams("target", "rust_icon", "rust_icon_state")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object rust_icon = Dm.arg(__extra, 0) ?? Dm.resource("icons/effects/rust_overlay.dmi");
        object rust_icon_state = Dm.arg(__extra, 1) ?? "rust_default";
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        var target_atom = (atom)target;
        if (rust_overlay == null)
            rust_overlay = image(rust_icon, rust_icon_state);
        ADD_TRAIT(target_atom, TRAIT_RUSTY, ELEMENT_TRAIT(type));
        RegisterSignal(target_atom, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(apply_rust_overlay));
        RegisterSignal(target_atom, COMSIG_ATOM_EXAMINE, nameof(handle_examine));
        RegisterSignal(target_atom, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_interaction));
        RegisterSignals(target_atom, Dm.list(COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_WELDER), COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_RUSTSCRAPER)), nameof(secondary_tool_act));
        RegisterSignal(target_atom, COMSIG_ATOM_EXPOSE_REAGENT, nameof(on_reagent_expose));
        // Unfortunately registering with parent sometimes doesn't cause an overlay update
        target_atom.update_appearance();
        return dot;
    }

    // tg: code/datums/elements/rust.dm:26
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        var source_atom = (atom)source;
        UnregisterSignal(source_atom, COMSIG_ATOM_UPDATE_OVERLAYS);
        UnregisterSignal(source_atom, COMSIG_ATOM_EXAMINE);
        UnregisterSignal(source_atom, COMSIG_ATOM_ITEM_INTERACTION);
        UnregisterSignal(source_atom, Dm.list(COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_WELDER), COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_RUSTSCRAPER)));
        UnregisterSignal(source_atom, COMSIG_ATOM_EXPOSE_REAGENT);
        REMOVE_TRAIT(source_atom, TRAIT_RUSTY, ELEMENT_TRAIT(type));
        source_atom.update_appearance();
        return dot;
    }

    // tg: code/datums/elements/rust.dm:36
    [SignalHandler]
    public virtual void handle_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        Dm.append(ref examine_text, span_notice(I18n.T("[] is very rusty, you could probably <i>burn</i> or <i>scrape</i> it off, hell maybe even pour some <i>space cola</i> on it to remove the rust.", source)));
    }

    // tg: code/datums/elements/rust.dm:41
    [SignalHandler]
    public virtual void apply_rust_overlay(atom parent_atom = null, DmList overlays = null)
    {
        if (rust_overlay != null)
            Dm.append(ref overlays, rust_overlay);
    }

    /// Because do_after sleeps we register the signal here and defer via an async call
    // tg: code/datums/elements/rust.dm:48
    [SignalHandler]
    public virtual object secondary_tool_act(atom source = null, mob user = null, obj_item item = null)
    {
        Waiting.Detach(handle_tool_use(source, user, item)); // INVOKE_ASYNC(src, PROC_REF(handle_tool_use), source, user, item)
        return ITEM_INTERACT_BLOCKING;
    }

    /// We call this from secondary_tool_act because we sleep with do_after
    // tg: code/datums/elements/rust.dm:55
    public virtual async DmTask handle_tool_use(atom source = null, mob user = null, obj_item item = null)
    {
        object sw__1 = item.tool_behaviour;
        if (Dm.eq(sw__1, TOOL_WELDER))
        {
            if (!Dm.truthy(item.tool_start_check(user as mob_living, amount: 1)))
                return;
            user.balloon_alert(user, I18n.T("burning off rust..."));

            if (!Dm.truthy(await item.use_tool(source, user as mob_living, 5 * SECONDS)))
                return;
            user.balloon_alert(user, I18n.T("burned off rust"));
            Detach(source);
            return;
        }
        else if (Dm.eq(sw__1, TOOL_RUSTSCRAPER))
        {
            if (!Dm.truthy(item.tool_start_check(user as mob_living)))
                return;
            user.balloon_alert(user, I18n.T("scraping off rust..."));
            if (!Dm.truthy(await item.use_tool(source, user as mob_living, 2 * SECONDS)))
                return;
            user.balloon_alert(user, I18n.T("scraped off rust"));
            Detach(source);
            return;
        }
    }

    ///Immediately removes rust if exposed to space cola.
    // tg: code/datums/elements/rust.dm:81
    [SignalHandler]
    public virtual void on_reagent_expose(atom source = null, datum_reagent reagent_splashed = null, object reac_volume = null, object methods = null)
    {
        if (!istype(reagent_splashed, typeof(datum_reagent_consumable_space_cola)))
            return;
        if (Dm.truthy(Dm.bitand(methods, INHALE)))
            return;
        Detach(source);
    }

    /// Prevents placing floor tiles on rusted turf
    // tg: code/datums/elements/rust.dm:90
    [SignalHandler]
    public virtual object on_interaction(datum source = null, mob user = null, obj_item tool = null, object modifiers = null)
    {
        if (istype(tool, typeof(obj_item_stack_tile)) || istype(tool, typeof(obj_item_stack_rods)))
        {
            user.balloon_alert(user, I18n.T("floor too rusted!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return null;
    }
}
