// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/food_cart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the food cart, which unpacks into a griddle stand, a food smart fridge, a reinforced table
// and its tent (the stand has no procs). The var block is generated (D111).

// tg: code/modules/food_and_drinks/machinery/food_cart.dm:2
public partial class obj_machinery_food_cart : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cart_griddle = Dm.@new<obj_machinery_griddle_stand>(this);
        cart_smartfridge = Dm.@new<obj_machinery_smartfridge_food>(this);
        cart_table = Dm.@new<obj_structure_table_reinforced>(this);
        cart_tent = Dm.@new<obj_effect_food_cart_stand>(this);
        packed_things = Dm.list(cart_table, cart_smartfridge, cart_tent, cart_griddle); //middle, left, left, right
        RegisterSignal(cart_griddle, COMSIG_QDELETING, nameof(lost_part));
        RegisterSignal(cart_smartfridge, COMSIG_QDELETING, nameof(lost_part));
        RegisterSignal(cart_table, COMSIG_QDELETING, nameof(lost_part));
        RegisterSignal(cart_tent, COMSIG_QDELETING, nameof(lost_part));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:31
    public override object Destroy(float? force = null)
    {
        if (cart_griddle != null)
            QDEL_NULL(ref cart_griddle);
        if (cart_smartfridge != null)
            QDEL_NULL(ref cart_smartfridge);
        if (cart_table != null)
            QDEL_NULL(ref cart_table);
        if (cart_tent != null)
            QDEL_NULL(ref cart_tent);
        packed_things.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            if (Dm.truthy(Dm.bitand(cart_griddle.machine_stat, BROKEN)))
                Dm.append(ref dot, span_warning(I18n.T("The stand's <b>griddle</b> is completely broken!")));
            else
                Dm.append(ref dot, span_notice(I18n.T("The stand's <b>griddle</b> is intact.")));
            Dm.append(ref dot, span_notice(I18n.T("The stand's <b>fridge</b> seems fine."))); //weirdly enough, these fridges don't break
            Dm.append(ref dot, span_notice(I18n.T("The stand's <b>table</b> seems fine.")));
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:53
    public virtual async DmTask pack_up()
    {
        if (!Dm.truthy(unpacked))
            return;
        visible_message(span_notice(I18n.T("[] retracts all of it's unpacked components.", this)));
        foreach (object o in Dm.each(packed_things))
        {
            obj @object = (obj)o;
            UnregisterSignal(@object, COMSIG_MOVABLE_MOVED);
            await @object.forceMove(this);
        }
        await set_anchored(FALSE);
        unpacked = FALSE;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:64
    public virtual async DmTask unpack(mob user = null)
    {
        if (Dm.truthy(unpacked))
            return;
        if (!Dm.truthy(check_setup_place()))
        {
            to_chat(user, span_warning(I18n.T("There isn't enough room to unpack here! Bad spaces were marked in red.")));
            return;
        }
        visible_message(span_notice(I18n.T("[] expands into a full stand.", this)));
        await set_anchored(TRUE);
        float iteration = 1;
        turf grabbed_turf = get_step(get_turf(this), EAST);
        foreach (object angle in Dm.each(Dm.list(0, -45, -45, 45)))
        {
            turf T = get_step(grabbed_turf, turn(SOUTH, Dm.num(angle)));
            obj thing = packed_things[iteration] as obj;
            await thing.forceMove(T);
            RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(lost_part));
            iteration++;
        }
        unpacked = TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:82
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            to_chat(user, span_warning(I18n.T("[] is completely busted.", this)));
            return dot;
        }
        obj_item_card_id id_card = ((mob_living)user).get_idcard(hand_first: TRUE) as obj_item_card_id;
        if (!Dm.truthy(check_access(id_card)))
        {
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, TRUE);
            return dot;
        }
        to_chat(user, span_notice(I18n.T("You attempt to [] []...", Dm.truthy(unpacked) ? I18n.T("pack up") : I18n.T("unpack"), this)));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
        {
            to_chat(user, span_warning(I18n.T("Your []packing of [] was interrupted!", Dm.truthy(unpacked) ? "" : "un", this)));
            return dot;
        }
        if (Dm.truthy(unpacked))
            await pack_up();
        else
            await unpack(user);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:100
    public virtual object check_setup_place()
    {
        float has_space = TRUE;
        turf grabbed_turf = get_step(get_turf(this), EAST);
        foreach (object angle in Dm.each(Dm.list(0, -45, 45)))
        {
            turf T = get_step(grabbed_turf, turn(SOUTH, Dm.num(angle)));
            if (T != null && !Dm.truthy(T.density))
                Dm.@new<obj_effect_temp_visual_cart_space>(T);
            else
            {
                has_space = FALSE;
                Dm.@new<obj_effect_temp_visual_cart_space_bad>(T);
            }
        }
        return has_space;
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:112
    [SignalHandler]
    public virtual void lost_part(atom_movable source = null, object force = null)
    {
        //okay, so it's deleting the fridge or griddle which are more important. We're gonna break the machine then
        UnregisterSignal(cart_griddle, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        UnregisterSignal(cart_smartfridge, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        UnregisterSignal(cart_table, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        UnregisterSignal(cart_tent, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        Waiting.NoSleep(atom_break());
    }

    // tg: code/modules/food_and_drinks/machinery/food_cart.dm:122
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        await pack_up();
        if (!QDELETED(cart_griddle))
            QDEL_NULL(ref cart_griddle);
        if (!QDELETED(cart_smartfridge))
            QDEL_NULL(ref cart_smartfridge);
        if (!QDELETED(cart_table))
            QDEL_NULL(ref cart_table);
        if (!QDELETED(cart_tent))
            QDEL_NULL(ref cart_tent);
        return dot;
    }
}
