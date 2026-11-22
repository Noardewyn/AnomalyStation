// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/ed209/ed209.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:1
public partial class mob_living_basic_bot_secbot_ed209 : mob_living_basic_bot_secbot
{
    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:28
    private static DmList sherrif_hats => TgState.Current.mob_living_basic_bot_secbot_ed209__sherrif_hats ??= typecacheof(Dm.list(typeof(obj_item_clothing_head_cowboy))) as DmList;

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:40
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_weapon());
        bot_charge = Dm.@new<datum_action_cooldown_mob_cooldown_ed209_charge>(this);
        Waiting.NoSleep(AddElement(typeof(datum_element_hat_wearer), TgState.Current.mob_living_basic_bot_secbot_ed209__hat_offset ??= Dm.list(2, 0)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_defaceable), Dm.resource("icons/mob/silicon/aibot_faces.dmi"), Dm.list(Dm.kv("ed209", FALSE), Dm.kv("ed209_highlight", TRUE)), "a face"));
        Waiting.NoSleep(AddComponent(typeof(datum_component_stun_n_cuff),
            Dm.named("stun_sound", Dm.resource("sound/items/weapons/egloves.ogg")), Dm.named("handcuff_type", typeof(obj_item_restraints_handcuffs_cable_zipties))));
        Dm.Deferred("code/modules/mob/living/basic/bots/ed209/ed209.dm:58", "Initialize: AddElement(ridable, ride_component): riding the ED-209 (datums/components/riding, section 109)");
        RegisterSignal(this, COMSIG_BASICMOB_POST_ATTACK_RANGED, nameof(post_ranged_attack));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:61
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        sheriffized = Dm.num(Dm.truthy(is_type_in_typecache(arrived, sherrif_hats))); //yeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeehawwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:65
    [SignalHandler]
    public virtual void post_ranged_attack()
    {
        if (!Dm.truthy(sheriffized) || !(shoot_cry <= world.time)) // COOLDOWN_FINISHED, inline
            return;
        shoot_cry = world.time + 30 * SECONDS; // COOLDOWN_START, inline
        Waiting.Detach(say("YIPPIE-KI-YAY!"));
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:72
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        sheriffized = Dm.num(Dm.truthy(is_type_in_typecache(gone, sherrif_hats)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:76
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(sheriffized))
            Dm.append(ref dot, span_notice("Fastest hand in the west."));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:81
    public override async DmTask bot_reset(float? bypass_ai_reset = null)
    {
        await base.bot_reset(bypass_ai_reset);
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) && bot_charge?.owner == null)
            await bot_charge.Grant(this);
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) && bot_charge?.owner != null)
            await bot_charge.Remove(this);
        await set_weapon();
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:89
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        icon_state = Dm.text("ed209[]", Dm.bitand(bot_mode_flags, BOT_MODE_ON));
        await set_weapon();
        balloon_alert(user, "safeties disabled");
        audible_message(span_bolddanger(Dm.text("[] buzzes menacingly!", this)));
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:97
    public virtual async DmTask set_weapon()
    {
        qdel(GetComponent(typeof(datum_component_ranged_attacks)) as datum);
        object projectile = Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) ? emagged_projectile_type : projectile_type;
        object final_projectile_sound = Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) ? emagged_projectile_sound : projectile_sound;
        await AddComponent(typeof(datum_component_ranged_attacks), Dm.named("projectile_type", projectile), Dm.named("projectile_sound", final_projectile_sound));
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:107
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            DmList custom = data["custom_controls"] as DmList;
            custom["handcuff"] = Dm.bitand(security_mode_flags, SECBOT_HANDCUFF_TARGET);
            custom["check_ids"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_IDS);
            custom["check_records"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_RECORDS);
        }
        return data;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:115
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || !isliving(user) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;
        switch (action as string)
        {
            case "handcuff":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_HANDCUFF_TARGET);
                break;
            case "check_ids":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_IDS);
                break;
            case "check_records":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_RECORDS);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:128
    public override async DmTask retrieve_secbot_drops(atom drop_location = null)
    {
        obj_item_bot_assembly_ed209 ed_assembly = Dm.@new<obj_item_bot_assembly_ed209>(drop_location);
        ed_assembly.build_step = ASSEMBLY_FIRST_STEP;
        ed_assembly.add_overlay("hs_hole");
        ed_assembly.created_name = name;
        Dm.@new<obj_item_assembly_prox_sensor>(drop_location);
        obj_item_gun_energy_disabler disabler_gun = Dm.@new<obj_item_gun_energy_disabler>(drop_location);
        disabler_gun.cell.charge = 0;
        disabler_gun.update_appearance();
        if (prob(50))
        {
            Dm.@new<obj_item_bodypart_leg_left_robot>(drop_location);
            if (prob(25))
                Dm.@new<obj_item_bodypart_leg_right_robot>(drop_location);
        }
        if (prob(75))
            return;
        if (prob(50)) // either helmet or vest
            Dm.@new<obj_item_clothing_head_helmet>(drop_location);
        else
            Dm.@new<obj_item_clothing_suit_armor_vest>(drop_location);
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209.dm:148
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        qdel(bot_charge);
        bot_charge = null;
        return dot;
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_secbot_ed209__sherrif_hats;
    internal DmList mob_living_basic_bot_secbot_ed209__hat_offset;
}
