// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/laser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the laser gun, the pistol, the carbine and the practice lasers (M7x slice B), the cyborg's (silicons SI2), the Cybersun
// S-120's full auto and empty lore, the laser tag guns' empty lore (gaps CL1: the laser tag game, their tag pins and the vests' lasertag
// component); gaps GU1 (D333): the assault laser's seclight mount, the retro, old, soulful, hellfire, antique (captain's), scatter shot,
// scatter and X-ray lasers' lore, and the compact laser shotgun's pump rack after each shot. The laser cannon, the energy shotgun and the
// luxury paywall laser declare no procs. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/laser.dm:1
public partial class obj_item_gun_energy_laser : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:23
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 18), Dm.named("overlay_y", 12)));
    }

    // tg: code/modules/projectiles/guns/energy/laser.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Only regular lasguns can be slapcrafted
        if (type != typeof(obj_item_gun_energy_laser))
            return dot;
        // var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_gun_energy_laser__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

// Laser Gun
// tg: code/modules/projectiles/guns/energy/laser.dm:321 (interleaved types, rules 1.9)
public partial class obj_item_gun_energy_laser : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:321
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 5 Heat Delivery System (sometimes referred to as the HDS-5 in promotional material) is what truly put Nanotrasen head and shoulders above most weapon manufacturers in the modern era. All modern energy weaponry offered by the company have the success of the Type 5 to thank for setting the standard for energy-based weapon platforms.<br><br>Adopted as the standard infantry firearm for Nanotrasen military forces, as well as private security lethal armaments, few can deny the weapon's reliability, and at an affordable price!<br><br>However, the weapon platform still possesses many of the vulnerabilities of previous energy-based weaponry. Onboard power supplies cannot be adequately shielded from external electromagnetic pulses that might interfere with the weapon's functionality without also severely jeopardizing thermal distribution into the weapon's heatsink. The Type 4, which never saw wider adoption, remains a haunting example to Nanotrasen's weapons division as to the consequences when a HDS is unable to expel thermal buildup safely.<br><br>Certainly, the Melted Veterans of Galpha 5 advocacy group will never let them forget it.")));
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:30
public partial class obj_item_gun_energy_laser_pistol : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:40
    public override async DmTask add_seclight_point()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:67
public partial class obj_item_gun_energy_laser_practice : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:75
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// Laser Carbine
// tg: code/modules/projectiles/guns/energy/laser.dm:461 (interleaved types, rules 1.9)
public partial class obj_item_gun_energy_laser_carbine : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:461
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 5/R Rapid Heat Delivery System (sometimes referred to as the HDS-5/R in briefing manuals, and 'that piece of shit flashlight' amongst TGMC troopers) was a shaky first step into automatic directed energy weaponry. <br><br>Intended for use in special operations, particularly in the hands of orbital drop shock troopers, the Type 5/R was foreseen to be an excellent addition to Nanotrasen's arsenal of offerings to military forces across occupied space. However, field performance proved grim.<br><br>The advantages of directed energy weapons is the lightweight impacts felt on the supply chain for logistical officers and quartermasters due to the only necessary upkeep for the weapons being a consistent power supply, either established or brought to the front, and the occassional cleaning.<br><br>This, however, is not a benefit that soldiers operating behind enemy lines or during tactical deployments are capable of exploiting. As a result, operators often chafed against the limited ammunition supply compared to conventional ballistic firearms, and the weapon quickly was abandoned by most special forces units.<br><br>Instead, the weapon found favour in the hands of private security teams, who enjoyed the volume of fire it provided, while maintaining exceptional accuracy even at long ranges, along with being compact enough to allow a high degree of discretion compared to a full sized rifle. The weapon is also often utilized by rim pirates and marauders, giving the weapon something of an ill reputation.")));
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:124
public partial class obj_item_gun_energy_laser_carbine_practice : obj_item_gun_energy_laser_carbine
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:132
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// The static lists of the laser gun (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:17  var/static/list/slapcraft_recipe_list
    internal DmList obj_item_gun_energy_laser__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_laser_xraylaser), typeof(datum_crafting_recipe_laser_hellgun), typeof(datum_crafting_recipe_laser_ioncarbine));
}

// tg: code/modules/projectiles/guns/energy/laser.dm:104
public partial class obj_item_gun_energy_laser_cybersun : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:114
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.15f * SECONDS, Dm.named("allow_akimbo", FALSE)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/laser.dm:118
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:183
public partial class obj_item_gun_energy_laser_cyborg : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:189
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/laser.dm:193
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

////////Laser Tag////////////////////

// tg: code/modules/projectiles/guns/energy/laser.dm:271
public partial class obj_item_gun_energy_laser_bluetag : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:286
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:289
public partial class obj_item_gun_energy_laser_redtag : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:301
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:43
public partial class obj_item_gun_energy_laser_assault : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:60
    public override async DmTask add_seclight_point()
    {
        await AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 18), Dm.named("overlay_y", 30));
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:135
public partial class obj_item_gun_energy_laser_retro_old : obj_item_gun_energy_laser_retro
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:139
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:168
public partial class obj_item_gun_energy_laser_captain_scattershot : obj_item_gun_energy_laser_captain
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:180
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:196
public partial class obj_item_gun_energy_laser_scatter : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:202
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/laser.dm:217
public partial class obj_item_gun_energy_laser_scatter_shotty_compact : obj_item_gun_energy_laser_scatter_shotty
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:228
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        object dot = await base.shoot_live_shot(user, pointblank, pbtarget, message);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, Dm.resource("sound/items/weapons/gun/shotgun/rack.ogg"), 30, TRUE), 0.2f * SECONDS, file: "code/modules/projectiles/guns/energy/laser.dm", line: 230);
        return dot;
    }
}

// The Deep Lore //

// Retro Laser Gun
// tg: code/modules/projectiles/guns/energy/laser.dm:78
public partial class obj_item_gun_energy_laser_retro : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:341
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 1 Heat Delivery System (sometimes referred to as the HDS-1 in older weapon catalogs) was a weapon that marked the beginning of a new era of firearm development.<br><br>Invented in the think-tank laboratories of Nanotrasen's weapon development team towards the end of the 24th century, the Type 1 found itself adopted broadly by various factions and military entities vying for control over the frontier once it hit the market. One hallmark of those who stood successful in these conflicts was the adoption of the Type 1 as a standard infantry weapon. The logistics required to maintain the operational peak of the HDS-1 allowed most quartermasters to merely dump a half dozen of the weapons into the hands of bloodythirsty marines, knowing full well the weapons were rugged enough to survive most anything thrown at them, only needing a recharging station with a power supply to become operational again once they ran empty.<br><br>So many of these weapons exist today that even modern conflicts may see more usage of the HDS-1 than the updated and equally reliable HDS5 employed by Nanotrasen's modern combat forces. Nanotrasen, despite their best efforts, still have not managed to encourage potential customers to swap for the new model despite a generous exchange discount.")));
    }
}

// Soulful Laser Gun
// tg: code/modules/projectiles/guns/energy/laser.dm:85
public partial class obj_item_gun_energy_laser_soul : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:361
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 3 Heat Delivery System (sometimes referred to as the HDS-3 in the memories of security officers) is quite possibly the most common type of HDS still available on the market. Fondly regarded, with quite a few diehard fans still clinging to their Type 3s like their lives depended on it, the weapon has its own place in history as the 'gun that could do it all'.<br><br>The Type 3 line ran for several decades before attempts to replace it ever even crossed Nanotrasen's minds. When people think 'laser gun', the Type 3 is usually what comes to mind.<br><br>When Nanotrasen announced its replacement, the Type 4, skeptics were quick to pan the weapon, claiming that it lacked several notable features that users of the Type 3 had enjoyed for years. As it turns out, most of those critics would end up vindicated after word of Galpha 5 and the terrible, terrible consequences of the Type 4's volatile nature came to light. Most stuck to the Type 3 and never looked back, even when the Type 5 rolled out to considerable success in its own right.<br><br>Nanotrasen still services Type 3s, with many of the parts used in the weapon sharing compatible cousins in the Type 5. Most examples of the Type 3 today may actually be closer in function and form to the Type 5 than they were during their original construction, depending on how often it is serviced.")));
    }
}

// hellfire laser gun
// tg: code/modules/projectiles/guns/energy/laser.dm:142
public partial class obj_item_gun_energy_laser_hellgun : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:383
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 4 Heat Delivery System (sometimes referred to as the HDS-4 in legal documentation) is considered a notable example of Nanotrasen's weapons development teams flying too close to the sun.<br><br>The success of the Type 3 resulted in shareholders urging marketing to bring out the 'next best thing' in energy-based weaponry. At the time, Nanotrasen's weapons division had a prototype still in the works, with recently-learned lessons from the failure-prone Type 2 in mind after it had more than a few catastrophic failures in testing. However, there were some concerns raised amongst researchers as to the 'moral implications' that might result from unleashing 'that much directed radioactive material' towards a living being. Executives at the time brushed off such concerns, as there was money to be made and already ultrawealthy shareholders to feed the earnings.<br><br>The Type 4 was rushed onto the market within the next quarter, even before most common safety mechanisms had been properly tested and implemented. Reports immediately began flooding in of horrific accidental discharges, battlefield atrocities, and unexpected spontaneous combustion from excessive exposure to the untested experimental heat distribution systems 'taking its pound of flesh' for the 'hell it unleashed'.<br><br>News outlets and tabloids alike railed against the company for creating what was now being called the 'hellfire' laser gun. In response, most legal bodies rushed to ban the firearm from sales within their region of space, and the weapon became infamous for its unethical means of ending sentient life. Laws were passed to ensure power regulators were installed in all future energy-based weaponry sold by Nanotrasen. Nanotrasen quickly discontinued the Type 4 in response, and it never saw production from that day forth. However, retrofit kits still exist on the black market and in some of Nanotrasen's own warehouses. While, legally, it is unlawful to sell and possess a Type 4, Nanotrasen itself does not regulate possession of the firearm aboard its own stations, nor does any legal body intend on preventing them from utilizing it in defense of its own assets.")));
    }
}

// Antique Laser Gun
// tg: code/modules/projectiles/guns/energy/laser.dm:153
public partial class obj_item_gun_energy_laser_captain : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:411
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("For a brief period, Nanotrasen produced a series of custom-made Type 4 laser guns for a select group of clients, primarily composed of wealthy starship captains, politicians, and military leaders looking to demonstrate prestige before the common folk.<br><br>The Type 4 was a commercial failure, but this particular variant earned its own infamy, linked to narratives of crazed despots using it to put down political rivals and dissidents, as well as tales of mad generals marching ahead of their forces, this weapon brandished and running hot in an outstretched arm, pointed towards any moving target they could find on the battlefield.<br><br>Copies of this firearm are now prohibited within TerraGov space, and any captured are quickly decommissioned.This is largely why Nanotrasen <b>insists</b> that any examples held by ranking officers be kept under lock and key. All records of the schematics surrounding this variant of the Type 4 were seized and destroyed, and the creator behind it was detained in a maximum security TerraGov sanitorium. When they found her again, she appeared to have smeared the walls in her own blood, claiming that 'She' was coming, and that she had paid dearly for the knowledge of how to make the weapon.<br><br>Even the microfusion breeder cell housed inside the weapon is practically a lost technology, and Nanotrasen have been unable to reverse engineer the devices exact means of functionality.<br><br>The Syndicate are obviously just as interested in exactly how this weapon is capable of self-perpetuation, hence why the collective seem hell-bent on capturing them whenever possible. Maybe keep this somewhere safe. Or don't.")));
    }
}

// X-ray Laser Gun
// tg: code/modules/projectiles/guns/energy/laser.dm:250
public partial class obj_item_gun_energy_laser_xray : obj_item_gun_energy_laser
{
    // tg: code/modules/projectiles/guns/energy/laser.dm:438
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The NT Type 6 Heat Delivery System (sometimes referred to as the HDS6 in research notes) is a breakthrough in the development of man-portable directed energy weaponry.<br><br>Very little is known about the Type 6, as it is a relatively new experimental weapon only accessible to Nanotrasen security forces. Somehow, Nanotrasen has found a means to 'slip' the energy beams produced by the Type 6 through unintended targets, only impacting once it has made contact with a pre-designated target by the weapon's user. It appears to be unable to slip past organic matter reliably, which hampers its potential for eliminating friendly-fire. However, inorganic targets are left unscathed unless the weapon is directed towards firing upon the object. This makes the weapon exceptional for asset recovery, defense of entrenched positions, and assaults on defensive structures. <br><br>Nanotrasen claims that this phenomenon is achieved 'through the power of X-rays'. Most critics have highlighted that this is total nonsense. Some claim that Nanotrasen has discovered a yet-unknown state of matter that the company is exploiting for weapons development and manufacturing. The most conspiratorially minded of Nanotrasen's critics have even gone as far as to claim it is 'proof of ectoplasm as the sixth element,' perhaps even allowing the weapon to operate through supernatural means: perhaps even powered by the 'spirits of the damned'.<br><br>Whatever the truth may be, the weapon seems to function as advertized, and is even more energy efficient than the Type 5. Nanotrasen expects full commercial rollout sometime in the next quarter.")));
    }
}
