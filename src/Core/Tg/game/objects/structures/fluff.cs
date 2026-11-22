// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/fluff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (the M5 leftovers, 2026-10-16): the fluff base and its wrench, which a multi-tile airlock's filler inherits
// (airlock.dm:2595). Gaps ST5 (D333): the bus driver's seat honks. The other fluff subtypes with procs of their own stay inert with
// their modules. The var blocks are generated.
// Gaps AS1 (D333): the electric tram rail's electrified_buckle component (a mob buckled to the powered rail is shocked).

/**
 * # Fluff structure
 *
 * Fluff structures serve no purpose and exist only for enriching the environment. By default, they can be deconstructed with a wrench.
 */
// tg: code/game/objects/structures/fluff.dm:6
public partial class obj_structure_fluff : obj_structure
{
    // tg: code/game/objects/structures/fluff.dm:17
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(deconstructible))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        user.visible_message(span_notice(I18n.T("[] starts disassembling []...", user, this)),
            span_notice(I18n.T("You start disassembling []...", this)));
        tool.play_tool_sound(this);
        if (!Dm.truthy(await tool.use_tool(this, user, 50)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] disassembles []!", user, this)), span_notice(I18n.T("You break down [] into scrap metal.", this)));
        playsound(user, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        Dm.@new<obj_item_stack_sheet_iron>(drop_location());
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// The tram port (2026-11-12): the tram rails (fluff.dm:286-346).
// tg: code/game/objects/structures/fluff.dm:286
public partial class obj_structure_fluff_tram_rail : obj_structure_fluff
{
    // tg: code/game/objects/structures/fluff.dm:298
    public override async DmTask post_buckle_mob(mob_living L = null)
    {
        await base.post_buckle_mob(L);
        L.pixel_y += Dm.eq(dir, SOUTH) ? -3 : 14;
        RegisterSignal(L, COMSIG_LIVING_HIT_BY_TRAM, nameof(on_buckled_tram_smashed));
    }

    // tg: code/game/objects/structures/fluff.dm:303
    public override async DmTask post_unbuckle_mob(mob_living L = null)
    {
        await base.post_unbuckle_mob(L);
        L.pixel_y -= Dm.eq(dir, SOUTH) ? -3 : 14;
        UnregisterSignal(L, COMSIG_LIVING_HIT_BY_TRAM);
    }

    /// If someone gets hit by the tram while buckled to us (mission accomplished) unbuckle them so that they can fly away
    /// Also we rip one of their arms off to "uncuff" them
    // tg: code/game/objects/structures/fluff.dm:310
    public virtual void on_buckled_tram_smashed(mob_living smashed = null)
    {
        Waiting.NoSleep(unbuckle_mob(smashed, force: TRUE, can_fall: FALSE)); // Make sure they don't fall down a z-level until they've been thrown
    }
}

// tg: code/game/objects/structures/fluff.dm:326
public partial class obj_structure_fluff_tram_rail_electric : obj_structure_fluff_tram_rail
{
    // tg: code/game/objects/structures/fluff.dm:335
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_electrified_buckle),
            Dm.named("input_requirements", SHOCK_REQUIREMENT_AREA_POWER),
            Dm.named("shock_immediately", TRUE),
            Dm.named("print_message", FALSE),
            Dm.named("damage_on_shock", 5),
            Dm.named("loop_length", 8 * SECONDS),
            Dm.named("area_power_channel", power_channel),
            Dm.named("shock_flags", SHOCK_NOSTUN)));
        return dot;
    }

    // tg: code/game/objects/structures/fluff.dm:347
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        area our_area = get_area(this) as area;
        if (our_area != null && Dm.truthy(our_area.powered(power_channel)) && Dm.truthy(await (user as mob_living).electrocute_act(75, this)))
            do_sparks(5, TRUE, this);
        return dot;
    }
}

// tg: code/game/objects/structures/fluff.dm:132  (gaps ST5)
public partial class obj_structure_fluff_bus_passable_seat_driver : obj_structure_fluff_bus_passable_seat
{
    // tg: code/game/objects/structures/fluff.dm:137
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/items/carhorn.ogg"), 50, TRUE);
        return await base.attack_hand(user, modifiers);
    }
}
