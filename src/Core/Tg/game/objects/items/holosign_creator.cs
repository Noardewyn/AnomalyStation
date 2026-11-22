// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/holosign_creator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file but the screentips' contexts of the holofan projector (register_context, M9; add_context is
// translated). The var block is generated. The cyborg's projector and its emagged overload since silicons SI2.

// tg: code/game/objects/items/holosign_creator.dm:1
public partial class obj_item_holosign_creator : obj_item
{
    // tg: code/game/objects/items/holosign_creator.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_openspace_item_click_handler)));
        RegisterSignal(this, COMSIG_OBJ_PAINTED, nameof(on_color_change));
        return dot;
    }

    // tg: code/game/objects/items/holosign_creator.dm:33
    public override async DmTask handle_openspace_click(turf target = null, mob user = null, DmList modifiers = null)
    {
        await interact_with_atom(target, user as mob_living, modifiers);
    }

    // tg: code/game/objects/items/holosign_creator.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(signs))
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("It is currently maintaining <b>[]/[]</b> projections.", signs.len, max_signs)));
        return dot;
    }

    // tg: code/game/objects/items/holosign_creator.dm:42
    public override object check_allowed_items(atom target = null, float? not_inside = null, float? target_self = null)
    {
        if (HAS_TRAIT(target, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return FALSE;
        return base.check_allowed_items(target, not_inside, target_self);
    }

    // tg: code/game/objects/items/holosign_creator.dm:47
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_allowed_items(interacting_with, not_inside: TRUE)))
            return NONE;

        turf target_turf = get_turf(interacting_with);
        obj_structure_holosign target_holosign = Dm.locate_type(holosign_type, target_turf) as obj_structure_holosign;

        if (Dm.truthy(target_holosign))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(await target_turf.is_blocked_turf(TRUE, ignore_atoms: projectable_through, type_list: TRUE))) //can't put holograms on a tile that has dense stuff
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(holocreator_busy))
        {
            balloon_alert(user, I18n.T("busy making a hologram!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.num(LAZYLEN(signs)) >= max_signs)
        {
            balloon_alert(user, I18n.T("max capacity!"));
            return ITEM_INTERACT_BLOCKING;
        }

        playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);

        if (Dm.truthy(creation_time))
        {
            holocreator_busy = TRUE;
            if (!Dm.truthy(await do_after(user, creation_time, target: interacting_with)))
            {
                holocreator_busy = FALSE;
                return ITEM_INTERACT_BLOCKING;
            }
            holocreator_busy = FALSE;
            if (Dm.num(LAZYLEN(signs)) >= max_signs)
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(await target_turf.is_blocked_turf(TRUE, ignore_atoms: projectable_through, type_list: TRUE))) //don't try to sneak dense stuff on our tile during the wait.
                return ITEM_INTERACT_BLOCKING;
        }

        target_holosign = await create_holosign(interacting_with, user) as obj_structure_holosign;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/holosign_creator.dm:80
    [DmParams("target_mob", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/game/objects/items/holosign_creator.dm:83
    public virtual async DmTask<object> create_holosign(atom target = null, mob user = null)
    {
        atom_movable new_holosign = Dm.@new(holosign_type, get_turf(target), this) as atom_movable;
        new_holosign.add_hiddenprint(user);
        if (Dm.truthy(color))
            new_holosign.color = color;
        return new_holosign;
    }

    // tg: code/game/objects/items/holosign_creator.dm:90
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYLEN(signs)))
        {
            foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs.Copy()))
                qdel(hologram);
            balloon_alert(user, I18n.T("holograms cleared"));
        }
        return null;
    }

    // tg: code/game/objects/items/holosign_creator.dm:97
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (Dm.truthy(LAZYLEN(signs)))
        {
            foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs.Copy()))
                qdel(hologram);
        }
        return dot;
    }

    // tg: code/game/objects/items/holosign_creator.dm:104
    [SignalHandler]
    public virtual void on_color_change(obj_item holosign_creator = null, mob user = null, obj_item_toy_crayon_spraycan spraycan = null, object is_dark_color = null)
    {
        if (!Dm.truthy(spraycan.actually_paints))
            return;

        if (Dm.truthy(LAZYLEN(signs)))
        {
            foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs.Copy()))
                hologram.color = color;
        }
    }
}

// tg: code/game/objects/items/holosign_creator.dm:112  /obj/item/holosign_creator/janibarrier, :122 security, :132 engineering (var-only)

// tg: code/game/objects/items/holosign_creator.dm:142
public partial class obj_item_holosign_creator_atmos : obj_item_holosign_creator
{
    // tg: code/game/objects/items/holosign_creator.dm:159
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held projector, M9
        Dm.Deferred("code/game/objects/items/holosign_creator.dm:161", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/game/objects/items/holosign_creator.dm:164
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (!Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_SUCCESS)))
            return dot;
        obj_machinery_door_firedoor firelock = Dm.locate<obj_machinery_door_firedoor>(get_turf(interacting_with));
        if (firelock != null)
            await firelock.open();
        return dot;
    }

    // tg: code/game/objects/items/holosign_creator.dm:171
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(LAZYLEN(signs)))
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("[] clearview", Dm.truthy(clearview) ? "Turn off" : "Temporarily activate");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/holosign_creator.dm:177
    public override async DmTask<object> create_holosign(atom target = null, mob user = null)
    {
        obj_structure_holosign_barrier_atmos new_holosign = Dm.@new(holosign_type, get_turf(target), this) as obj_structure_holosign_barrier_atmos;
        new_holosign.add_hiddenprint(user);
        if (Dm.truthy(color))
            new_holosign.color = color;
        if (Dm.truthy(clearview))
            new_holosign.clearview_transparency();
        return new_holosign;
    }

    // tg: code/game/objects/items/holosign_creator.dm:186
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(clearview))
        {
            reset_hologram_transparency();
            balloon_alert(user, I18n.T("turned off clearview"));
            return null;
        }
        if (Dm.truthy(LAZYLEN(signs)))
        {
            foreach (obj_structure_holosign_barrier_atmos hologram in Dm.each_cast<obj_structure_holosign_barrier_atmos>(signs.Copy()))
                hologram.clearview_transparency();
            clearview = TRUE;
            balloon_alert(user, I18n.T("turned on clearview"));
            clearview_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_hologram_transparency)), 40 * SECONDS, TIMER_STOPPABLE, file: "code/game/objects/items/holosign_creator.dm", line: 197);
        }
        return await base.attack_self_secondary(user, modifiers);
    }

    // tg: code/game/objects/items/holosign_creator.dm:199
    public virtual void reset_hologram_transparency()
    {
        if (Dm.truthy(LAZYLEN(signs)))
        {
            foreach (obj_structure_holosign_barrier_atmos hologram in Dm.each_cast<obj_structure_holosign_barrier_atmos>(signs.Copy()))
                hologram.reset_transparency();
            clearview = FALSE;
            deltimer(clearview_timer);
        }
    }
}

// tg: code/game/objects/items/holosign_creator.dm:209  /obj/item/holosign_creator/medical (var-only)

// tg: code/game/objects/items/holosign_creator.dm:219
public partial class obj_item_holosign_creator_cyborg : obj_item_holosign_creator
{
    // tg: code/game/objects/items/holosign_creator.dm:226
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (iscyborg(user))
        {
            mob_living_silicon_robot borg = (mob_living_silicon_robot)user;

            if (Dm.truthy(shock))
            {
                to_chat(user, span_notice(I18n.T("You clear all active holograms, and reset your projector to normal.")));
                holosign_type = typeof(obj_structure_holosign_barrier_cyborg);
                creation_time = 0.5f * SECONDS;
                foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs?.Copy()))
                    qdel(hologram);
                shock = FALSE;
                return null;
            }
            if (Dm.truthy(borg.emagged) && !Dm.truthy(shock))
            {
                to_chat(user, span_warning(I18n.T("You clear all active holograms, and overload your energy projector!")));
                holosign_type = typeof(obj_structure_holosign_barrier_cyborg_hacked);
                creation_time = 3 * SECONDS;
                foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs?.Copy()))
                    qdel(hologram);
                shock = TRUE;
                return null;
            }
        }
        foreach (obj_structure_holosign hologram in Dm.each_cast<obj_structure_holosign>(signs?.Copy()))
            qdel(hologram);
        balloon_alert(user, I18n.T("holograms cleared"));
        return null;
    }
}
