// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/pistol.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the Makarov pistol's contraband (a clown pin in one in ten, unremovable) and suppressed kinds and the contraband Desert
// Eagle; syndicate G4c: the M1911's lore and the CH1M911; syndicate N1: the Ansem's lore (the operatives' sidearm); gaps GU1 (D333): the
// Regal Condor's lore, the stickman's flat gun that slips away, the doohickey (the Liberator: its barrel screwed on and off, the double
// damage up close and at gunpoint, the self-destruction with its shrapnel). Left (docs/ai/specs/not-translated.md section 215): the Ansem/SC
// with its underbarrel disruptor (pistol.dm:74, a traitor kit, D333). The var blocks are generated (D111); the automatic base
// (automatic.dm:1) has no procs.

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:36
public partial class obj_item_gun_ballistic_automatic_pistol_contraband : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(10))
        {
            pin = pick(
                typeof(obj_item_firing_pin_clown),
                typeof(obj_item_firing_pin_clown_ultra),
                typeof(obj_item_firing_pin_clown_ultra_selfdestruct));
        }
        object dot = base.Initialize(mapload, __extra);
        (pin as obj_item_firing_pin).pin_removable = FALSE;
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:50
public partial class obj_item_gun_ballistic_automatic_pistol_suppressed : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item_suppressor S = Dm.@new<obj_item_suppressor>(this);
        install_suppressor(S);
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:55
public partial class obj_item_gun_ballistic_automatic_pistol_clandestine : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:63
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("Manufactured by Scarborough Arms, the Ansem is regarded as one of the best sidearms on the market. Minimal recoil combined with high stopping power makes it a favourite amongst soldiers of fortune across known space.<br> <br>The ease of concealment due to the weapon's sleek profile has given it a well-earned reputation as the 'smuggler's gun of course'. While space pirate bands tend to favour the HDS-1 created by Nanotrasen due to their low maintenance cost, the Ansem has seen plenty of use by mercernaries, hitmen and espionage agents. Dozens of seized Ansem pistols line evidence lockups across the Spinward. Many with extremely long and bloodied histories from years of service amongst underground criminal elements.")));
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:183
public partial class obj_item_gun_ballistic_automatic_pistol_deagle_contraband : obj_item_gun_ballistic_automatic_pistol_deagle
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:185
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(10))
        {
            pin = pick(
                typeof(obj_item_firing_pin_clown),
                typeof(obj_item_firing_pin_clown_ultra),
                typeof(obj_item_firing_pin_clown_ultra_selfdestruct));
        }
        object dot = base.Initialize(mapload, __extra);
        (pin as obj_item_firing_pin).pin_removable = FALSE;
        return dot;
    }
}

// Syndicate G4c (D316): the M1911's lore and the monkey gun case's CH1M911, which has none.
// tg: code/modules/projectiles/guns/ballistic/pistol.dm:129
public partial class obj_item_gun_ballistic_automatic_pistol_m1911 : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:141
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("The Colt M1911, created by John Moses Browning centuries ago, is the primordial ancestor of modern automatic firearms. Somehow, to this day, there are still officers who refuse to utilize anything that isn't chambered in 'God's Caliber'.<br><br>Few want to tell any of these people that .45 ACP has struggled to puncture modern ballistic plating for almost five centuries.<br><br>You are VERY confident that this particular example was lathed this decade. Most M1911's seen in the wild today are recreations of the weapon made with modern alloyed metals and manufactoring techniques, mass produced for the consumer market. This does not stop fraudsters from claiming that their weapon is a 'family heirloom' that has saved them from various conflicts that they insist they participated in.")));
    }
}

/**
 * Weak 1911 for syndicate chimps. It comes in a 4 TC kit.
 * 15 damage every.. second? 7 shots to kill. Not fast.
 */
// tg: code/modules/projectiles/guns/ballistic/pistol.dm:157
public partial class obj_item_gun_ballistic_automatic_pistol_m1911_chimpgun : obj_item_gun_ballistic_automatic_pistol_m1911
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:164
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:206  (gaps GU1)
public partial class obj_item_gun_ballistic_automatic_pistol_deagle_regal : obj_item_gun_ballistic_automatic_pistol_deagle
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:224
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("One of the Culling Arms of the Tiger Cooperative, a collection of relic weapons said to have come to Martinson in a frenzied vision from God to enact holy retribution upon the enemies of the faith. Almost all examples of these guns are replicas of the original pattern. This one is no different. However, it carries some of the gravitus of the original firearm. And, some say, the holy might that it wielded against the enemies of the cult.<br><br>The fact that you are looking at this relic weapon means one of two things.<br><br>A) A Tiger Cooperative evangelist has died during or before they could commit a bloody massacre in the name of God. Or <br><br>B) You are that evangelist, and will soon be adding another chapter to this gun's dark history.")));
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:250  (gaps GU1)
public partial class obj_item_gun_ballistic_automatic_pistol_stickman : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:257
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        to_chat(user, span_notice(I18n.T("As you try to manipulate [], it slips out of your possession..", this)));
        if (prob(50))
        {
            to_chat(user, span_notice(I18n.T("..and vanishes from your vision! Where the hell did it go?")));
            qdel(this);
            await user.update_icons();
        }
        else
        {
            to_chat(user, span_notice(I18n.T("..and falls into view. Whew, that was a close one.")));
            await user.dropItemToGround(this);
        }
    }
}

// tg: code/modules/projectiles/guns/ballistic/pistol.dm:271  (gaps GU1)
public partial class obj_item_gun_ballistic_automatic_pistol_doohickey : obj_item_gun_ballistic_automatic_pistol
{
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:268  #define DOORHICKEY_GUN_MIN_DAMAGE 70 (file-local)
    private const float DOORHICKEY_GUN_MIN_DAMAGE = 70;
    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:269  #define DOORHICKEY_GUN_MAX_DAMAGE 140 (file-local)
    private const float DOORHICKEY_GUN_MAX_DAMAGE = 140;

    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:288
    public override async DmTask unload_ammo(mob_living user = null, float? forced = null)
    {
        forced ??= FALSE;
        if (Dm.truthy(forced))
        {
            await base.unload_ammo(user, forced);
            return;
        }

        balloon_alert(user, I18n.T("unscrewing the barrel..."));
        playsound(user, Dm.resource("sound/items/tools/screwdriver_operating.ogg"), 75, FALSE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return;
        }
        await base.unload_ammo(user, forced);
    }

    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:299
    public override async DmTask<object> load_gun(obj_item ammo = null, mob_living user = null)
    {
        object dot = await base.load_gun(ammo, user);
        if (!Dm.truthy(dot))
            return dot;

        balloon_alert(user, I18n.T("screwing the barrel on..."));
        playsound(user, Dm.resource("sound/items/tools/screwdriver_operating.ogg"), 75, FALSE, MEDIUM_RANGE_SOUND_EXTRARANGE);
        if (Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return TRUE;

        balloon_alert(user, I18n.T("interrupted!"));
        await unload_ammo(user, forced: TRUE);
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:313
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        float dmg_multiplier = 1;

        if (get_dist(target, user) <= 1)
            dmg_multiplier *= 2;

        if (isliving(target))
        {
            mob_living victim = (mob_living)target;
            datum_status_effect_grouped_heldup gunpoint = victim.has_status_effect(typeof(datum_status_effect_grouped_heldup)) as datum_status_effect_grouped_heldup;
            if (gunpoint != null)
            {
                foreach (datum_weakref pointer_ref in Dm.each_cast<datum_weakref>(gunpoint.sources))
                {
                    if (Dm.eq(pointer_ref.resolve(), user))
                    {
                        dmg_multiplier *= 1.5f; // Caps at 60 damage
                        break;
                    }
                }
            }
        }

        projectile_damage_multiplier *= dmg_multiplier;
        object dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        projectile_damage_multiplier /= dmg_multiplier;
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:332
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        pointblank ??= FALSE;
        message ??= TRUE;
        object dot = await base.shoot_live_shot(user, pointblank, pbtarget, message);
        if (!Dm.truthy(dot))
            return dot;

        float damage_to_take = rand(DOORHICKEY_GUN_MIN_DAMAGE, DOORHICKEY_GUN_MAX_DAMAGE);
        if (Dm.num(atom_integrity) > damage_to_take)
        {
            await take_damage(damage_to_take);
            return dot;
        }

        playsound(loc, SFX_SHATTER, 75, TRUE);
        if (loc != user)
        {
            await take_damage(damage_to_take);
            return dot;
        }

        float shrapnel_bomb = FALSE;
        obj_item_bodypart_arm poor_sod = user.get_active_hand() as obj_item_bodypart_arm;
        if (prob(damage_to_take - Dm.num(atom_integrity)) && poor_sod != null)
            shrapnel_bomb = TRUE;

        user.visible_message(span_danger(I18n.T("[] explodes into small pieces[]!", this, Dm.truthy(shrapnel_bomb) ? I18n.T(", chunk of it embedding in []'s []", user, user.parse_zone_with_bodypart(poor_sod.body_zone)) : "")),
            span_userdanger(I18n.T("[] explodes into small pieces[]!", this, Dm.truthy(shrapnel_bomb) ? I18n.T(", chunk of it embedding in your []!", poor_sod) : "")),
            span_hear(I18n.T("You can hear sound of plastic shattering.")));

        if (poor_sod != null)
            await poor_sod.receive_damage((damage_to_take - Dm.num(atom_integrity)) * 0.5f, wound_bonus: -10, exposed_wound_bonus: 20, sharpness: SHARP_EDGED, damage_source: this);
        else
            await user.take_bodypart_damage((damage_to_take - Dm.num(atom_integrity)) * 0.5f, wound_bonus: -10, exposed_wound_bonus: 20, sharpness: SHARP_EDGED);

        if (Dm.truthy(shrapnel_bomb))
        {
            obj_item_shrapnel_plastic shrapnel = Dm.@new<obj_item_shrapnel_plastic>(user.loc);
            if (!Dm.truthy(await shrapnel.force_embed(user as mob_living_carbon, poor_sod)))
                qdel(shrapnel);
            else if (!HAS_TRAIT(user, TRAIT_ANALGESIA))
                await user.emote("scream");
        }

        Dm.@new<obj_effect_decal_cleanable_plastic>(get_turf(this));
        await take_damage(damage_to_take);
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/pistol.dm:371
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("The Liberator pattern, according to digital historians, was first posted to a fringe imageboard on the NTNet. The post included a now dead link to a defuncting hosting service through which board members were encouraged to download and 'admire' the design. The thread's author began their post by describing their extreme vitriolic hatred of so-called 'moon men' amongst the Nanotrasen 'elites'. And that, through 'the sauce', they had been shown a means to 'end the oppression of Luna's Tormentors'. They called it the 'Liberator' as a result.<br><br>Response to this thread began as skeptism amongst posters regarding the 3D pattern. Many highlighted the dubious functionality, let alone safe operation of a gun created using cheap plastic and minimal machined parts firing a high pressure round through an unrifled barrel. But the thread suddenly began to gain traction immediately following a particular news article published by the Spinward Dove, a respected editorial in the Spinward Sector. The author of this article recounts that, during a particularly brisk day in New Moscow, they observed three off-duty Nanotrasen assistants attempting to use what the author described as a 'bizarre plastic doohickey' to shoot a mouse at point blank. The horrific aftermath, and images of the rattled but otherwise unscathed mouse, lead many to try and find from where these now crippled assistants had come across such an absurd device. Word spread of the post, and its author's continuously nonsensical rants regarding their hated foe in other areas of the imageboard.<br><br>The author of the thread returned immediately following the news, claiming that those who frequented the imageboard had 'stolen' their 'dreams of freedom', and that clearly the moon men were attempting to discredit their invention through this publicity stunt. A slur-laiden rant from the thread author followed this declaration, leading to the the moderators of the imageboard to step in and close the thread for good.")));
    }
}
