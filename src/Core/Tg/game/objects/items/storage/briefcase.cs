// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/briefcase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.
// Gaps J3 (D298): the briefcase, the lawyer's and the empty one; the folder and papers inside are inert until S3. The briefcase launchpad is the syndicate campaign G2's; the operatives' sniper briefcase N2a's.

// tg: code/game/objects/items/storage/briefcase.dm:2
public partial class obj_item_storage_briefcase : obj_item_storage
{
    // tg: code/game/objects/items/storage/briefcase.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/cuffable_item): cuffing the briefcase to the wrist (the cuffed_item status effect, not ported)
        Dm.Deferred("code/game/objects/items/storage/briefcase.dm:26", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the briefcase to the wrist (the element and the cuffed_item status effect, with the security gear, J4)");
        return dot;
    }

    // tg: code/game/objects/items/storage/briefcase.dm:28
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_pen>(this);
        object folder = Dm.@new(folder_path, this);
        for (float i = 1; i <= 6; i++)
            Dm.@new<obj_item_paper>(folder);
    }

    // tg: code/game/objects/items/storage/briefcase.dm:41
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        DmList papers_found = Dm.list();
        turf item_loc = get_turf(this);

        if (item_loc == null)
            return OXYLOSS;

        foreach (obj_item potentially_paper in Dm.each<obj_item>(contents))
        {
            if (potentially_paper is obj_item_paper || potentially_paper is obj_item_paperplane)
                papers_found.Add(potentially_paper);
        }
        if (!Dm.truthy(Dm.length(papers_found)) || item_loc == null)
        {
            user.visible_message(span_suicide(I18n.T("[] bashes []self in the head with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
            return BRUTELOSS;
        }

        user.visible_message(span_suicide(I18n.T("[] opens [] and all of [] papers fly out!", user, this, user.p_their())));
        foreach (obj_item paper in Dm.each<obj_item>(papers_found)) //Throws the papers in a random direction
        {
            turf turf_to_throw_at = prob(20) ? item_loc : get_ranged_target_turf(item_loc, Dm.num(pick(GLOB.alldirs))) as turf;
            await paper.throw_at(turf_to_throw_at, 2);
        }

        await stoplag(1 * SECONDS);
        await user.say("ARGGHH, HOW WILL I GET THIS WORK DONE NOW?!!");
        user.visible_message(span_suicide(I18n.T("[] looks overwhelmed with paperwork! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/storage/briefcase.dm:35
public partial class obj_item_storage_briefcase_lawyer : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:38
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_stamp_law>(this);
        await base.PopulateContents();
    }
}

// Empty subtype
// tg: code/game/objects/items/storage/briefcase.dm:66
public partial class obj_item_storage_briefcase_empty : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:66
    public override async DmTask PopulateContents()
    {
        return;
    }
}

/**
 * Secure briefcase
 * Uses the lockable storage component to give it a lock.
 */
// tg: code/game/objects/items/storage/briefcase.dm:86  (the syndicate campaign G3b, D316)
public partial class obj_item_storage_briefcase_secure : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_lockable_storage), stored_lock_code));
        RegisterSignal(this, COMSIG_LOCKABLE_STORAGE_SET_CODE, nameof(update_lock_code));
        return dot;
    }

    // tg: code/game/objects/items/storage/briefcase.dm:99
    [SignalHandler]
    public virtual void update_lock_code(obj_item_storage_briefcase_secure briefacase = null, object new_code = null)
    {
        stored_lock_code = new_code;
    }
}

/// Base container used for gimmick disks.
// tg: code/game/objects/items/storage/briefcase.dm:105
public partial class obj_item_storage_briefcase_secure_digital_storage : obj_item_storage_briefcase_secure
{
    // tg: code/game/objects/items/storage/briefcase.dm:112
    public override async DmTask PopulateContents()
    {
        return;
    }
}

///Syndie variant of Secure Briefcase. Contains space cash, slightly more robust.
// tg: code/game/objects/items/storage/briefcase.dm:116
public partial class obj_item_storage_briefcase_secure_syndie : obj_item_storage_briefcase_secure
{
    // tg: code/game/objects/items/storage/briefcase.dm:119
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (int iterator = 1; iterator <= 5; iterator++)
            Dm.@new<obj_item_stack_spacecash_c1000>(this);
    }
}

// tg: code/game/objects/items/storage/briefcase.dm:125
public partial class obj_item_storage_briefcase_secure_riches : obj_item_storage_briefcase_secure
{
    // tg: code/game/objects/items/storage/briefcase.dm:125
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_suit_armor_vest>(this); // tg: code/game/objects/items/storage/briefcase.dm:126
        Dm.@new<obj_item_gun_ballistic_automatic_pistol>(this); // tg: code/game/objects/items/storage/briefcase.dm:127
        Dm.@new<obj_item_suppressor>(this); // tg: code/game/objects/items/storage/briefcase.dm:128
        Dm.@new<obj_item_melee_baton_telescopic>(this); // tg: code/game/objects/items/storage/briefcase.dm:129
        Dm.@new<obj_item_clothing_mask_balaclava>(this); // tg: code/game/objects/items/storage/briefcase.dm:130
        Dm.@new<obj_item_bodybag>(this); // tg: code/game/objects/items/storage/briefcase.dm:131
        Dm.@new<obj_item_soap_nanotrasen>(this); // tg: code/game/objects/items/storage/briefcase.dm:132
    }
}

// tg: code/game/objects/items/storage/briefcase.dm:134  (the syndicate campaign G3b, D316)
public partial class obj_item_storage_briefcase_hitchiker : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:134
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_food_sandwich_peanut_butter_jelly>(this);
        Dm.@new<obj_item_food_sandwich_peanut_butter_jelly>(this);
        Dm.@new<obj_item_reagent_containers_cup_glass_waterbottle_large>(this);
        Dm.@new<obj_item_soap>(this);
        Dm.@new<obj_item_pillow_random>(this);
        Dm.@new<obj_item_tank_internals_emergency_oxygen>(this);
        Dm.@new<obj_item_tank_internals_emergency_oxygen>(this);
    }
}

//Briefcase item that contains the launchpad.
// tg: code/game/objects/items/storage/briefcase.dm:144  (the syndicate campaign G2, D316)
public partial class obj_item_storage_briefcase_launchpad : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:147
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        pad = Dm.@new<obj_machinery_launchpad_briefcase>(null, this); //spawns pad in nullspace to hide it from briefcase contents
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/storage/briefcase.dm:151
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(pad))
            qdel(pad);
        pad = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/storage/briefcase.dm:157
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_pen>(this);
        Dm.@new<obj_item_launchpad_remote>(this, pad);
    }

    // tg: code/game/objects/items/storage/briefcase.dm:161
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!isturf(user.loc)) //no setting up in a locker
            return null;
        add_fingerprint(user);
        user.visible_message(span_notice(I18n.T("[] starts setting down []...", user, this)), span_notice(I18n.T("You start setting up []...", pad)));
        if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
        {
            await pad.forceMove(get_turf(this));
            pad.update_indicator();
            pad.closed = FALSE;
            await user.transferItemToLoc(this, pad, TRUE);
            atom_storage.close_all();
        }
        return null;
    }

    // tg: code/game/objects/items/storage/briefcase.dm:173
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_launchpad_remote)))
            return await base.tool_act(user, tool, modifiers);
        obj_item_launchpad_remote remote = (obj_item_launchpad_remote)tool;
        if (Dm.eq(remote.pad, WEAKREF(this.pad)))
            return await base.tool_act(user, tool, modifiers);
        remote.pad = WEAKREF(this.pad) as datum_weakref;
        to_chat(user, span_notice(I18n.T("You link [] to [].", pad, remote)));
        return ITEM_INTERACT_BLOCKING;
    }
}

// Syndicate G4a (D316): the briefcase gun, which fires the gun inside it.
// tg: code/game/objects/items/storage/briefcase.dm:183
public partial class obj_item_storage_briefcase_gun : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:186
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        foreach (obj_item_gun weapon in Dm.each<obj_item_gun>(this))
            return await weapon.try_fire_gun(interacting_with, user, list2params(modifiers));
        return NONE;
    }

    // tg: code/game/objects/items/storage/briefcase.dm:191
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (Dm.truthy(user.is_holding(this)))
            dot.Add(span_notice(I18n.T("Upon closer inspection, you notice a hole in the side of the briefcase.")));
        return dot;
    }
}

// tg: code/game/objects/items/storage/briefcase.dm:196
public partial class obj_item_storage_briefcase_gun_preloaded : obj_item_storage_briefcase_gun
{
    // tg: code/game/objects/items/storage/briefcase.dm:198
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_gun_ballistic_automatic_pistol>(this);
        await base.PopulateContents();
    }
}

// tg: code/game/objects/items/storage/briefcase.dm:69
public partial class obj_item_storage_briefcase_sniper : obj_item_storage_briefcase
{
    // tg: code/game/objects/items/storage/briefcase.dm:73
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents(); // in case you need any paperwork done after your rampage
        Dm.@new<obj_item_gun_ballistic_rifle_sniper_rifle_syndicate>(this);
        Dm.@new<obj_item_clothing_neck_tie_red_hitman>(this);
        Dm.@new<obj_item_clothing_under_syndicate_sniper>(this);
        Dm.@new<obj_item_ammo_box_magazine_sniper_rounds>(this);
        Dm.@new<obj_item_ammo_box_magazine_sniper_rounds>(this);
        Dm.@new<obj_item_ammo_box_magazine_sniper_rounds_disruptor>(this);
    }
}

