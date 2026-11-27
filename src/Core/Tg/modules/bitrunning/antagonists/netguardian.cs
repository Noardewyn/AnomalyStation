// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/netguardian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the netguardian prime, the flying war machine a heavily threatened server spawns as its glitch (its burst-firing
// SMG, its rocket barrage with the scan and rockets overlays, its sparks and sound on death) and its AI (netguardian.bt.json). The
// var blocks are generated.

// tg: code/modules/bitrunning/antagonists/netguardian.dm:7
public partial class mob_living_basic_netguardian : mob_living_basic
{
    // tg: code/modules/bitrunning/antagonists/netguardian.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks),
            Dm.named("casing_type", typeof(obj_item_ammo_casing_c46x30mm)),
            Dm.named("projectile_sound", Dm.resource("sound/items/weapons/gun/smg/shot.ogg")),
            Dm.named("burst_shots", 6)));

        datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_netguardian rockets = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_netguardian>(this);
        Waiting.NoSleep(rockets.Grant(this));
        ((datum_ai_controller)ai_controller).set_blackboard_key(BB_NETGUARDIAN_ROCKET_ABILITY, rockets);

        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/bitrunning/antagonists/netguardian.dm:72
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        do_sparks(number: 3, cardinal_only: TRUE, source: this);
        playsound(this, Dm.resource("sound/vehicles/mecha/weapdestr.ogg"), 100);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/bitrunning/antagonists/netguardian.dm:77
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;
        // DEVIATION(DV12): . += emissive_appearance(icon, "netguardian_emissive", src), the glow on the emissive plane, is not created
        Dm.Deferred("code/modules/bitrunning/antagonists/netguardian.dm:81", "update_overlays: emissive_appearance of the netguardian's lights (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/modules/bitrunning/antagonists/netguardian.dm:83
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_netguardian : datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire
{
    // tg: code/modules/bitrunning/antagonists/netguardian.dm:92
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        mob_living player = owner as mob_living;
        playsound(player, Dm.resource("sound/vehicles/mecha/skyfall_power_up.ogg"), 120);
        await player.say(I18n.T("target acquired."), "machine");

        object overlay_icon = Dm.resource("icons/mob/nonhuman-player/netguardian.dmi");
        DmList overlays = Dm.list();
        Dm.append(ref overlays, mutable_appearance(overlay_icon, "scan"));
        Dm.append(ref overlays, mutable_appearance(overlay_icon, "rockets"));
        // DEVIATION(DV12): overlays += emissive_appearance(overlay_icon, "scan", player), the glow on the emissive plane, is not created
        Dm.Deferred("code/modules/bitrunning/antagonists/netguardian.dm:101", "Activate: emissive_appearance of the scan (DEVIATION DV12)");
        player.add_overlay(overlays);

        StartCooldown();
        if (!Dm.truthy(await do_after(player, 1.5f * SECONDS)))
        {
            player.balloon_alert(player, I18n.T("cancelled"));
            StartCooldown(cooldown_time * 0.2f);
            player.cut_overlay(overlays);
            return TRUE;
        }

        player.cut_overlay(overlays);
        await attack_sequence(owner as mob_living, target_atom);
        return TRUE;
    }
}
