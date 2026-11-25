// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/lighting/light_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file, which the light replacer stores, recycles and fits (the tube and bulb items
// in boxes, in hands and on the floor). The var block is generated (D111). Left as deferred leaves (not-translated.md): the
// golem food component (golems, M10+). The fixture's side of putting a tube in by hand (light.dm:387-416) stays in light.cs.
// the light item
// can be tube or bulb subtypes
// will fit into empty /obj/machinery/light of the corresponding type

// tg: code/modules/power/lighting/light_items.dm:5
public partial class obj_item_light : obj_item
{
    // tg: code/modules/power/lighting/light_items.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(LIGHT_REAGENT_CAPACITY, Dm.bitor(Dm.bitor(Dm.bitor(INJECTABLE, DRAINABLE), SEALED_CONTAINER), TRANSPARENT));
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", force)));
        Waiting.NoSleep(update_icon_state());
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_golem_food), Dm.named("golem_food_key", typeof(obj_item_light)), Dm.named("extra_validation", Dm.@new<datum_callback>(this, nameof(is_intact)))));
        return dot;
    }

    // tg: code/modules/power/lighting/light_items.dm:25  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_light__Initialize__loc_connections;

    // tg: code/modules/power/lighting/light_items.dm:32
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_silicon), 5), Dm.kv(typeof(datum_reagent_nitrogen), 10));
    }

    // tg: code/modules/power/lighting/light_items.dm:35
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_lightreplacer)))
            return NONE;
        await tool.item_interaction(user, this);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Returns true if bulb is intact
    // tg: code/modules/power/lighting/light_items.dm:42
    public virtual object is_intact()
    {
        return Dm.eq(status, LIGHT_OK);
    }

    // tg: code/modules/power/lighting/light_items.dm:45
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.eq(status, LIGHT_BROKEN))
            user.visible_message(span_suicide(I18n.T("[] begins to stab []self with \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        else
        {
            user.visible_message(span_suicide(I18n.T("[] begins to eat \\the []! It looks like [] not very bright!", user, this, user.p_theyre())));
            await shatter();
        }
        return BRUTELOSS;
    }

    // tg: code/modules/power/lighting/light_items.dm:93
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (!Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra))) //not caught by a mob
            Waiting.NoSleep(shatter());
        return null;
    }

    // update the icon state and description of the light

    // tg: code/modules/power/lighting/light_items.dm:99
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        object sw__1 = status;
        if (Dm.eq(sw__1, LIGHT_OK))
            icon_state = (string)base_state;
        else if (Dm.eq(sw__1, LIGHT_BURNED))
            icon_state = Dm.text("[]-burned", base_state);
        else if (Dm.eq(sw__1, LIGHT_BROKEN))
            icon_state = Dm.text("[]-broken", base_state);
        return dot;
    }

    // tg: code/modules/power/lighting/light_items.dm:109
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        object sw__1 = status;
        if (Dm.eq(sw__1, LIGHT_OK))
            desc = Dm.text("A replacement [].", name);
        else if (Dm.eq(sw__1, LIGHT_BURNED))
            desc = Dm.text("A burnt-out [].", name);
        else if (Dm.eq(sw__1, LIGHT_BROKEN))
            desc = Dm.text("A broken [].", name);
        return dot;
    }

    // tg: code/modules/power/lighting/light_items.dm:119
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable moving_atom = null)
    {
        // SIGNAL_HANDLER
        if (!isliving(moving_atom))
            return;
        mob_living moving_mob = moving_atom as mob_living;
        if (!Dm.truthy(Dm.bitand(moving_mob.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || Dm.truthy(moving_mob.buckled))
        {
            playsound(this, Dm.resource("sound/effects/footstep/glass_step.ogg"), HAS_TRAIT(moving_mob, TRAIT_LIGHT_STEP) ? 30 : 50, TRUE);
            if (Dm.eq(status, LIGHT_BURNED) || Dm.eq(status, LIGHT_OK))
                Waiting.NoSleep(shatter(moving_mob));
        }
    }

    // tg: code/modules/power/lighting/light_items.dm:129  (/obj/item/light/attack(mob/living/M, mob/living/user, def_zone): the
    // arguments are the base attack's, positionally)
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);
        await shatter(M);
        return dot;
    }

    // tg: code/modules/power/lighting/light_items.dm:133
    public override async DmTask<object> attack_atom(atom attacked_obj = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attack_atom(attacked_obj, user, modifiers, attack_modifiers);
        await shatter(attacked_obj);
        return dot;
    }

    // tg: code/modules/power/lighting/light_items.dm:137
    public virtual async DmTask shatter(object target = null)
    {
        if (Dm.eq(status, LIGHT_OK) || Dm.eq(status, LIGHT_BURNED))
        {
            visible_message(span_danger(I18n.T("[] shatters.", this)), span_hear(I18n.T("You hear a small glass object shatter.")));
            status = LIGHT_BROKEN;
            force = 5;
            sharpness = SHARP_POINTY;
            playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 75, TRUE);
            datum_reagents holder = reagents as datum_reagents;
            if (Dm.truthy(Dm.length(holder.reagent_list)))
            {
                visible_message(span_danger(I18n.T("The contents of [] splash onto you as you step on it!", this)), span_hear(I18n.T("You feel the contents of [] splash onto you as you step on it!.", this)));
                await holder.expose(target as atom, TOUCH);
            }
            update_appearance(Dm.bitor(UPDATE_DESC, UPDATE_ICON));
        }
    }
}

// tg: code/modules/power/lighting/light_items.dm:53
public partial class obj_item_light_tube : obj_item_light
{
    // tg: code/modules/power/lighting/light_items.dm:64
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        object sw__1 = status;
        if (Dm.eq(sw__1, LIGHT_BURNED))
            inhand_icon_state = Dm.text("[]-burned", base_state);
        else if (Dm.eq(sw__1, LIGHT_BROKEN))
            inhand_icon_state = Dm.text("[]-broken", base_state);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/lighting/light_items.dm:25  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_item_light__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_light.on_entered)));
}
