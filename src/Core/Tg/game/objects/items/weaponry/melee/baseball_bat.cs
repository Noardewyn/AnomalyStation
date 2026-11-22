// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/baseball_bat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the kneecapping element it wears (datums/elements/kneecapping.dm). The baseball bat knocks
// whom it hits a tile or two back, aimed at the legs of someone down it breaks the kneecaps, and a thing thrown at its wielder (a
// mob too for the home run and metal bats) is batted back where it came from at twice its force, its thrower lit up and held a
// moment; one in a hundred (half on April Fools) is a cricket bat. The home run bat gathers strength for nine seconds and sends
// its next victim eight to ten tiles away with tg's home run sound; the metal bat reflects lasers. The var blocks are generated
// (D111).

// tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:1
public partial class obj_item_melee_baseball_bat : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_kneecapping)));
        // No subtypes
        if (type != typeof(obj_item_melee_baseball_bat))
            return dot;
        if (prob(Dm.truthy(check_holidays(APRIL_FOOLS)) ? 50 : 1))
            make_silly();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:39
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(homerun_able))
            return await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(homerun_ready))
        {
            to_chat(user, span_warning(I18n.T("You're already ready to do a home run!")));
            return await base.attack_self(user, modifiers, __extra);
        }
        to_chat(user, span_warning(I18n.T("You begin gathering strength...")));
        playsound(get_turf(this), Dm.resource("sound/effects/magic/lightning_chargeup.ogg"), 65, TRUE);
        if (Dm.truthy(await do_after(user, 9 * SECONDS, target: this)))
        {
            to_chat(user, span_userdanger(I18n.T("You gather power! Time for a home run!")));
            homerun_ready = TRUE;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:52
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        // we obtain the relative direction from the bat itself to the target
        float relative_direction = get_cardinal_dir(this, target_mob);
        atom throw_target = get_edge_target_turf(target_mob, relative_direction) as atom;
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
            return dot;
        if (Dm.truthy(homerun_ready))
        {
            user.visible_message(span_userdanger(I18n.T("It's a home run!")));
            if (!QDELETED(target_mob))
                Waiting.Detach(target_mob.throw_at(throw_target, rand(8, 10), 14, user));
            Dm.append(SSexplosions.medturf, throw_target);
            playsound(get_turf(this), Dm.resource("sound/items/weapons/homerun.ogg"), 100, TRUE);
            homerun_ready = FALSE;
            return dot;
        }
        else if (!QDELETED(target_mob) && !Dm.truthy(target_mob.anchored))
        {
            float whack_speed = prob(60) ? 1 : 4;
            Waiting.Detach(target_mob.throw_at(throw_target, rand(1, 2), whack_speed, user, gentle: TRUE)); // sorry friends, 7 speed batting caused wounds to absolutely delete whoever you knocked your target into (and said target)
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:71
    public override object Destroy(float? force = null)
    {
        foreach (object target in Dm.each(thrown_datums))
        {
            datum_thrownthing throw_datum = thrown_datums[target] as datum_thrownthing;
            Waiting.NoSleep(throw_datum.callback.Invoke());
        }
        thrown_datums.Cut();
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:78
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        turf target_turf = get_turf(target);
        if (target_turf == null)
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        foreach (atom_movable atom in Dm.each<atom_movable>(target_turf.contents.Copy()))
        {
            if (!Dm.truthy(try_launch(atom, user)))
                continue;
            return TRUE;
        }
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:88
    public virtual object try_launch(atom_movable target = null, mob_living user = null)
    {
        if (target.throwing == null || (ismob(target) && !Dm.truthy(mob_thrower)))
            return FALSE;
        datum_thrownthing throw_datum = target.throwing;
        object datum_throw_speed = throw_datum.speed;
        float angle = 0;
        float target_to_user = get_dir(target, user);
        if (Dm.truthy(Dm.bitand(target.dir, turn(target_to_user, 90))))
            angle = 270;
        if (Dm.truthy(Dm.bitand(target.dir, turn(target_to_user, 270))))
            angle = 90;
        if (Dm.truthy(Dm.bitand(target.dir, REVERSE_DIR(target_to_user))))
            angle = 180;
        if (Dm.truthy(Dm.bitand(target.dir, target_to_user)))
            angle = 360;
        turf return_to_sender = get_ranged_target_turf_direct(user, throw_datum.starting_turf, max(3, Dm.num(round(target.throw_range * 1.5f, 1))), offset: angle + (rand(-1, 1) * 10)) as turf;
        throw_datum.finalize(hit: FALSE);
        target.mouse_opacity = MOUSE_OPACITY_TRANSPARENT; //dont mess with our ball
        target.color = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 3); //make them super light
        animate(target, Dm.named("time", 0.5f * SECONDS), Dm.named("color", null), Dm.named("flags", ANIMATION_PARALLEL));
        user.color = Dm.list(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 3);
        animate(user, Dm.named("time", 0.5f * SECONDS), Dm.named("color", null), Dm.named("flags", ANIMATION_PARALLEL));
        playsound(this, Dm.resource("sound/items/baseballhit.ogg"), 100, TRUE);
        Waiting.NoSleep(user.do_attack_animation(target, used_item: this));
        ADD_TRAIT(user, TRAIT_IMMOBILIZED, type);
        _addtimer(Dm.@new<datum_callback>(this, nameof(launch_back), target, user, return_to_sender, datum_throw_speed), 0.5f * SECONDS, file: "code/game/objects/items/weaponry/melee/baseball_bat.dm", line: 113);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:116
    public virtual async DmTask launch_back(atom_movable target = null, mob_living user = null, turf target_turf = null, object datum_throw_speed = null)
    {
        playsound(target, Dm.resource("sound/effects/magic/tail_swing.ogg"), 50, TRUE);
        REMOVE_TRAIT(user, TRAIT_IMMOBILIZED, type);
        target.mouse_opacity = Dm.num(Dm.initial(target, (atom p) => p.mouse_opacity));
        target.add_filter("baseball_launch", 3, motion_blur_filter(1, 3) as DmList);
        target.throwforce *= 2;
        await target.throw_at(target_turf, get_dist(target, target_turf), Dm.num(datum_throw_speed) + 1, user, callback: Dm.@new<datum_callback>(this, nameof(on_hit), target));
        thrown_datums[target] = target.throwing;
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:125
    public virtual void make_silly()
    {
        name = I18n.T("cricket bat");
        icon_state = "baseball_bat_brit";
        inhand_icon_state = "baseball_bat_brit";
        desc = pick(I18n.T("You've got red on you."), I18n.T("You gotta know what a crumpet is to understand cricket.")) as string;
    }

    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:131
    public virtual void on_hit(atom_movable target = null)
    {
        target.remove_filter("baseball_launch");
        target.throwforce *= 0.5f;
        Dm.remove(ref thrown_datums, target);
    }
}

// tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:144
public partial class obj_item_melee_baseball_bat_ablative : obj_item_melee_baseball_bat
{
    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:156
    public override async DmTask<object> IsReflect(object def_zone = null) //some day this will reflect thrown items instead of lasers
    {
        await DmTask.CompletedTask;
        return TRUE;
    }
}

// In case you ever want to spawn it via map/admin console
// tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:160
public partial class obj_item_melee_baseball_bat_british : obj_item_melee_baseball_bat
{
    // tg: code/game/objects/items/weaponry/melee/baseball_bat.dm:160
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        make_silly();
        return dot;
    }
}
