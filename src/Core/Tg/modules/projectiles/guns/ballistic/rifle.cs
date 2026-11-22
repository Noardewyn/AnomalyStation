// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/rifle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4b, D316): the bolt rifle base and the rebar crossbows (the syndicate one of the uplink, the engineers'
// handcrafted and stressed ones come with it); N2a: the anti-materiel sniper rifle (the operatives' one too); gaps GU1 (D333): the
// Sakhno and its surplus, Zhihao and Donk kinds (jamming, the sawn-off Obrez), the pipe guns and pipe pistols, the SKS. Left
// (docs/ai/specs/not-translated.md section 215): the ballistic harpoon gun's lore (rifle.dm:147, an uplink kit) and the enchanted rifle of
// the wizard's summon guns (rifle.dm:449, D333). The var blocks are generated (D111). The crossbow's own rack and drop_bolt report the
// first-person motion like the base's (DV213).

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:1
public partial class obj_item_gun_ballistic_rifle : obj_item_gun_ballistic
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:22
    public override async DmTask rack(mob user = null)
    {
        if (bolt_locked == FALSE)
        {
            balloon_alert(user, I18n.T("bolt opened"));
            playsound(this, rack_sound, rack_sound_volume, rack_sound_vary);
            await process_chamber(FALSE, FALSE, FALSE);
            bolt_locked = TRUE;
            HeldMotionSeam.Report(user, HeldMotion.Rack); // DEVIATION(DV213): the user's first-person gun moves
            update_appearance();
            return;
        }
        await drop_bolt(user);
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:33
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(need_bolt_lock_to_interact) && !Dm.truthy(bolt_locked) && tool is not obj_item_knife)
        {
            balloon_alert(user, I18n.T("bolt closed!"));
            return null;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:40
    public override object can_shoot()
    {
        if (Dm.truthy(bolt_locked))
            return FALSE;
        return base.can_shoot();
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The bolt is [].", Dm.truthy(bolt_locked) ? "open" : "closed"));
        return dot;
    }
}

///////////////////////
// BOLT ACTION RIFLE //
///////////////////////

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:53
public partial class obj_item_gun_ballistic_rifle_boltaction : obj_item_gun_ballistic_rifle
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:82
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("The Sakhno Precision Rifle's origins are closely tied to the Sakhno Concern, one of the Third Soviet Union's state-ran weapons manufacturing concerns before the Union's collapse in the February Revolution, and subsequently one of the Spinward Stellar Coalition's favored industrial concerns.<br><br>Designed to be easily manufactured with the tooling and equipment that the nascent TSU had on-hand, and with enough reliability and stopping power for all sorts of purposes, from hunting to home defense, the Sakhno became a mainstay for Soviet citizens since 2440... and, subsequently, a mainstay when those citizens became conspirators and combatants against dictatorship.<br><br>Even to this day, though, Sakhnos in various conditions and states of modification end up all around the Spinward Sector. Many are manufactured or refurbished with new internals, with reliability to match their history, while others are from stockpiles of surplus or salvage, scavenged and refurbished time and time again... which typically isn't great for reliability.")));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:99
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 41), Dm.named("offset_y", 14), Dm.named("bayonet_overlay", "bayonet_thin"));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:102
    public override object do_sawoff()
    {
        object dot = base.do_sawoff();
        spread = 36;
        pixel_x = 0; // SET_BASE_PIXEL(0, 0) (__DEFINES/visual_helpers.dm:3), inline
        base_pixel_x = 0;
        pixel_y = 0;
        base_pixel_y = 0;
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:107
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(jammed))
        {
            if (prob(unjam_chance))
            {
                jammed = FALSE;
                unjam_chance = Dm.initial(this, (obj_item_gun_ballistic_rifle_boltaction p) => p.unjam_chance);
            }
            else
            {
                unjam_chance += 10;
                balloon_alert(user, I18n.T("jammed!"));
                playsound(user, Dm.resource("sound/items/weapons/jammed.ogg"), 75, TRUE);
                return FALSE;
            }
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:119
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        if (Dm.truthy(can_jam))
        {
            if (chambered.loaded_projectile != null)
            {
                if (prob(jamming_chance))
                    jammed = TRUE;
                jamming_chance += jamming_increment;
                jamming_chance = clamp(jamming_chance, 0, 100);
            }
        }
        return await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:128
    public override async DmTask<object> blow_up(mob user = null)
    {
        object dot = FALSE;
        if (chambered?.loaded_projectile != null)
        {
            await process_fire(user, user as mob_living, FALSE);
            dot = TRUE;
        }
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:162
public partial class obj_item_gun_ballistic_rifle_boltaction_surplus : obj_item_gun_ballistic_rifle_boltaction
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:176
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 41), Dm.named("offset_y", 14), Dm.named("bayonet_overlay", "bayonet_thin_surplus"));
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:179
public partial class obj_item_gun_ballistic_rifle_boltaction_prime : obj_item_gun_ballistic_rifle_boltaction
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:195
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 1.5f)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:199
    public override object do_sawoff()
    {
        object dot = base.do_sawoff();
        name = I18n.T("\\improper Obrez Moderna"); // wear it loud and proud
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:203
public partial class obj_item_gun_ballistic_rifle_boltaction_donkrifle : obj_item_gun_ballistic_rifle_boltaction
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:222
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("Donk Co.'s Jezail line of bolt-action sporting rifles is, admittedly, a strange, almost archaic design, coming from an even stranger place, as most people would not expect a logistics and food processing company to create, market, and distribute bolt-action rifles.<br><br>For better or for worse, though, they're pretty good as rifles - though they're not nearly as ubiquitous as the Sakhno, especially in the Spinward Sector, they still share the same chambering in .310 Strilka, and thus, the stopping power of the cartridge, with no risk of receiving a refurbished, poorly-kept surplus arm that jams more than it shoots.")));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:234
    public override object do_sawoff() //the heavy price one pays for fitting this in a backpack
    {
        object dot = base.do_sawoff();
        projectile_damage_multiplier = 0.75f;
        spread = 50;
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:239
public partial class obj_item_gun_ballistic_rifle_rebarxbow : obj_item_gun_ballistic_rifle
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:271
    public override async DmTask rack(mob user = null)
    {
        if (Dm.truthy(bolt_locked))
        {
            await drop_bolt(user);
            return;
        }
        balloon_alert(user, I18n.T("bowstring loosened"));
        playsound(this, rack_sound, rack_sound_volume, rack_sound_vary);
        await handle_chamber(empty_chamber: FALSE, from_firing: FALSE, chamber_next_round: FALSE);
        bolt_locked = TRUE;
        HeldMotionSeam.Report(user, HeldMotion.Rack); // DEVIATION(DV213): the user's first-person gun moves
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:281
    public override async DmTask drop_bolt(mob user = null)
    {
        if (!Dm.truthy(await do_after(user, draw_time, target: this, timed_action_flags: doafter_flags)))
            return;
        playsound(this, bolt_drop_sound, bolt_drop_sound_volume, FALSE);
        balloon_alert(user, I18n.T("bowstring drawn"));
        chamber_round();
        bolt_locked = FALSE;
        HeldMotionSeam.Report(user, HeldMotion.Rack); // DEVIATION(DV213): the user's first-person gun moves
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:290
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        await base.shoot_live_shot(user, pointblank, pbtarget, message);
        await rack();
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:294
    public override object can_shoot()
    {
        if (Dm.truthy(bolt_locked))
            return FALSE;
        return base.can_shoot();
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:299
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        if (chambered != null || magazine == null || !Dm.truthy(length(magazine.contents)))
        {
            await base.shoot_with_empty_chamber(user);
            return;
        }
        await drop_bolt(user);
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:304
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The crossbow is [] to fire.", Dm.truthy(bolt_locked) ? "not ready" : "ready"));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:308
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (magazine == null)
            Dm.append(ref dot, Dm.text("[]", Dm.initial(this, (obj_item_gun_ballistic_rifle_rebarxbow p) => p.icon_state)) + "_empty");
        if (!Dm.truthy(bolt_locked))
            Dm.append(ref dot, Dm.text("[]", Dm.initial(this, (obj_item_gun_ballistic_rifle_rebarxbow p) => p.icon_state)) + "_bolt_locked");
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:326
public partial class obj_item_gun_ballistic_rifle_rebarxbow_syndie : obj_item_gun_ballistic_rifle_rebarxbow
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:341
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 2))); //enough range to at least be useful for stealth
        return dot;
    }
}

/// PIPE GUNS ///

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:347
public partial class obj_item_gun_ballistic_rifle_boltaction_pipegun : obj_item_gun_ballistic_rifle_boltaction
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:365
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to recall a tale about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("<b>You were told this story, in hushed tones, from a wizened man in a grey jumpsuit...</b><br><br>It is said that the first slaying committed on a Nanotrasen space station was by an assistant.<br><br>That this act, done by toolbox, maybe spear, was what consigned their kind to a life of destitution, rejection and violence.<br><br>They carry the weight of this act visibly; the grey jumpsuit. Breathing deeply filtered air. And with bloodsoaked yellow hands clenched into fists.<br><br>Eyes, sharp and waiting. Hunters in the dark.<br><br>Eventually, these killing spirits sought to stake a claim on the metal tombs they were trapped within. Rejecting their status. Determined to be something more.<br><br>This weapon is one such tool. And it is a grim one indeed - wrought from scrap, pulled from the station's walls and floors and the very nails holding it together.<br><br>It is a symbol that the true masters of this place are not those who merely inhabit it, but the one willing to twist it towards a killing intent.")));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:379
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 35), Dm.named("offset_y", 10));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:382
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
        do_sparks(1, TRUE, this);
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:389
public partial class obj_item_gun_ballistic_rifle_boltaction_pipegun_pistol : obj_item_gun_ballistic_rifle_boltaction_pipegun
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:409
    public override async DmTask add_bayonet_point()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:489
public partial class obj_item_gun_ballistic_rifle_sniper_rifle : obj_item_gun_ballistic_rifle
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:521
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_warning(I18n.T("<b>It seems to have a warning label:</b> Do NOT, under any circumstances, attempt to 'quickscope' with this rifle.")));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:525
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 4))); //enough range to at least make extremely good use of the penetrator rounds
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:529
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore), Dm.named("lore", I18n.T("Modern arms and armor technology has come a long way since man's first strides into space, from high-throughput directed energy weaponry to advanced energy shielding and everything in between. Such advancements in technology, then, should have put the role of the anti-materiel rifle to rest, as other solutions to the armor problems it solves would have become more accessible - in theory, anyway.<br><br>In practice, the problem with advanced armor is that it's expensive and/or heavy - both of which are constraints that limit the usage of such defensive solutions, especially on smaller targets such as station-ready exosuits or human-scale MODsuits.<br><br>These constraints gave the anti-materiel rifle concept a new lease on life, which Scarborough Arms is happy to provide to prospective buyers.")));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:544
    public override void reset_fire_cd()
    {
        base.reset_fire_cd();
        if (Dm.truthy(suppressed))
            playsound(this, "sound/machines/eject.ogg", 25, TRUE, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        else
            playsound(this, "sound/machines/eject.ogg", 50, TRUE);
    }
}

// SKS semi-automatic rifle //

// tg: code/modules/projectiles/guns/ballistic/rifle.dm:558
public partial class obj_item_gun_ballistic_rifle_sks : obj_item_gun_ballistic_rifle
{
    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:580
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("The Sakhno Concern's modernized reproductions of the SKS were commissioned by the leadership of the Third Soviet Union to celebrate their establishment in the Spinward Sector.<br><br>In the wake of the Union's collapse, however, these weapons now hold a unique place in history amongst the populace of the sector as a reminder of what came before - and, possibly, the hope of what may come after. However, both parts kits and completed rifles are, strangely, rarer than the Sakhno M2442 Army.<br><br>Frontier settlers are known to own these for hunting purposes, or fighting off annoying tax collectors. Some argue that these are the same activity.")));
    }

    // tg: code/modules/projectiles/guns/ballistic/rifle.dm:593
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 38), Dm.named("offset_y", 12));
    }
}
