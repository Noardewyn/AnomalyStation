// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/_ammunition.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice B: the ammo casing base, whole (the ammo box collection, collect_into_box, came with the magazines in slice C). The weapon description element is attached (D291).
// The var blocks are generated (D111).

// tg: code/modules/projectiles/ammunition/_ammunition.dm:1
public partial class obj_item_ammo_casing : obj_item
{
    // tg: code/modules/projectiles/ammunition/_ammunition.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (projectile_type != null)
            loaded_projectile = Dm.@new(projectile_type, this) as obj_projectile;
        pixel_x = base_pixel_x + rand(-10, 10);
        pixel_y = base_pixel_y + rand(-10, 10);
        Waiting.NoSleep(setDir(pick(GLOB.alldirs)));
        update_appearance();
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:63
    public override object Destroy(float? force = null)
    {
        turf T = get_turf(this);
        if (T != null && loaded_projectile == null && Dm.truthy(is_station_level(T.z)))
            SSblackbox.record_feedback("tally", "station_mess_destroyed", 1, name);
        QDEL_NULL(ref loaded_projectile);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:70
    public override async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description), Dm.named("attached_proc", nameof(add_notes_ammo)));
    }

    /**
     *
     * Outputs type-specific weapon stats for ammunition based on the projectile loaded inside the casing.
     * Distinguishes between critting and stam-critting in separate lines
     *
     */
    // tg: code/modules/projectiles/ammunition/_ammunition.dm:79
    public virtual object add_notes_ammo()
    {
        // Try to get a projectile to derive stats from
        Type exam_proj = projectile_type;
        float initial_damage = Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.damage));
        float initial_stamina = Dm.num(Dm.initial(exam_proj, (obj_projectile p) => p.stamina));
        // projectile damage multiplier for guns with snowflaked damage multipliers
        float proj_damage_mult = 1;
        if (!ispath(exam_proj) || pellets == 0)
            return null;

        // are we in an ammo box?
        if (loc is obj_item_ammo_box)
        {
            obj_item_ammo_box our_box = loc as obj_item_ammo_box;
            // is our ammo box in a gun?
            if (isgun(our_box.loc))
            {
                obj_item_gun our_gun = our_box.loc as obj_item_gun;
                // grab the damage multiplier
                proj_damage_mult = our_gun.projectile_damage_multiplier;
            }
        }
        // if not, are we just in a gun e.g. chambered
        else if (isgun(loc))
        {
            obj_item_gun our_gun = loc as obj_item_gun;
            // grab the damage multiplier.
            proj_damage_mult = our_gun.projectile_damage_multiplier;
        }
        DmList readout = Dm.list();
        if (proj_damage_mult <= 0 || (initial_damage <= 0 && initial_stamina <= 0))
            return I18n.T("Our legal team has determined the offensive nature of these [] rounds to be esoteric.", span_warning(Dm.str(caliber)));
        // No dividing by 0
        if (initial_damage != 0)
            Dm.append(ref readout, I18n.T("Most monkeys our legal team subjected to these [] rounds succumbed to their wounds after [] at point-blank, taking [] per round.", span_warning(Dm.str(caliber)), span_warning(Dm.text("[] shot\\s", HITS_TO_CRIT((initial_damage * proj_damage_mult) * Dm.num(pellets)))), span_warning(Dm.text("[] shot\\s", pellets))));
        if (initial_stamina != 0)
            Dm.append(ref readout, I18n.T("[] collapsed from exhaustion after [] of these [] rounds.", readout.Count == 0 ? "Most monkeys" : "More fortunate monkeys", span_warning(Dm.text("[] impact\\s", HITS_TO_CRIT((initial_stamina * proj_damage_mult) * Dm.num(pellets)))), span_warning(Dm.str(caliber))));
        return readout.Join("\n"); // Sending over a single string, rather than the whole list
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:112
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_ammo_casing p) => p.icon_state), loaded_projectile != null ? "-live" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:116
    public override object update_desc(float? updates = null)
    {
        desc = Dm.text("[][]", Dm.initial(this, (obj_item_ammo_casing p) => p.desc), loaded_projectile != null ? null : " This one is spent.");
        return base.update_desc(updates);
    }

    /*
     * On accidental consumption, 'spend' the ammo, and add in some gunpowder
     */
    // tg: code/modules/projectiles/ammunition/_ammunition.dm:123
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon victim = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= TRUE;
        if (loaded_projectile != null)
        {
            loaded_projectile = null;
            update_appearance();
            if (victim.reagents != null)
                await (victim.reagents as datum_reagents).add_reagent(typeof(datum_reagent_gunpowder), 3);
            if (source_item?.reagents != null)
                await (source_item.reagents as datum_reagents).add_reagent(typeof(datum_reagent_gunpowder), (source_item.reagents as datum_reagents).total_volume * (2f / 3));
        }

        return await base.on_accidental_consumption(victim, user, source_item, discover_after);
    }

    //proc to magically refill a casing with a new projectile
    // tg: code/modules/projectiles/ammunition/_ammunition.dm:133
    public virtual void newshot() //For energy weapons, syringe gun, shotgun shells and wands (!).
    {
        if (loaded_projectile == null)
            loaded_projectile = Dm.@new(projectile_type, this, this) as obj_projectile;
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:137
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_ammo_box)))
            return NONE;
        if (!isturf(loc))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await collect_into_box(user, tool as obj_item_ammo_box)))
            return ITEM_INTERACT_BLOCKING;
        return ITEM_INTERACT_SUCCESS;
    }

    /// Collects the casing and its like on its tile into the passed box, TRUE if anything collected.
    // tg: code/modules/projectiles/ammunition/_ammunition.dm:147
    public virtual async DmTask<object> collect_into_box(mob_living user = null, obj_item_ammo_box box = null)
    {
        float boolets = 0;
        foreach (object entry in Dm.each((loc as atom).contents))
        {
            if (entry is not obj_item_ammo_casing bullet)
                continue;
            if (box.stored_ammo.Count >= box.max_ammo)
                break;
            if (bullet.loaded_projectile == null)
                continue;
            if (Dm.truthy(box.give_round(bullet, 0)))
                boolets++;
        }

        if (boolets == 0)
        {
            to_chat(user, span_warning(I18n.T("You fail to collect anything!")));
            return FALSE;
        }
        box.update_appearance();
        to_chat(user, span_notice(I18n.T("You collect [] []\\s. [] now contains [] []\\s.", boolets, box.casing_phrasing, box, box.stored_ammo.Count, box.casing_phrasing)));
        return TRUE;
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:164
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        bounce_away(FALSE, NONE);
        return base.throw_impact(hit_atom, throwingdatum, __extra);
    }

    // tg: code/modules/projectiles/ammunition/_ammunition.dm:168
    public virtual void bounce_away(float? still_warm = null, float? bounce_delay = null)
    {
        still_warm ??= FALSE;
        bounce_delay ??= 3;
        update_appearance();
        SpinAnimation(10, 1);
        turf T = get_turf(this);
        if (Dm.truthy(still_warm) && Dm.truthy(T?.bullet_sizzle))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, Dm.resource("sound/items/tools/welder.ogg"), 20, 1), bounce_delay.Value, file: "code/modules/projectiles/ammunition/_ammunition.dm", line: 171); //If the turf is made of water and the shell casing is still hot, make a sizzling sound when it's ejected.
        else if (Dm.truthy(T?.bullet_bounce_sound))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, T.bullet_bounce_sound, 20, 1), bounce_delay.Value, file: "code/modules/projectiles/ammunition/_ammunition.dm", line: 173); //Soft / non-solid turfs that shouldn't make a sound when a shell casing is ejected over them.
    }
}
