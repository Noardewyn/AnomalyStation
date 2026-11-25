// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the plasmaman's envirosuit with its automatic extinguisher (five charges, refilled by the cartridge), and the clown's
// that sprays lube instead. The job and department envirosuits are var-only, generated (D111).

//Basically the assistant suit
// tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:2
public partial class obj_item_clothing_under_plasmaman : obj_item_clothing_under
{
    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:24
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There [] [] extinguisher charges left in this suit.", extinguishes_left == 1 ? "is" : "are", extinguishes_left)));
        return dot;
    }

    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:28
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ICLOTHING)))
        {
            RegisterSignals(user, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_LIVING_IGNITED, SIGNAL_ADDTRAIT(TRAIT_HEAD_ATMOS_SEALED)), nameof(check_fire_state));
            check_fire_state();
        }
    }

    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:34
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_LIVING_IGNITED, SIGNAL_ADDTRAIT(TRAIT_HEAD_ATMOS_SEALED)));
        return dot;
    }

    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:38
    [SignalHandler]
    public virtual void check_fire_state(datum source = null)
    {
        // SIGNAL_HANDLER

        if (!ishuman(loc))
            return;

        // This is weird but basically we're calling this proc once the cooldown ends in case our wearer gets set on fire again during said cooldown
        // This is why we're ignoring source and instead checking by loc
        mob_living_carbon_human owner = loc as mob_living_carbon_human;
        if (!Dm.truthy(owner.on_fire) || !Dm.truthy(owner.is_atmos_sealed(additional_flags: PLASMAMAN_PREVENT_IGNITION, check_hands: TRUE)))
            return;

        if (!Dm.truthy(extinguishes_left) || !(extinguish_timer <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;

        extinguishes_left -= 1;
        extinguish_timer = world.time + extinguish_cooldown; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        // Check if our (possibly other) wearer is on fire once the cooldown ends
        _addtimer(Dm.@new<datum_callback>(this, nameof(check_fire_state)), extinguish_cooldown, file: "code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm", line: 55);
        Waiting.NoSleep(activate_fire_safety(owner));
    }

    /// Extinguish our suit
    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:60
    public virtual async DmTask activate_fire_safety(mob_living owner = null)
    {
        owner.visible_message(span_warning(I18n.T("[]'s suit automatically extinguishes []!", owner, owner.p_them())), span_warning(I18n.T("Your suit automatically extinguishes you.")));
        owner.extinguish_mob();
        Dm.@new<obj_effect_particle_effect_water>(get_turf(owner));
    }

    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:65
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_extinguisher_refill))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (extinguishes_left == 5)
        {
            to_chat(user, span_notice(I18n.T("The inbuilt extinguisher is full.")));
            return ITEM_INTERACT_BLOCKING;
        }

        extinguishes_left = 5;
        to_chat(user, span_notice(I18n.T("You refill the suit's built-in extinguisher, using up the cartridge.")));
        check_fire_state();
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:141
public partial class obj_item_clothing_under_plasmaman_clown : obj_item_clothing_under_plasmaman
{
    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:152
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:154", "Initialize: AddElement(/datum/element/swabable, CELL_LINE_TABLE_CLOWN, ...): the cell lines (the sample swab, not ported)");
        return dot;
    }

    /// Sprays lube instead of water
    // tg: code/modules/clothing/under/jobs/Plasmaman/civilian_service.dm:166
    public override async DmTask activate_fire_safety(mob_living owner = null)
    {
        owner.visible_message(span_warning(I18n.T("[]'s suit spews space lube everywhere!", owner)), span_warning(I18n.T("Your suit spews space lube everywhere!")));
        owner.extinguish_mob();
        await do_foam(4, this, get_turf(owner), typeof(datum_reagent_lube), 15);
    }
}
