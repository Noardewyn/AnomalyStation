// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/flashbang.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The flashbang and the stingbang (gaps J4, D298); the rotfrag and rotsting grenades (admin goofs, gaps OD3). The fantasy bonuses are rows.

// tg: code/game/objects/items/grenades/flashbang.dm:2
public partial class obj_item_grenade_flashbang : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/flashbang.dm:21
    public override async DmTask arm_grenade(mob user = null, object delayoverride = null, float? msg = null, float? volume = null)
    {
        volume ??= 60;
        msg ??= TRUE;
        await base.arm_grenade(user, delayoverride, msg, volume);
        if (!Dm.truthy(active))
            return;

        float sweetspot_range = clamp(ceil(flashbang_range / sweetspot_divider), 0, flashbang_range);
        await set_light(sweetspot_range, sweetspot_range, flashbang_light);
    }

    // tg: code/game/objects/items/grenades/flashbang.dm:29
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        turf flashbang_turf = get_turf(this) as turf;
        if (flashbang_turf == null)
            return dot;

        //Check that there's enough pressure on the detonation turf for the 'bang' part of the flashbang to work.
        datum_gas_mixture environment = await flashbang_turf.return_air() as datum_gas_mixture;
        float pressure = environment != null ? Dm.num(environment.return_pressure()) : 0;
        bool soundbang = pressure >= SOUND_MINIMUM_PRESSURE;

        do_sparks(Dm.num(rand(5, 9)), FALSE, this);
        playsound(flashbang_turf, Dm.resource("sound/items/weapons/flashbang.ogg"), 100, TRUE, 8, 0.9f);
        Dm.@new<obj_effect_dummy_lighting_obj>(flashbang_turf, flashbang_range + 2, 4, COLOR_WHITE, 2);
        foreach (mob_living living_mob in Dm.each<mob_living>(get_hearers_in_view(flashbang_range, flashbang_turf)))
            await bang(get_turf(living_mob) as turf, living_mob, soundbang);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/grenades/flashbang.dm:51
    public virtual async DmTask bang(turf the_turf = null, mob_living living_mob = null, bool soundbang = true)
    {
        if (living_mob.stat == DEAD) //They're dead!
            return;
        living_mob.show_message(span_warning(I18n.T("BANG")), MSG_AUDIBLE);
        float distance = Dm.num(get_dist(get_turf(this), the_turf));
        float sweetspot_range = clamp(ceil(flashbang_range / sweetspot_divider), 0, flashbang_range);

        //Flash
        object attempt_flash = await living_mob.flash_act(affect_silicon: 1);
        if (Dm.truthy(attempt_flash) && !Dm.eq(attempt_flash, FLASH_COMPLETED))
        {
            if (distance <= sweetspot_range || issilicon(living_mob))
            {
                await living_mob.Paralyze(max(20 / max(1, distance), 5));
                await living_mob.Knockdown(max(200 / max(1, distance), 60));
            }
            else
            {
                await living_mob.adjust_dizzy_up_to(max(200 / max(1, distance), 5), 20 * SECONDS);
            }
            await living_mob.dropItemToGround(living_mob.get_active_held_item() as obj_item);
            await living_mob.dropItemToGround(living_mob.get_inactive_held_item() as obj_item);
        }

        //Bang
        if (!soundbang && distance != 0)
            return;

        if (distance == 0)
        {
            await living_mob.soundbang_act(SOUNDBANG_OVERWHELMING, 20 * SECONDS, 10, 15);
            return;
        }

        if (distance <= 1) // Adds more stun as to not prime n' pull (#45381)
        {
            await living_mob.soundbang_act(SOUNDBANG_STRONG, 3 * SECONDS, 5);
            return;
        }

        if (distance <= sweetspot_range)
        {
            await living_mob.soundbang_act(SOUNDBANG_NORMAL, max(20 * SECONDS / max(1, distance), 60), Dm.num(rand(0, 5)));
            return;
        }

        if (!Dm.truthy(await living_mob.soundbang_act(SOUNDBANG_NORMAL, 0, Dm.num(rand(0, 2)))))
            return;

        await living_mob.adjust_staggered_up_to(max(200 / max(1, distance), 5), 10 * SECONDS);
        await living_mob.dropItemToGround(living_mob.get_active_held_item() as obj_item);
        await living_mob.dropItemToGround(living_mob.get_inactive_held_item() as obj_item);
    }
}

// tg: code/game/objects/items/grenades/flashbang.dm:93
public partial class obj_item_grenade_stingbang : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/flashbang.dm:111
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (Dm.truthy(dud_flags))
        {
            active = FALSE;
            update_appearance();
            return FALSE;
        }

        if (iscarbon(loc))
        {
            mob_living_carbon user = loc as mob_living_carbon;
            obj_item_bodypart bodypart = user.get_holding_bodypart_of_item(this) as obj_item_bodypart;
            if (bodypart != null)
            {
                await forceMove(get_turf(user) as atom);
                object did_dismember = await bodypart.dismember();
                user.visible_message(Dm.text("<b>[]</b>", span_danger(I18n.T("[] goes off in []'s hand[]!", this, user, Dm.truthy(did_dismember) ? I18n.T(", blowing [] [] to bloody shreds", user.p_their(), bodypart.plaintext_zone) : ""))), span_userdanger(I18n.T("[] goes off in your hand[]!", this, Dm.truthy(did_dismember) ? I18n.T(", blowing your [] to bloody shreds", bodypart.plaintext_zone) : "")));
            }
        }

        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        turf flashbang_turf = get_turf(this) as turf;
        if (flashbang_turf == null)
            return dot;
        do_sparks(Dm.num(rand(5, 9)), FALSE, this);
        playsound(flashbang_turf, Dm.resource("sound/items/weapons/flashbang.ogg"), 50, TRUE, 8, 0.9f);
        Dm.@new<obj_effect_dummy_lighting_obj>(flashbang_turf, flashbang_range + 2, 2, COLOR_WHITE, 1);
        foreach (mob_living living_mob in Dm.each<mob_living>(get_hearers_in_view(flashbang_range, flashbang_turf)))
            await pop(get_turf(living_mob) as turf, living_mob);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/grenades/flashbang.dm:141
    public virtual async DmTask pop(turf the_turf = null, mob_living living_mob = null)
    {
        if (living_mob.stat == DEAD) //They're dead!
            return;
        living_mob.show_message(span_warning(I18n.T("POP")), MSG_AUDIBLE);
        float distance = Dm.num(get_dist(get_turf(this), the_turf));
        //Flash
        if (Dm.truthy(await living_mob.flash_act(affect_silicon: 1)))
        {
            await living_mob.Paralyze(max(10 / max(1, distance), 5));
            await living_mob.Knockdown(max(100 / max(1, distance), 60));
        }

        //Bang
        if (distance == 0)
        {
            await living_mob.Paralyze(2 * SECONDS);
            await living_mob.Knockdown(20 * SECONDS);
            await living_mob.soundbang_act(SOUNDBANG_NORMAL, 200, 10, 15);
            if (Dm.truthy(await living_mob.apply_damages(brute: 10, burn: 10)))
                to_chat(living_mob, span_userdanger(I18n.T("The blast from \\the [] bruises and burns you!", this)));
        }

        // only checking if they're on top of the tile, cause being one tile over will be its own punishment
    }
}

// Grenade that releases more shrapnel the more times you use it in hand between priming and detonation (sorta like the 9bang from MW3), for admin goofs
// tg: code/game/objects/items/grenades/flashbang.dm:162  (gaps OD3)
public partial class obj_item_grenade_primer : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/flashbang.dm:174
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(active))
        {
            user.playsound_local(user, Dm.resource("sound/misc/box_deploy.ogg"), 50, TRUE);
            rots = Dm.num(rots) + 1;
            user.changeNext_move(CLICK_CD_RAPID);
        }
        return dot;
    }

    // tg: code/game/objects/items/grenades/flashbang.dm:181
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        shrapnel_radius = round(Dm.num(rots) / Dm.num(rots_per_mag));
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        qdel(this);
        return dot;
    }
}
