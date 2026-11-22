// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/netherworld/migo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:1
public partial class mob_living_basic_migo : mob_living_basic
{
    private DmList migo_sounds => TgState.Current.mob_living_basic_migo__migo_sounds ??= Dm.list(
            
Dm.resource("sound/items/bubblewrap.ogg"),
            Dm.resource("sound/items/tools/change_jaws.ogg"),
            Dm.resource("sound/items/tools/crowbar.ogg"),
            Dm.resource("sound/items/drink.ogg"),
            Dm.resource("sound/items/deconstruct.ogg"),
            Dm.resource("sound/items/carhorn.ogg"),
            Dm.resource("sound/items/tools/change_drill.ogg"),
            Dm.resource("sound/items/dodgeball.ogg"),
            Dm.resource("sound/items/eatfood.ogg"),
            Dm.resource("sound/items/megaphone.ogg"),
            Dm.resource("sound/items/tools/screwdriver.ogg"),
            Dm.resource("sound/items/weeoo1.ogg"),
            Dm.resource("sound/items/tools/wirecutter.ogg"),
            Dm.resource("sound/items/tools/welder.ogg"),
            Dm.resource("sound/items/zip/zip.ogg"),
            Dm.resource("sound/items/tools/rped.ogg"),
            Dm.resource("sound/items/tools/ratchet.ogg"),
            Dm.resource("sound/items/polaroid/polaroid1.ogg"),
            Dm.resource("sound/items/pshoom/pshoom.ogg"),
            Dm.resource("sound/items/airhorn/airhorn.ogg"),
            Dm.resource("sound/items/geiger/high1.ogg"),
            Dm.resource("sound/items/geiger/high2.ogg"),
            Dm.resource("sound/mobs/non-humanoids/beepsky/creep.ogg"),
            Dm.resource("sound/mobs/non-humanoids/beepsky/iamthelaw.ogg"),
            Dm.resource("sound/mobs/non-humanoids/ed209/ed209_20sec.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/hiss3.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/hiss6.ogg"),
            Dm.resource("sound/mobs/non-humanoids/medbot/patchedup.ogg"),
            Dm.resource("sound/mobs/non-humanoids/medbot/feelbetter.ogg"),
            Dm.resource("sound/mobs/humanoids/human/laugh/manlaugh1.ogg"),
            Dm.resource("sound/mobs/humanoids/human/laugh/womanlaugh.ogg"),
            Dm.resource("sound/items/weapons/sear.ogg"),
            Dm.resource("sound/music/antag/clockcultalr.ogg"),
            Dm.resource("sound/music/antag/ling_alert.ogg"),
            Dm.resource("sound/music/antag/traitor/tatoralert.ogg"),
            Dm.resource("sound/music/antag/monkey.ogg"),
            Dm.resource("sound/vehicles/mecha/nominal.ogg"),
            Dm.resource("sound/vehicles/mecha/weapdestr.ogg"),
            Dm.resource("sound/vehicles/mecha/critdestr.ogg"),
            Dm.resource("sound/vehicles/mecha/imag_enh.ogg"),
            Dm.resource("sound/effects/adminhelp.ogg"),
            Dm.resource("sound/effects/alert.ogg"),
            Dm.resource("sound/effects/blob/attackblob.ogg"),
            Dm.resource("sound/effects/bamf.ogg"),
            Dm.resource("sound/effects/blob/blobattack.ogg"),
            Dm.resource("sound/effects/break_stone.ogg"),
            Dm.resource("sound/effects/bubbles/bubbles.ogg"),
            Dm.resource("sound/effects/bubbles/bubbles2.ogg"),
            Dm.resource("sound/effects/clang.ogg"),
            Dm.resource("sound/effects/clockcult_gateway_disrupted.ogg"),
            Dm.resource("sound/effects/footstep/clownstep2.ogg"),
            Dm.resource("sound/effects/curse/curse1.ogg"),
            Dm.resource("sound/effects/dimensional_rend.ogg"),
            Dm.resource("sound/effects/doorcreaky.ogg"),
            Dm.resource("sound/effects/empulse.ogg"),
            Dm.resource("sound/effects/explosion/explosion_distant.ogg"),
            Dm.resource("sound/effects/explosion/explosionfar.ogg"),
            Dm.resource("sound/effects/explosion/explosion1.ogg"),
            Dm.resource("sound/effects/grillehit.ogg"),
            Dm.resource("sound/effects/genetics.ogg"),
            Dm.resource("sound/effects/heart_beat.ogg"),
            Dm.resource("sound/runtime/hyperspace/hyperspace_begin.ogg"),
            Dm.resource("sound/runtime/hyperspace/hyperspace_end.ogg"),
            Dm.resource("sound/effects/his_grace/his_grace_awaken.ogg"),
            Dm.resource("sound/effects/pai_boot.ogg"),
            Dm.resource("sound/effects/phasein.ogg"),
            Dm.resource("sound/effects/pickaxe/picaxe1.ogg"),
            Dm.resource("sound/effects/sparks/sparks1.ogg"),
            Dm.resource("sound/effects/smoke.ogg"),
            Dm.resource("sound/effects/splat.ogg"),
            Dm.resource("sound/effects/snap.ogg"),
            Dm.resource("sound/effects/tendril_destroyed.ogg"),
            Dm.resource("sound/effects/supermatter.ogg"),
            Dm.resource("sound/effects/desecration/desecration-01.ogg"),
            Dm.resource("sound/effects/desecration/desecration-02.ogg"),
            Dm.resource("sound/effects/desecration/desecration-03.ogg"),
            Dm.resource("sound/announcer/alarm/bloblarm.ogg"),
            Dm.resource("sound/announcer/alarm/airraid.ogg"),
            Dm.resource("sound/misc/bang.ogg"),
            Dm.resource("sound/misc/highlander.ogg"),
            Dm.resource("sound/misc/interference.ogg"),
            Dm.resource("sound/announcer/notice/notice1.ogg"),
            Dm.resource("sound/announcer/notice/notice2.ogg"),
            Dm.resource("sound/misc/sadtrombone.ogg"),
            Dm.resource("sound/misc/slip.ogg"),
            Dm.resource("sound/misc/splort.ogg"),
            Dm.resource("sound/items/weapons/armbomb.ogg"),
            Dm.resource("sound/items/weapons/beam_sniper.ogg"),
            Dm.resource("sound/items/weapons/chainsawhit.ogg"),
            Dm.resource("sound/items/weapons/emitter.ogg"),
            Dm.resource("sound/items/weapons/emitter2.ogg"),
            Dm.resource("sound/items/weapons/blade1.ogg"),
            Dm.resource("sound/items/weapons/bladeslice.ogg"),
            Dm.resource("sound/items/weapons/blastcannon.ogg"),
            Dm.resource("sound/items/weapons/blaster.ogg"),
            Dm.resource("sound/items/weapons/bulletflyby3.ogg"),
            Dm.resource("sound/items/weapons/circsawhit.ogg"),
            Dm.resource("sound/items/weapons/cqchit2.ogg"),
            Dm.resource("sound/items/weapons/drill.ogg"),
            Dm.resource("sound/items/weapons/genhit1.ogg"),
            Dm.resource("sound/items/weapons/gun/pistol/shot_suppressed.ogg"),
            Dm.resource("sound/items/weapons/gun/pistol/shot.ogg"),
            Dm.resource("sound/items/weapons/handcuffs.ogg"),
            Dm.resource("sound/items/weapons/homerun.ogg"),
            Dm.resource("sound/items/weapons/kinetic_accel.ogg"),
            Dm.resource("sound/machines/clockcult/steam_whoosh.ogg"),
            Dm.resource("sound/machines/fryer/deep_fryer_emerge.ogg"),
            Dm.resource("sound/machines/airlock/airlock.ogg"),
            Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"),
            Dm.resource("sound/machines/airlock/airlockclose.ogg"),
            Dm.resource("sound/machines/airlock/airlockforced.ogg"),
            Dm.resource("sound/machines/airlock/airlockopen.ogg"),
            Dm.resource("sound/announcer/alarm/nuke_alarm.ogg"),
            Dm.resource("sound/machines/blender.ogg"),
            Dm.resource("sound/machines/airlock/boltsdown.ogg"),
            Dm.resource("sound/machines/airlock/boltsup.ogg"),
            Dm.resource("sound/machines/buzz/buzz-sigh.ogg"),
            Dm.resource("sound/machines/buzz/buzz-two.ogg"),
            Dm.resource("sound/machines/chime.ogg"),
            Dm.resource("sound/machines/cryo_warning.ogg"),
            Dm.resource("sound/machines/defib/defib_charge.ogg"),
            Dm.resource("sound/machines/defib/defib_failed.ogg"),
            Dm.resource("sound/machines/defib/defib_ready.ogg"),
            Dm.resource("sound/machines/defib/defib_zap.ogg"),
            Dm.resource("sound/machines/beep/deniedbeep.ogg"),
            Dm.resource("sound/machines/ding.ogg"),
            Dm.resource("sound/machines/disposalflush.ogg"),
            Dm.resource("sound/machines/door/door_close.ogg"),
            Dm.resource("sound/machines/door/door_open.ogg"),
            Dm.resource("sound/machines/engine_alert/engine_alert1.ogg"),
            Dm.resource("sound/machines/engine_alert/engine_alert2.ogg"),
            Dm.resource("sound/machines/hiss.ogg"),
            Dm.resource("sound/mobs/non-humanoids/honkbot/honkbot_evil_laugh.ogg"),
            Dm.resource("sound/machines/juicer.ogg"),
            Dm.resource("sound/machines/ping.ogg"),
            Dm.resource("sound/ambience/misc/signal.ogg"),
            Dm.resource("sound/machines/synth/synth_no.ogg"),
            Dm.resource("sound/machines/synth/synth_yes.ogg"),
            Dm.resource("sound/machines/terminal/terminal_alert.ogg"),
            Dm.resource("sound/machines/beep/triple_beep.ogg"),
            Dm.resource("sound/machines/beep/twobeep.ogg"),
            Dm.resource("sound/machines/ventcrawl.ogg"),
            Dm.resource("sound/machines/warning-buzzer.ogg"),
            Dm.resource("sound/announcer/default/outbreak5.ogg"),
            Dm.resource("sound/announcer/default/outbreak7.ogg"),
            Dm.resource("sound/announcer/default/poweroff.ogg"),
            Dm.resource("sound/announcer/default/radiation.ogg"),
            Dm.resource("sound/announcer/default/shuttlecalled.ogg"),
            Dm.resource("sound/announcer/default/shuttledock.ogg"),
            Dm.resource("sound/announcer/default/shuttlerecalled.ogg"),
            Dm.resource("sound/announcer/default/aimalf.ogg")); //hahahaha fuck you code divers

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!istype(this, typeof(mob_living_basic_migo_hatsune)) && prob(0.1f)) // chance on-load mi-gos will spawn with a miku wig on (shiny variant)
        {
            Dm.@new<mob_living_basic_migo_hatsune>(get_turf(loc), mapload);
            return INITIALIZE_HINT_QDEL;
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_NETHER, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 0f));
        Waiting.NoSleep(AddComponent(typeof(datum_component_health_scaling_effects), Dm.named("min_health_slowdown", -1.5f), Dm.named("additional_status_callback", Dm.@new<datum_callback>(this, nameof(update_dodge_chance)))));
        return dot;
    }

    /// Makes the migo more likely to dodge around the more damaged it is
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:50
    public virtual void update_dodge_chance(object health_ratio = null)
    {
        dodge_prob = LERP(50, 10, Dm.num(health_ratio));
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:54
    public virtual void make_migo_sound()
    {
        playsound(this, pick(migo_sounds), 50, TRUE);
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:57
    public override async DmTask send_speech(object message = null, float? range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        await base.send_speech(message, range, source, bubble_type, spans, message_language, message_mods, forced, tts_message, tts_filter);
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return;
        make_migo_sound();
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:63
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        float spt = Dm.num(seconds_per_tick ?? SSMOBS_DT());
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return dot;
        if (SPT_PROB(5, spt))
            make_migo_sound();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:72
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (!Dm.truthy(dodging) && !Dm.truthy(ckey) && prob(dodge_prob) && Dm.num(moving_diagonally) == 0 && isturf(loc) && isturf(newloc))
            return await dodge(newloc, direct);
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:78
    public virtual async DmTask<object> dodge(atom moving_to = null, object move_direction = null)
    {
        //Assuming we move towards the target we want to swerve toward them to get closer
        object cdir = Dm.turn(move_direction, 45);
        object ccdir = Dm.turn(move_direction, -45);
        dodging = TRUE;
        object dot = await Move(get_step(loc, Dm.num(pick(cdir, ccdir))) as atom);
        if (!Dm.truthy(dot)) //Can't dodge there so we just carry on
            dot = await Move(moving_to, move_direction);
        dodging = FALSE;
        return dot;
    }
}

/// The special hatsune miku themed mi-go.
// tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:90
public partial class mob_living_basic_migo_hatsune : mob_living_basic_migo
{
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:99
    public override void make_migo_sound()
    {
        playsound(this, Dm.resource("sound/mobs/non-humanoids/tourist/tourist_talk_japanese1.ogg"), 50, TRUE);
    }

    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_instrument_piano_synth)));
        return dot;
    }
}

public partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/netherworld/migo.dm:33  var/static/list/migo_sounds
    internal DmList mob_living_basic_migo__migo_sounds;
}
