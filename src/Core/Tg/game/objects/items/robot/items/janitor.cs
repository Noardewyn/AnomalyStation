// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/janitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the janitor cyborg's vacuum suite, whose wand a crew member takes and works on a hose. The
// var blocks are generated (D111).

// tg: code/game/objects/items/robot/items/janitor.dm:1
public partial class obj_item_borg_cleaner_box : obj_item_borg
{
    // tg: code/game/objects/items/robot/items/janitor.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (loc is not mob_living_silicon robot)
            return INITIALIZE_HINT_QDEL;
        obj_item_robot_model_janitor model = Dm.locate<obj_item_robot_model_janitor>(robot.get_contents());
        module_list = WEAKREF(model) as datum_weakref;
        Waiting.NoSleep(AddComponent(typeof(datum_component_borg_item_offered_when_pulled), robot));
        ADD_TRAIT(this, TRAIT_BORG_GIVE, TRAIT_BORG_GIVE);
        hose = Dm.@new<obj_item_vacuum_item>(this);
        hose.cleaner_box = WEAKREF(this) as datum_weakref;
        Waiting.NoSleep(hose.AddComponent(
            typeof(datum_component_transforming),
            Dm.named("force_on", hose.force),
            Dm.named("hitsound_on", hose.hitsound),
            Dm.named("w_class_on", hose.w_class),
            Dm.named("clumsy_check", FALSE),
            Dm.named("attack_verb_continuous_on", Dm.list("washed", "mopped", "scrubbed", "whacked", "bapped", "decontaminated")),
            Dm.named("attack_verb_simple_on", Dm.list("wash", "mop", "scrub", "whack", "bap", "decontaminate"))
            ));
        hose.RegisterSignal(hose, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(obj_item_vacuum_item.on_transform));
        Waiting.NoSleep(update_icon(UPDATE_OVERLAYS));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:34
    public override object Destroy(float? force = null)
    {
        if (hose?.borg_hose != null)
            QDEL_NULL(ref hose.borg_hose);
        if (Dm.truthy(deployed))
            Waiting.NoSleep(hose.retract_hose());
        QDEL_NULL(ref hose);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:42
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(deployed))
            await hose.retract_hose();
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:48
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(deployed))
            await hose.retract_hose();
        locked = Dm.not(locked);
        await update_icon(UPDATE_OVERLAYS);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:55
    public override async DmTask<object> on_offered(mob_living offerer = null, mob_living offered = null)
    {
        object dot = TRUE;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_ITEM_OFFERING]) ? NONE : _SendSignal(COMSIG_ITEM_OFFERING, Dm.list(this, offerer)), COMPONENT_OFFER_INTERRUPT)))
            return dot;
        if (hose.loc != this && hose.loc is not mob_living) // Error handling
        {
            stack_trace(Dm.text("[] has been offered with [] not present inside its contents or inside a mob's loc variable. Location: []", this, hose, loc == null ? "NULLSPACE" : Dm.text("[], X: [], Y: [], Z:[]", hose.loc, hose.x, hose.y, hose.z)), "code/game/objects/items/robot/items/janitor.dm", 59);
            deployed = FALSE;
            await hose.forceMove(this);
        }
        if (Dm.truthy(locked) || Dm.truthy(deployed))
            return dot;
        if (offered == null)
            offered = Dm.locate<mob_living_carbon>(orange(1, offerer));
        if (offered != null && offered is mob_living_carbon)
        {
            offerer.visible_message(
                span_notice(I18n.T("[] extends the handle towards [] for their cleaning suite.", offerer, offered)),
                span_notice(I18n.T("The handle to your [] extends towards []'s hand.", this, offered)), null, 2);
        }

        await offerer.apply_status_effect(typeof(datum_status_effect_offering), this, typeof(atom_movable_screen_alert_give_borg), offered);
        return dot;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:75
    public override async DmTask<object> on_offer_taken(mob_living offerer = null, mob_living taker = null)
    {
        object dot = await base.on_offer_taken(offerer, taker);
        if (Dm.truthy(dot))
            return TRUE;
        hose.bag = (loc is mob_living_silicon ? pick(loc.get_all_contents_type(typeof(obj_item_storage_bag_trash))) : Dm.locate<obj_item_storage_bag_trash>(module_list.resolve())) as obj_item_storage_bag_trash;
        if (hose.bag == null)
        {
            stack_trace(Dm.text("[] failed to connect to a trash bag on [].", this, module_list.resolve()), "code/game/objects/items/robot/items/janitor.dm", 81);
            return TRUE;
        }
        taker.visible_message(
            span_notice(I18n.T("[] takes the [] from [].", taker, hose, offerer)),
            span_notice(I18n.T("You take the [] from []", hose, offerer)));
        hose.do_pickup_animation(taker, offerer);
        await taker.put_in_hands(hose);
        hose.borg_hose = hose.generate_hose(offerer, taker) as datum_beam_held_vacuum;
        hose.RegisterSignal(hose, COMSIG_ITEM_DROPPED, nameof(obj_item_vacuum_item.on_drop));
        playsound(hose, Dm.resource("sound/items/vacuum/vacuum_hose.ogg"), 100, TRUE);
        deployed = TRUE;
        await update_icon(UPDATE_OVERLAYS);
        offerer.remove_status_effect(typeof(datum_status_effect_offering));
        return TRUE;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:96
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(deployed))
            Dm.append(ref dot, "cleanerbox_on");
        else
            Dm.append(ref dot, "cleanerbox_wand");
        if (Dm.truthy(locked))
            Dm.append(ref dot, "cleanerbox_locked");
        return dot;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:105
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(Dm.truthy(locked) ? I18n.T("<b>Alt-Click</b> to <b>unlock</b> the [].", this) : I18n.T("<b>Alt-Click</b> to <b>lock</b> the [].", this)));
        return dot;
    }
}

// tg: code/game/objects/items/robot/items/janitor.dm:109
public partial class obj_item_vacuum_item : obj_item
{
    // tg: code/game/objects/items/robot/items/janitor.dm:128
    public override object Destroy(float? force = null)
    {
        bag = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:132
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (interacting_with is not obj_item thing)
            return NONE;
        if (bag == null && Dm.truthy(cleaning))
            return NONE;
        if (Dm.truthy(thing.anchored) || thing.w_class >= WEIGHT_CLASS_BULKY)
            return NONE;
        playsound(bag, Dm.resource("sound/items/vacuum/vacuum_use.ogg"), 20, TRUE);
        foreach (obj_item I in Dm.each<obj_item>(get_turf(thing)))
        {
            if (!istype(I, thing.GetType()))
                continue;
            if (!Dm.truthy(await do_after(user, 0.1f * SECONDS, user, show_progress: FALSE)))
                break;
            if (Dm.truthy(bag.atom_storage.attempt_insert(I, user, FALSE)))
                continue;
            break;
        }
        return dot;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:150
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob_living user = null, object active = null)
    {
        cleaning = Dm.not(cleaning);
        if (user == null)
            return COMPONENT_NO_DEFAULT_MESSAGE;
        playsound(this, Dm.resource("sound/items/vacuum/vacuum_clack.ogg"), 30, TRUE);
        if (Dm.truthy(cleaning)) //CLEAN_SCRUB because if you get a borg to help you clean up a crime, you deserve to win.
        {
            balloon_alert(user, I18n.T("cleaning"));
            Waiting.NoSleep(AddComponent(
                typeof(datum_component_cleaner),
                Dm.named("base_cleaning_duration", 1 * SECONDS),
                Dm.named("pre_clean_callback", Dm.@new<datum_callback>(this, nameof(clean_sound)))
                ));
        }
        else
        {
            balloon_alert(user, I18n.T("vacuuming"));
            qdel(GetComponent(typeof(datum_component_cleaner)));
        }
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:169
    public virtual object clean_sound()
    {
        playsound(this, Dm.resource("sound/items/vacuum/vacuum_steam.ogg"), 10, TRUE);
        return CLEAN_ALLOWED;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:173
    public virtual async DmTask retract_hose()
    {
        obj_item_borg_cleaner_box cleaner_resolved = cleaner_box?.resolve() as obj_item_borg_cleaner_box;
        if (cleaner_resolved == null)
            throw Dm.CRASH(Dm.text("Somehow [] doesn't have a source to return to!", this), "code/game/objects/items/robot/items/janitor.dm", 174);
        if (loc == cleaner_resolved)
            return;
        do_pickup_animation(cleaner_resolved, get_turf(this));
        await forceMove(cleaner_resolved);
        playsound(cleaner_resolved, Dm.resource("sound/items/vacuum/vacuum_ploop.ogg"), 100);
        if (borg_hose != null && !QDELING(borg_hose))
        {
            balloon_alert_to_hearers(I18n.T("snap"));
            QDEL_NULL(ref borg_hose);
        }
        bag = null;
        cleaner_resolved.deployed = FALSE;
        UnregisterSignal(this, COMSIG_ITEM_DROPPED);
        await cleaner_resolved.update_icon(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:190
    public virtual object generate_hose(mob_living offerer = null, mob_living taker = null)
    {
        datum_beam_held_vacuum generated_borg_hose = Dm.@new<datum_beam_held_vacuum>(taker, offerer, Dm.named("icon_state", "hosebeam"), Dm.named("max_distance", 7), Dm.named("emissive", FALSE), Dm.named("beam_layer", BELOW_MOB_LAYER));
        object index = taker.get_held_index_of_item(this);
        generated_borg_hose.lefthand = IS_LEFT_INDEX(Dm.num(index)) ? TRUE : FALSE;
        Waiting.Detach(generated_borg_hose.Start()); // INVOKE_ASYNC(generated_borg_hose, TYPE_PROC_REF(/datum/beam, Start))
        RegisterSignal(generated_borg_hose, COMSIG_QDELETING, nameof(retract_hose));
        RegisterSignal(generated_borg_hose, COMSIG_BEAM_BEFORE_DRAW, nameof(on_update));
        return generated_borg_hose;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:199
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(Dm.truthy(cleaning) ? I18n.T("<b>Interact</b> to switch to <b>vacuum</b> mode.") : I18n.T("<b>Interact</b> to switch to <b>cleaning</b> mode.")));
        return dot;
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:202
    [SignalHandler]
    public virtual void on_update()
    {
        if (borg_hose.origin is mob_living mob)
        {
            object index = mob.is_holding(this);
            borg_hose.lefthand = IS_LEFT_INDEX(Dm.num(index)) ? TRUE : FALSE;
        }
        if (prob(10))
            playsound(this, Dm.resource("sound/items/vacuum/vacuum_hose.ogg"), 50, TRUE);
    }

    // tg: code/game/objects/items/robot/items/janitor.dm:211
    [SignalHandler]
    public virtual void on_drop(obj_item vacuum = null, mob_living user = null)
    {
        if (user == loc)
            return;
        Waiting.NoSleep(retract_hose());
    }
}
