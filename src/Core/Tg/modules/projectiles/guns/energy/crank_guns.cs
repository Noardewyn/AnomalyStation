// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/crank_guns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the hand-cranked guns (datums/components/crank_recharge.dm): the laser musket and its heroic
// prime (the "charged" in-hand look at a full cell, the bayonet mount, the lore), the smoothbore disabler and its elite prime (the
// seclight mount), the thermal pistols, inferno and cryo, spun to charge (EMP-proof, the bayonet and seclight mounts, the lore),
// and the iconoclast's repeater (a two-round burst on full auto). The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:1
public partial class obj_item_gun_energy_laser_musket : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:14
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 22), Dm.named("offset_y", 11));
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_crank_recharge),
            Dm.named("charging_cell", get_cell()),
            Dm.named("charge_amount", STANDARD_CELL_CHARGE * 0.5f),
            Dm.named("cooldown_time", 2 * SECONDS),
            Dm.named("charge_sound", Dm.resource("sound/items/weapons/laser_crank.ogg")),
            Dm.named("charge_sound_cooldown_time", 1.8f * SECONDS),
            Dm.named("charge_move", IGNORE_USER_LOC_CHANGE)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("[][]", Dm.initial(this, (obj_item p) => p.inhand_icon_state), Dm.eq(get_charge_ratio(), 4) ? "charged" : "");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:48
public partial class obj_item_gun_energy_disabler_smoothbore : obj_item_gun_energy_disabler
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_crank_recharge),
            Dm.named("charging_cell", get_cell()),
            Dm.named("charge_amount", STANDARD_CELL_CHARGE),
            Dm.named("cooldown_time", 2 * SECONDS),
            Dm.named("charge_sound", Dm.resource("sound/items/weapons/laser_crank.ogg")),
            Dm.named("charge_sound_cooldown_time", 1.8f * SECONDS),
            Dm.named("charge_move", IGNORE_USER_LOC_CHANGE)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:77
    public override async DmTask add_seclight_point()
    {
        await AddComponent(typeof(datum_component_seclite_attachable),
            Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")),
            Dm.named("light_overlay", "flight"),
            Dm.named("overlay_x", 18),
            Dm.named("overlay_y", 12)); //i swear 1812 being the overlay numbers was accidental
    }
}

//Inferno and Cryo Pistols

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:105
public partial class obj_item_gun_energy_laser_thermal : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:117
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 19), Dm.named("offset_y", 13));
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF | EMP_PROTECT_CONTENTS));
        Waiting.NoSleep(AddComponent(typeof(datum_component_crank_recharge),
            Dm.named("charging_cell", get_cell()),
            Dm.named("spin_to_win", TRUE),
            // LASER_SHOTS(8, STANDARD_CELL_CHARGE) (__DEFINES/projectiles.dm:89), inline (rules 11.1)
            Dm.named("charge_amount", ((100 * STANDARD_CELL_CHARGE) - Dm.mod(100 * STANDARD_CELL_CHARGE, 8)) / (100 * 8)),
            Dm.named("cooldown_time", 0.8f * SECONDS),
            Dm.named("charge_sound", Dm.resource("sound/items/weapons/kinetic_reload.ogg")),
            Dm.named("charge_sound_cooldown_time", 0.8f * SECONDS)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:133
    public override async DmTask add_seclight_point()
    {
        await AddComponent(typeof(datum_component_seclite_attachable),
            Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")),
            Dm.named("light_overlay", "flight"),
            Dm.named("overlay_x", 15),
            Dm.named("overlay_y", 9));
    }
}

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:158
public partial class obj_item_gun_energy_laser_musket_repeater : obj_item_gun_energy_laser_musket
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:177
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_crank_recharge),
            Dm.named("charging_cell", get_cell()),
            // LASER_SHOTS(6, STANDARD_CELL_CHARGE) (__DEFINES/projectiles.dm:89), inline (rules 11.1)
            Dm.named("charge_amount", ((100 * STANDARD_CELL_CHARGE) - Dm.mod(100 * STANDARD_CELL_CHARGE, 6)) / (100 * 6)),
            Dm.named("cooldown_time", 0.4f * SECONDS),
            Dm.named("charge_sound", Dm.resource("sound/machines/clockcult/integration_cog_install.ogg")),
            Dm.named("charge_sound_cooldown_time", 3 * SECONDS)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.5f * SECONDS));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:189
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// The Deep Lore //

// Laser Musket

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:196 (interleaved types, rules 1.9)
public partial class obj_item_gun_energy_laser_musket : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:196
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("think carefully")), this))),
            Dm.named("lore", I18n.T("The first examples of laser muskets emerged somewhat recently, even though improvised firearms have had a rich history throughout human, Mothic and Tiziran history.<br><br>Laser muskets often started to emerge when conventional weaponry or ammunition started to grow scarcer, and the amount of scrap electronics and materials outpaced the speed at which engineering crews could repair structural defenses and infrastructure. They're regarded as weapons of extreme desperation, but also of unmatched grit in the face of a hopeless situation. <br><br>Constructing one of these is sometimes seen as a rite of passage amongst militia groups and rebel forces. Each one as unique as the next. <br><br>What will this one's name be? What history will it write? Time will surely tell.")));
    }
}

// Thermal Pistols

// tg: code/modules/projectiles/guns/energy/crank_guns.dm:214 (interleaved types, rules 1.9)
public partial class obj_item_gun_energy_laser_thermal : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/crank_guns.dm:214
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("A weapon with no name, more a pattern of design. Examples of thermal pistols vary heavily based on the manufacturer or craftsperson. However, the original inventor of the thermal pistol is largely clouded in mystery. Replica versions of the pistol were circulating long before weaponized nanites were banned by the TerraGov Subdermal Weapons Accord, and have continued to exist as a means of disposing of the self-replicating stocks of weaponized nanites.<br><br>This is an example of Nanotrasen's Viper Classic model, based on earlier models of the weapon in appearance, but using up to date nanite breeding chambers that respond to user articulation. That is, the weapons can be charged with a vigorous shake. Though most users prefer to use the custom trick triggerguard to twirl them on their finger. Mostly for showmanship. <br><br>Due to various movies and television programs, or perhaps spacer legends of fancy, the weapons are often associated with the 'Space Rangers' that roam the periphery and Australicus regions of space. Polarizing tales of rugged gun fighters roaming from outpost to outpost, leaving justice, death or merely a large quantity of buzzing nanite clouds in their wake. Robber barons, frontier lawbringers. Even the occasional hard-boiled detective. These weapons are sometimes associated with cultural imagery around rugged individualism, tenacity and nonconformity, as well as a bygone age of noble warriors and ruthless villains. Using these weapons today is largely seen as a way to evoke this imagery, even if it comes across as mildly tacky. <br><br>It isn't fully understood who started the practice of 'pairing' the pistols, though the custom has remained as long as living memory. Every thermal pistol has a mate, and it is considered ill fortune to separate them purposefully.")));
    }
}
