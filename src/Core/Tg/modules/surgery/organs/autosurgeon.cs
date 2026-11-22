// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/autosurgeon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/surgery/organs/autosurgeon.dm:1
public partial class obj_item_autosurgeon : obj_item
{
    // tg: code/modules/surgery/organs/autosurgeon.dm:22
    public override async DmTask<object> attack_self_tk(mob user = null) => null; //stops TK fuckery

    // tg: code/modules/surgery/organs/autosurgeon.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(starting_organ))
            Waiting.NoSleep(load_organ(Dm.@new(starting_organ, this) as obj_item_organ));
        return dot;
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:30
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stored_organ != null)
        {
            dot.Add(loaded_overlay);
            // DEVIATION(DV12): . += emissive_appearance(icon, loaded_overlay, src), the glow on the emissive plane
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:36
    public virtual async DmTask load_organ(obj_item_organ loaded_organ = null, mob_living user = null)
    {
        if (user != null)
        {
            if (stored_organ != null)
            {
                to_chat(user, span_alert(I18n.T("[] already has an implant stored.", this)));
                return;
            }

            if (Dm.num(uses) <= 0)
            {
                to_chat(user, span_alert(I18n.T("[] is used up and cannot be loaded with more implants.", this)));
                return;
            }

            if (Dm.truthy(length(organ_whitelist)))
            {
                bool organ_whitelisted = false;
                foreach (object whitelisted_organ in Dm.each(organ_whitelist))
                {
                    if (istype(loaded_organ, whitelisted_organ as Type))
                    {
                        organ_whitelisted = true;
                        break;
                    }
                }
                if (!organ_whitelisted)
                {
                    to_chat(user, span_alert(I18n.T("[] is not compatible with [].", this, loaded_organ)));
                    return;
                }
            }

            if (!Dm.truthy(await user.transferItemToLoc(loaded_organ, this)))
            {
                to_chat(user, span_alert(I18n.T("[] is stuck to your hand!", loaded_organ)));
                return;
            }
        }

        stored_organ = loaded_organ;
        await loaded_organ.forceMove(this);

        name = Dm.text("[] ([])", Dm.initial(this, nameof(name)), stored_organ.name); //to tell you the organ type, like "suspicious autosurgeon (Reviver implant)"
        update_appearance();
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:64
    public virtual async DmTask use_autosurgeon(mob_living target = null, mob_living user = null, object implant_time = null)
    {
        if (stored_organ == null)
        {
            to_chat(user, span_alert(I18n.T("[] currently has no implant stored.", this)));
            return;
        }

        if (Dm.num(uses) <= 0)
        {
            to_chat(user, span_alert(I18n.T("[] has already been used. The tools are dull and won't reactivate.", this)));
            return;
        }

        if (Dm.truthy(implant_time))
        {
            user.visible_message(
                span_notice(I18n.T("[] prepares to use [] on [].", user, this, target)),
                span_notice(I18n.T("You prepare to use [] on [].", this, target)));
            if (!Dm.truthy(await do_after(user, Dm.num(implant_time) * Dm.num(surgery_speed), target)))
                return;
        }

        if (target != user)
        {
            log_combat(user, target, Dm.text("autosurgeon implanted [] into", stored_organ), this.name, Dm.text("in []", AREACOORD(target)));
            user.visible_message(span_notice(I18n.T("[] presses a button on [] as it plunges into []'s body.", user, this, target)), span_notice(I18n.T("You press a button on [] as it plunges into []'s body.", this, target)));
        }
        else
        {
            user.visible_message(
                span_notice(I18n.T("[] presses a button on [] as it plunges into [] body.", user, this, user.p_their())),
                span_notice(I18n.T("You press a button on [] as it plunges into your body.", this)));
        }

        if (stored_organ.valid_zones != null && Dm.truthy(user.get_held_index_of_item(this)))
        {
            DmList checked_zones = Dm.list(user.zone_selected);
            if (IS_RIGHT_INDEX(Dm.num(user.get_held_index_of_item(this))))
                checked_zones.Add(BODY_ZONE_R_ARM, BODY_ZONE_R_LEG);
            else
                checked_zones.Add(BODY_ZONE_L_ARM, BODY_ZONE_L_LEG);

            foreach (object check_zone in Dm.each(checked_zones))
            {
                if (Dm.truthy(stored_organ.valid_zones[check_zone]))
                {
                    await stored_organ.swap_zone(check_zone);
                    break;
                }
            }
        }

        if (!Dm.truthy(await stored_organ.Insert(target as mob_living_carbon))) // insert stored organ into the user
        {
            balloon_alert(user, I18n.T("insertion failed!"));
            return;
        }

        stored_organ = null;
        name = Dm.initial(this, nameof(name)) as string; //get rid of the organ in the name
        playsound(target.loc, Dm.resource("sound/items/weapons/circsawhit.ogg"), 50, TRUE);
        update_appearance();

        uses = Dm.num(uses) - 1;
        if (Dm.num(uses) <= 0)
            desc = Dm.text("[] Looks like it's been used up.", Dm.initial(this, nameof(desc)));
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:119
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra) //when the object it used...
    {
        await use_autosurgeon(user as mob_living, user as mob_living);
        return null;
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:122
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        await use_autosurgeon(target, user, 8 * SECONDS);
        return null;
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:126
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (isorgan(tool))
        {
            await load_organ(tool as obj_item_organ, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/surgery/organs/autosurgeon.dm:132
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item screwtool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, screwtool)))
            return TRUE;
        if (stored_organ == null)
            to_chat(user, span_warning(I18n.T("There's no implant in [] for you to remove!", this)));
        else
        {
            atom drop_loc = user.drop_location() as atom;
            foreach (atom_movable stored_implant in Dm.each<atom_movable>(contents.Copy()))
            {
                await stored_implant.forceMove(drop_loc);
                to_chat(user, span_notice(I18n.T("You remove the [] from [].", stored_organ, this)));
                stored_organ = null;
            }

            screwtool.play_tool_sound(this);
            uses = Dm.num(uses) - 1;
            if (Dm.num(uses) <= 0)
                desc = Dm.text("[] Looks like it's been used up.", Dm.initial(this, nameof(desc)));
            update_appearance(UPDATE_ICON);
        }
        return TRUE;
    }
}

// gaps package P6 (D298): the suspicious autosurgeons (their implants and organs: the cyber implants box, the medkits, the uplinks)
// tg: code/modules/surgery/organs/autosurgeon.dm:155
public partial class obj_item_autosurgeon_syndicate : obj_item_autosurgeon
{
    // tg: code/modules/surgery/organs/autosurgeon.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/surgery/organs/autosurgeon.dm:194
public partial class obj_item_autosurgeon_syndicate_commsagent : obj_item_autosurgeon_syndicate
{
    // tg: code/modules/surgery/organs/autosurgeon.dm:199
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        organ_whitelist.Add(typeof(obj_item_organ_tongue));
        return dot;
    }
}
