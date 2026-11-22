// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/beds_chairs/alien_nest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the alien nest (its overlay over the captive, the hundred seconds' struggle, the host's
// sustenance). The var block is generated (D111).

//Alium nests. Essentially beds with an unbuckle delay that only aliums can buckle mobs to.

// tg: code/game/objects/structures/beds_chairs/alien_nest.dm:3
public partial class obj_structure_bed_nest : obj_structure_bed
{
    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:16  var/static/mutable_appearance/nest_overlay (made on first use: TgState holds no appearance before the world)
    public static mutable_appearance nest_overlay => TgState.Current.obj_structure_bed_nest__nest_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/nonhuman-player/alien.dmi"), "nestoverlay", LYING_MOB_LAYER);

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_DANGEROUS_BUCKLE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:22
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
            return NONE;

        return base.add_context(source, context, held_item, user);
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:28
    public override void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (being_buckled == buckler)
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays down on [], wrapping []self in a thick, sticky resin.", buckler, this, buckler.p_them())),
                span_notice(I18n.T("You lay down on [], wrapping yourself in a thick, sticky resin.", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
        else
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] lays [] down on [], wrapping [] in a thick, sticky resin.", buckler, being_buckled, this, being_buckled.p_them())),
                span_notice(I18n.T("[] lays you down on [], wrapping you in a thick, sticky resin.", buckler, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:42
    public override void unbuckle_feedback(mob_living being_unbuckled = null, mob unbuckler = null)
    {
        if (being_unbuckled == unbuckler)
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] pulls []self free from the sticky nest!", unbuckler, unbuckler.p_them())),
                span_notice(I18n.T("You pull yourself free from the sticky nest!")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
        else
        {
            being_unbuckled.visible_message(
                span_notice(I18n.T("[] pulls [] free from the sticky nest!", unbuckler, being_unbuckled)),
                span_notice(I18n.T("[] pulls you free from the sticky nest!", unbuckler)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:56
    [DmParams("captive", "hero")]
    public override async DmTask<object> user_unbuckle_mob(mob_living buckled_mob = null, mob user = null)
    {
        mob_living captive = buckled_mob;
        mob_living hero = user as mob_living;
        if (!Dm.truthy(Dm.length(buckled_mobs)))
            return null;

        if (hero.get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) != null)
        {
            await unbuckle_mob(captive);
            add_fingerprint(hero);
            return null;
        }

        if (captive != hero)
        {
            captive.visible_message(span_notice(I18n.T("[] pulls [] free from the sticky nest!", hero.name, captive.name)),
                span_notice(I18n.T("[] pulls you free from the gelatinous resin.", hero.name)),
                span_hear(I18n.T("You hear squelching...")));
            await unbuckle_mob(captive);
            add_fingerprint(hero);
            return null;
        }

        captive.visible_message(span_warning(I18n.T("[] struggles to break free from the gelatinous resin!", captive.name)),
            span_notice(I18n.T("You struggle to break free from the gelatinous resin... (Stay still for about a minute and a half.)")),
            span_hear(I18n.T("You hear squelching...")));

        if (!Dm.truthy(await do_after(captive, 100 * SECONDS, target: this, cog_icon: null)))
        {
            if (captive.buckled != null)
                to_chat(captive, span_warning(I18n.T("You fail to unbuckle yourself!")));
            return null;
        }

        captive.visible_message(span_warning(I18n.T("[] breaks free from the gelatinous resin!", captive.name)),
            span_notice(I18n.T("You break free from the gelatinous resin!")),
            span_hear(I18n.T("You hear squelching...")));

        await unbuckle_mob(captive);
        add_fingerprint(hero);
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:89
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        if (!ismob(M) || (Dm.num(get_dist(this, user)) > 1) || (M.loc != this.loc) || Dm.truthy(user.incapacitated) || M.buckled != null)
            return null;

        if (M.get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) != null)
            return null;
        if (user.get_organ_by_type(typeof(obj_item_organ_alien_plasmavessel)) == null)
            return null;

        if (Dm.truthy(has_buckled_mobs()))
            unbuckle_all_mobs();

        if (Dm.truthy(await buckle_mob(M)))
        {
            M.visible_message(span_notice(I18n.T("[] secretes a thick vile goo, securing [] into []!", user.name, M.name, this)),
                span_danger(I18n.T("[] drenches you in a foul-smelling resin, trapping you in []!", user.name, this)),
                span_hear(I18n.T("You hear squelching...")));
        }
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:106
    [DmParams("M")]
    public override async DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        mob_living M = buckled_mob;
        ADD_TRAIT(M, TRAIT_HANDS_BLOCKED, type);
        M.add_offsets(type, x_add: 2);
        M.layer = BELOW_MOB_LAYER;
        add_overlay(nest_overlay);

        if (ishuman(M))
        {
            mob_living_carbon_human victim = M as mob_living_carbon_human;
            if (((victim.wear_mask != null && victim.wear_mask is obj_item_clothing_mask_facehugger) || HAS_TRAIT(victim, TRAIT_XENO_HOST)) && victim.stat != DEAD) //If they're a host or have a facehugger currently infecting them. Must be alive.
                await victim.apply_status_effect(typeof(datum_status_effect_nest_sustenance));
        }
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:117
    [DmParams("M")]
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        mob_living M = unbuckled_mob;
        REMOVE_TRAIT(M, TRAIT_HANDS_BLOCKED, type);
        M.remove_offsets(type);
        M.layer = Dm.initial(M, (atom p) => p.layer);
        cut_overlay(nest_overlay);
        M.remove_status_effect(typeof(datum_status_effect_nest_sustenance));
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:124
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        switch (damage_type)
        {
            case BRUTE:
                playsound(loc, Dm.resource("sound/effects/blob/attackblob.ogg"), 100, TRUE);
                break;
            case BURN:
                playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }

    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:131
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return await attack_hand(user, modifiers);
        else
            return await base.attack_alien(user, modifiers);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/beds_chairs/alien_nest.dm:16
    internal mutable_appearance obj_structure_bed_nest__nest_overlay;
}
