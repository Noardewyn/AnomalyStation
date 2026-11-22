// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spiderwebs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P3, D327, with the webbing mutation): the web, the sticky web and the geneticists' web (spiderwebs.dm:1-136); the sealed
// web wall (spiderwebs.dm:155-171, gaps E3, D327: the nurse spider's). The other webs, the passage, cocoons, spikes, the effigy and the egg
// clusters come with the spiders. The var blocks are generated (D111).

// tg: code/game/objects/effects/spiderwebs.dm:3
public partial class obj_structure_spider : obj_structure
{
    // tg: code/game/objects/effects/spiderwebs.dm:1  #define SPIDER_WEB_TINT "web_colour_tint" (the file's define)
    internal const string SPIDER_WEB_TINT = "web_colour_tint";

    // tg: code/game/objects/effects/spiderwebs.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        ADD_TRAIT(this, TRAIT_INVERTED_DEMOLITION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:16
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        if (damage_type == BURN) //the stickiness of the web mutes all attack sounds except fire damage type
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:20
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        if (Dm.eq(damage_flag, MELEE))
        {
            switch (Dm.str(damage_type))
            {
                case BURN:
                    damage_amount = Dm.num(damage_amount) * 1.25f;
                    break;
                case BRUTE:
                    damage_amount = Dm.num(damage_amount) * 0.25f;
                    break;
            }
        }
        return await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
    }

    // tg: code/game/objects/effects/spiderwebs.dm:29
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(exposed_temperature) > 350 ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:32
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(5, BURN, 0, 0);
        return null;
    }
}

// tg: code/game/objects/effects/spiderwebs.dm:35
public partial class obj_structure_spider_stickyweb : obj_structure_spider
{
    // tg: code/game/objects/effects/spiderwebs.dm:59  var/static/list/loc_connections (rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_spider_stickyweb__Initialize__loc_connections;

    // tg: code/game/objects/effects/spiderwebs.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Offset on init so that they look nice in the map editor
        if (Dm.truthy(has_frill))
        {
            pixel_x = -9;
            pixel_y = -9;
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/effects/spiderwebs.dm:65
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!HAS_TRAIT(user, TRAIT_WEB_WEAVER))
            return dot;
        loc.balloon_alert_to_viewers(I18n.T("weaving..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS)))
        {
            loc.balloon_alert(user, I18n.T("interrupted!"));
            return dot;
        }
        qdel(this);
        obj_item_stack_sheet_cloth woven_cloth = Dm.@new<obj_item_stack_sheet_cloth>();
        await user.put_in_hands(woven_cloth);
        return dot;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:79
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(@sealed))
            return FALSE;
        if (isprojectile(mover))
            return prob(projectile_stuck_chance) ? TRUE : FALSE;
        return dot;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:87
    public virtual object is_whitelisted(mob candidate = null)
    {
        return HAS_TRAIT(candidate, TRAIT_WEB_SURFER) ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:90
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable victim = null, object old_loc = null)
    {
        if (!isliving(victim))
            return;
        if (Dm.truthy(is_whitelisted(victim as mob)) || (victim.pulledby != null && Dm.truthy(is_whitelisted(victim.pulledby as mob))))
            return;
        if (prob(stuck_chance))
            Waiting.NoSleep(stuck_react(victim as mob_living));
    }

    /// Drains stamina and shows feedback when you get stuck moving thru a web
    // tg: code/game/objects/effects/spiderwebs.dm:101
    public virtual async DmTask stuck_react(mob_living victim = null)
    {
        if (QDELETED(victim))
            return;
        if (Dm.num(victim.get_stamina_loss()) > 90)
        {
            if (victim.body_position != LYING_DOWN)
                to_chat(victim, span_warning(I18n.T("You trip over \\the [] due to exhaustion!", this)));

            await victim.SetKnockdown(3 * SECONDS);
            return;
        }

        if (prob(25))
        {
            loc.balloon_alert(victim, I18n.T("stuck in web!"));
            victim.Shake(duration: 0.2f * SECONDS);
        }

        await victim.adjust_stamina_loss(rand(10, 15));
    }
}

/// Web made by geneticists, needs special handling to allow them to pass through their own webs
// tg: code/game/objects/effects/spiderwebs.dm:118
public partial class obj_structure_spider_stickyweb_genetic : obj_structure_spider_stickyweb
{
    // The earlier of the type's two Initialize definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/game/objects/effects/spiderwebs.dm:123
    [DmParams("mapload", "allowedmob")]
    public object Initialize__d0(object mapload = null, params object[] __extra) // the earlier definition of two (rules 7.5); the later one is :128
    {
        object dot = base.Initialize(mapload, __extra);
        // Tint it purple so that spiders don't get confused about why they can't cross this one
        add_filter(SPIDER_WEB_TINT, 10, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#ffaaf8ff"), Dm.kv("size", 0.1f)));
        return dot;
    }

    // tg: code/game/objects/effects/spiderwebs.dm:128
    [DmParams("mapload", "allowedmob")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object allowedmob = Dm.arg(__extra, 0);
        allowed_mob = allowedmob as mob_living;
        return Initialize__d0(mapload, __extra);
    }

    // tg: code/game/objects/effects/spiderwebs.dm:132
    public override object is_whitelisted(mob candidate = null)
    {
        return candidate == allowed_mob ? TRUE : FALSE;
    }
}

/// Web 'wall'
// tg: code/game/objects/effects/spiderwebs.dm:155
public partial class obj_structure_spider_stickyweb_sealed : obj_structure_spider_stickyweb
{
    // tg: code/game/objects/effects/spiderwebs.dm:169
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/spiderwebs.dm:59  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_structure_spider_stickyweb__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure_spider_stickyweb.on_entered)));
}
