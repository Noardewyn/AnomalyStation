// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/fermenting_barrel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (fermenting fruit into wine in the wooden barrel). add_context is a live-partial row (D264).

// tg: code/modules/hydroponics/fermenting_barrel.dm:1
public partial class obj_structure_fermenting_barrel : obj_structure
{
    // tg: code/modules/hydroponics/fermenting_barrel.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(600, DRAINABLE);
        soundloop = Dm.@new<datum_looping_sound_boiling>(this, fermenting);
        soundloop.volume = sound_volume;
        register_context();

        RegisterSignals(this, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_WAS_RENAMED),
            SIGNAL_ADDTRAIT(TRAIT_HAS_LABEL),
            SIGNAL_REMOVETRAIT(TRAIT_WAS_RENAMED),
            SIGNAL_REMOVETRAIT(TRAIT_HAS_LABEL)), nameof(update_overlay_on_sig));
        return dot;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:45
    public override object Destroy(float? force = null)
    {
        qdel(soundloop);
        soundloop = null;
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(open))
        {
            float fruit_count = contents.len;
            if (Dm.truthy(fruit_count))
            {
                Dm.append(ref dot, span_notice(I18n.T("It contains [] fruit[] ready to be fermented.", fruit_count, fruit_count == 1 ? "" : "s")));
                Dm.append(ref dot, span_notice(I18n.T("[] to take them out of [].", EXAMINE_HINT("Right-click"), this)));
            }
            Dm.append(ref dot, span_notice(I18n.T("It is currently open, letting you fill it with fruits or reagents.")));
        }
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("It is currently closed, letting it ferment fruits or draw reagents from its tap.")));
        }
        return dot;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:60
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(open))
        {
            if (tool is obj_item_food_grown grown_tool && Dm.truthy(await insert_fruit(user, grown_tool)))
            {
                balloon_alert(user, I18n.T("added fruit"));
                return ITEM_INTERACT_SUCCESS;
            }

            if (tool is obj_item_storage_bag_plants bag)
            {
                float inserted_fruits = 0;
                foreach (obj_item_food_grown fruit in Dm.each<obj_item_food_grown>(bag.contents))
                {
                    if (!Dm.truthy(await insert_fruit(user, fruit, bag)))
                        break;
                    inserted_fruits++;
                }
                if (!Dm.truthy(inserted_fruits))
                    return ITEM_INTERACT_BLOCKING;

                balloon_alert(user, I18n.T("added [] fruit[]", inserted_fruits, inserted_fruits == 1 ? "" : "s"));
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:81
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(can_open))
            return null;
        datum_reagents holder = reagents as datum_reagents;
        if (Dm.truthy(open))
        {
            open = FALSE;
            holder.flags = Dm.bitor(Dm.num(holder.flags), DRAINABLE);
            holder.flags = Dm.bitand(Dm.num(holder.flags), Dm.bitnot(Dm.bitor(REFILLABLE, TRANSPARENT)));
            playsound(this, lid_close_sound, sound_volume);
            start_fermentation();
        }
        else
        {
            open = TRUE;
            holder.flags = Dm.bitand(Dm.num(holder.flags), ~DRAINABLE);
            holder.flags = Dm.bitor(Dm.num(holder.flags), Dm.bitor(REFILLABLE, TRANSPARENT));
            playsound(this, lid_open_sound, sound_volume);
            stop_fermentation();
        }
        update_appearance(UPDATE_ICON);
        return null;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:98
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (!Dm.truthy(open))
            return dot;

        if (!Dm.truthy(length(contents)))
        {
            balloon_alert(user, I18n.T("empty!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        await dump_contents();
        balloon_alert(user, I18n.T("emptied []", this));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:114
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return null;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:118
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(open) ? "barrel_open" : "barrel";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:122
    [SignalHandler]
    public virtual void update_overlay_on_sig()
    {
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:126
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (HAS_TRAIT(this, TRAIT_WAS_RENAMED) || HAS_TRAIT(this, TRAIT_HAS_LABEL))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_overlay_label", base_icon_state)));
        return dot;
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:148
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom drop_point = drop_location() as atom;
        foreach (obj_item_food_grown fruit in Dm.each_cast<obj_item_food_grown>(contents))
            await fruit.forceMove(drop_point);
    }

    /// Adds the fruit to the barrel to queue the fermentation
    // tg: code/modules/hydroponics/fermenting_barrel.dm:154
    public virtual async DmTask<object> insert_fruit(mob user = null, obj_item_food_grown fruit = null, obj_item_storage_bag_plants bag = null)
    {
        datum_reagents holder = reagents as datum_reagents;
        if (Dm.num(holder.total_volume) + potential_volume > Dm.num(holder.maximum_volume))
        {
            balloon_alert(user, I18n.T("it's full!"));
            return FALSE;
        }
        if (!Dm.truthy(fruit.can_distill))
        {
            balloon_alert(user, I18n.T("can't ferment this!"));
            return FALSE;
        }
        if (bag != null && !Dm.truthy(bag.atom_storage.attempt_remove(fruit, this)))
        {
            balloon_alert(user, I18n.T("can't take from bag!"));
            return FALSE;
        }
        else if (!Dm.truthy(await user.transferItemToLoc(fruit, this)))
        {
            balloon_alert(user, I18n.T("can't take fruit!"));
            return FALSE;
        }
        potential_volume += Dm.num((fruit.reagents as datum_reagents)?.total_volume);
        return TRUE;
    }

    /// Starts the fermentation process
    // tg: code/modules/hydroponics/fermenting_barrel.dm:171
    public virtual void start_fermentation()
    {
        if (Dm.truthy(fermenting))
            return;
        if (Dm.truthy(open))
            return;
        datum_reagents holder = reagents as datum_reagents;
        if (Dm.num(holder.total_volume) >= Dm.num(holder.maximum_volume))
            return;
        if (Dm.locate<obj_item_food>(contents) == null)
            return;
        fermenting = TRUE;
        soundloop.start();
        START_PROCESSING(SSobj, this);
    }

    /// Ferments the next found fruit into wine
    // tg: code/modules/hydroponics/fermenting_barrel.dm:185
    public virtual async DmTask<object> process_fermentation()
    {
        if (!Dm.truthy(fermenting))
            return null;
        if (Dm.truthy(open))
        {
            stop_fermentation();
            return null;
        }
        datum_reagents holder = reagents as datum_reagents;
        if (Dm.num(holder.total_volume) >= Dm.num(holder.maximum_volume))
        {
            stop_fermentation();
            return null;
        }
        obj_item_food_grown fruit = Dm.locate<obj_item_food_grown>(contents);
        if (fruit == null)
        {
            stop_fermentation();
            return null;
        }
        await fruit.ferment();
        datum_reagents fruit_reagents = fruit.reagents as datum_reagents;
        potential_volume -= Dm.num(fruit_reagents.total_volume);
        await fruit_reagents.trans_to(holder, Dm.num(fruit_reagents.total_volume));
        qdel(fruit);
        return null;
    }

    /// Stops the fermentation process
    // tg: code/modules/hydroponics/fermenting_barrel.dm:201
    public virtual void stop_fermentation()
    {
        fermenting = FALSE;
        soundloop.stop();
        STOP_PROCESSING(SSobj, this);
    }

    // tg: code/modules/hydroponics/fermenting_barrel.dm:206
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process_fermentation(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }
}

/// Lil gunpowder barrel fer pirates since it's a nice reagent holder
// tg: code/modules/hydroponics/fermenting_barrel.dm:210
public partial class obj_structure_fermenting_barrel_gunpowder : obj_structure_fermenting_barrel
{
    // tg: code/modules/hydroponics/fermenting_barrel.dm:215
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_gunpowder), 500));
        return dot;
    }
}

/// Medieval pirates can have a barrel as a treat
// tg: code/modules/hydroponics/fermenting_barrel.dm:220
public partial class obj_structure_fermenting_barrel_thermite : obj_structure_fermenting_barrel
{
    // tg: code/modules/hydroponics/fermenting_barrel.dm:225
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_thermite), 500));
        return dot;
    }
}
