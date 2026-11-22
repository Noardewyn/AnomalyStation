// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/can_barricade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M5 leftovers (2026-10-16): the whole file. Doors (door.dm:142) and full-tile windows (window.dm:62) take it: two wood
// planks on one build a crude plank barricade on its turf (deployable.dm).

// tg: code/datums/elements/can_barricade.dm:3
public partial class datum_element_can_barricade : datum_element
{
    // tg: code/datums/elements/can_barricade.dm:1  #define PLANK_BARRICADE_AMOUNT 2 (the file's own define)
    private const int PLANK_BARRICADE_AMOUNT = 2;

    // tg: code/datums/elements/can_barricade.dm:7
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_ATTACKBY, nameof(on_start_barricade));

        Dm.combine(ref ((atom)target).flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        return dot;
    }

    // tg: code/datums/elements/can_barricade.dm:18
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        // We don't remove HAS_CONTEXTUAL_SCREENTIPS_1, since there could be other stuff still hooked to it,
        // and being set without signals is not dangerous, just less performant.
        // A lot of things don't do this, perhaps make a proc that checks if any signals are still set, and if not,
        // remove the flag.
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/can_barricade.dm:26
    public virtual object on_start_barricade(atom source = null, obj_item plank = null, mob user = null, object @params = null)
    {
        // SIGNAL_HANDLER
        if (user is not mob_living living || Dm.truthy(living.combat_mode) || plank is not obj_item_stack_sheet_mineral_wood wood)
            return null;

        if (Dm.num(wood.get_amount()) < PLANK_BARRICADE_AMOUNT)
        {
            source.balloon_alert(user, I18n.T("need [] [] sheets!", PLANK_BARRICADE_AMOUNT, wood));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        source.balloon_alert(user, I18n.T("constructing barricade..."));
        playsound(source, Dm.resource("sound/items/hammering_wood.ogg"), 50, vary: TRUE);
        Waiting.Detach(barricade(source, wood, living, @params)); //signal handlers can't have do_afters inside of them
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// when our element gets attacked by wooden planks it creates a barricade
    // tg: code/datums/elements/can_barricade.dm:42
    public virtual async DmTask barricade(atom source = null, obj_item_stack_sheet_mineral_wood plank = null, mob_living user = null, object @params = null)
    {
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: source)) || !Dm.truthy(await plank.use(2)) || Dm.truthy(Dm.locate<obj_structure_barricade_wooden_crude>(source.loc)))
            return;

        source.balloon_alert(user, I18n.T("barricade constructed"));
        obj_structure_barricade_wooden_crude barricade = Dm.@new<obj_structure_barricade_wooden_crude>(source.loc);
        // tg: code/datums/elements/can_barricade.dm:48
        barricade.add_fingerprint(user);
    }

    // tg: code/datums/elements/can_barricade.dm:50
    public virtual object on_requesting_context_from_item(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        // SIGNAL_HANDLER
        if (held_item is obj_item_stack_sheet_mineral_wood && Dm.truthy(Waiting.NoSleep(source.Adjacent(user))))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Construct barricade";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }
}
