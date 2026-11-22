// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/guillotine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The guillotine: a human strapped into its stocks (the body drawn as only the head, sixteen pixels
// down, a layer up, the death's door mood), the lever pulled in combat mode for three seconds drops the blade in its animation: a
// blade sharp enough (7 of 10, or a head beaten to 100 brute) takes the head off with tg's sound, bloodies the frame by the kill
// count and makes the watching humans clap; a dull one does 15 brute a sharpness point and a scream; each drop dulls it, a whetstone
// sharpens it a point, plasteel repairs three; raised again by hand; wrenched down facing south. The var block is generated (D111).

// tg: code/game/objects/structures/guillotine.dm:31
public partial class obj_structure_guillotine : obj_structure
{
    /// The guillotine is not being interacted with at the moment
    // tg: code/game/objects/structures/guillotine.dm:2
    private const float GUILLOTINE_ACTION_IDLE = 0;
    /// The blade is ready to be dropped
    // tg: code/game/objects/structures/guillotine.dm:4
    private const float GUILLOTINE_BLADE_RAISED = 1;
    /// The blade is moving
    // tg: code/game/objects/structures/guillotine.dm:6
    private const float GUILLOTINE_BLADE_MOVING = 2;
    /// The blade has landed in the stocks
    // tg: code/game/objects/structures/guillotine.dm:8
    private const float GUILLOTINE_BLADE_DROPPED = 3;
    /// The blade is being sharpened
    // tg: code/game/objects/structures/guillotine.dm:10
    private const float GUILLOTINE_BLADE_SHARPENING = 4;
    /// The guillotine blade is being interacted with by the executor
    // tg: code/game/objects/structures/guillotine.dm:12
    private const float GUILLOTINE_ACTION_INUSE = 5;
    /// The guillotine is being unfastened
    // tg: code/game/objects/structures/guillotine.dm:14
    private const float GUILLOTINE_ACTION_WRENCH = 6;
    /// This is maxiumum sharpness and will decapitate without failure
    // tg: code/game/objects/structures/guillotine.dm:17
    private const float GUILLOTINE_BLADE_MAX_SHARP = 10;
    /// Minimum amount of sharpness for decapitation. Any less and it will just do severe brute damage
    // tg: code/game/objects/structures/guillotine.dm:19
    private const float GUILLOTINE_DECAP_MIN_SHARP = 7;
    /// How long the guillotine animation lasts
    // tg: code/game/objects/structures/guillotine.dm:21
    private const float GUILLOTINE_ANIMATION_LENGTH = 0.9f * SECONDS;
    /// How much we need to move the player to center their head
    // tg: code/game/objects/structures/guillotine.dm:23
    private const float GUILLOTINE_HEAD_OFFSET = 16;
    /// How much to increase/decrease a head when it's buckled/unbuckled
    // tg: code/game/objects/structures/guillotine.dm:25
    private const float GUILLOTINE_LAYER_DIFF = 1.2f;
    /// Delay for executing someone
    // tg: code/game/objects/structures/guillotine.dm:27
    private const float GUILLOTINE_ACTIVATE_DELAY = 3 * SECONDS;
    /// Delay for wrenching the guillotine
    // tg: code/game/objects/structures/guillotine.dm:29
    private const float GUILLOTINE_WRENCH_DELAY = 1 * SECONDS;

    // tg: code/game/objects/structures/guillotine.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        LAZYINITLIST(ref buckled_mobs);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/structures/guillotine.dm:66
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_sheet_plasteel plasteel)
        {
            if (blade_sharpness == GUILLOTINE_BLADE_MAX_SHARP)
            {
                to_chat(user, span_warning(I18n.T("The guillotine is already fully repaired!")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You start repairing the guillotine with the plasteel...")));
            if (!Dm.truthy(await do_after(user, 100, target: user)))
            {
                to_chat(user, span_notice(I18n.T("You stop repairing the guillotine with the plasteel.")));
                return ITEM_INTERACT_BLOCKING;
            }

            blade_sharpness = Math.Min(GUILLOTINE_BLADE_MAX_SHARP, blade_sharpness + 3);
            await plasteel.use(1);
            to_chat(user, span_notice(I18n.T("You repair the guillotine with the plasteel.")));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_sharpener)
        {
            add_fingerprint(user);
            if (blade_status == GUILLOTINE_BLADE_SHARPENING)
                return ITEM_INTERACT_BLOCKING;

            if (blade_status != GUILLOTINE_BLADE_RAISED)
            {
                to_chat(user, span_warning(I18n.T("You need to raise the blade in order to sharpen it!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (blade_sharpness == GUILLOTINE_BLADE_MAX_SHARP)
            {
                to_chat(user, span_warning(I18n.T("The blade is sharp enough!")));
                return ITEM_INTERACT_BLOCKING;
            }

            blade_status = GUILLOTINE_BLADE_SHARPENING;
            if (!Dm.truthy(await do_after(user, 0.7f * SECONDS, target: this)))
            {
                blade_status = GUILLOTINE_BLADE_RAISED;
                return ITEM_INTERACT_BLOCKING;
            }

            blade_status = GUILLOTINE_BLADE_RAISED;
            user.visible_message(span_notice(I18n.T("[] sharpens the large blade of the guillotine.", user)),
                span_notice(I18n.T("You sharpen the large blade of the guillotine.")));
            blade_sharpness += 1;
            playsound(this, Dm.resource("sound/items/unsheath.ogg"), 100, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/guillotine.dm:109
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();

        string msg = I18n.T("It is []", Dm.truthy(anchored) ? I18n.T("wrenched to the floor.") : I18n.T("unsecured. A wrench should fix that."));

        if (blade_status == GUILLOTINE_BLADE_RAISED)
        {
            msg += I18n.T(" The blade is raised, ready to fall, and");

            if (blade_sharpness >= GUILLOTINE_DECAP_MIN_SHARP)
                msg += I18n.T(" looks sharp enough to decapitate without any resistance.");
            else
                msg += I18n.T(" doesn't look particularly sharp. Perhaps a whetstone can be used to sharpen it.");
        }
        else
            msg += I18n.T("The blade is hidden inside the stocks.");

        Dm.append(dot, span_notice(msg));

        if (LAZYLEN(buckled_mobs) > 0)
            Dm.append(dot, span_notice(I18n.T("Someone appears to be strapped in. You can help them out, or you can harm them by activating the guillotine.")));
        return dot;
    }

    // tg: code/game/objects/structures/guillotine.dm:129
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);

        // Currently being used by something
        if (Dm.truthy(current_action))
            return null;

        if (blade_status == GUILLOTINE_BLADE_MOVING)
            return null;
        if (blade_status == GUILLOTINE_BLADE_DROPPED)
        {
            blade_status = GUILLOTINE_BLADE_MOVING;
            icon_state = "guillotine_raise";
            _addtimer(Dm.@new<datum_callback>(this, nameof(raise_blade)), GUILLOTINE_ANIMATION_LENGTH, file: "code/game/objects/structures/guillotine.dm", line: 142);
            return null;
        }
        if (blade_status == GUILLOTINE_BLADE_RAISED)
        {
            if (LAZYLEN(buckled_mobs) > 0)
            {
                if (Dm.truthy((user as mob_living)?.combat_mode))
                {
                    user.visible_message(span_warning(I18n.T("[] begins to pull the lever!", user)),
                        span_warning(I18n.T("You begin to the pull the lever.")));
                    current_action = GUILLOTINE_ACTION_INUSE;

                    if (Dm.truthy(await do_after(user, GUILLOTINE_ACTIVATE_DELAY, target: this)) && blade_status == GUILLOTINE_BLADE_RAISED)
                    {
                        current_action = GUILLOTINE_ACTION_IDLE;
                        blade_status = GUILLOTINE_BLADE_MOVING;
                        icon_state = "guillotine_drop";
                        _addtimer(Dm.@new<datum_callback>(this, nameof(drop_blade), user), GUILLOTINE_ANIMATION_LENGTH - 2, file: "code/game/objects/structures/guillotine.dm", line: 155); // Minus two so we play the sound and decap faster
                    }
                    else
                        current_action = GUILLOTINE_ACTION_IDLE;
                }
                else
                {
                    mob_living_carbon_human victim = buckled_mobs[1] as mob_living_carbon_human;

                    if (victim != null)
                        await victim.regenerate_icons();

                    unbuckle_all_mobs();
                }
            }
            else
            {
                blade_status = GUILLOTINE_BLADE_MOVING;
                icon_state = "guillotine_drop";
                _addtimer(Dm.@new<datum_callback>(this, nameof(drop_blade), user), GUILLOTINE_ANIMATION_LENGTH, file: "code/game/objects/structures/guillotine.dm", line: 168);
            }
        }
        return null;
    }

    /// Sets the guillotine blade in a raised position
    // tg: code/game/objects/structures/guillotine.dm:171
    public virtual void raise_blade()
    {
        blade_status = GUILLOTINE_BLADE_RAISED;
        icon_state = "guillotine_raised";
    }

    /// Drops the guillotine blade, potentially beheading or harming the buckled mob
    // tg: code/game/objects/structures/guillotine.dm:176
    public virtual async DmTask drop_blade(mob user = null)
    {
        if (Dm.truthy(has_buckled_mobs()) && Dm.truthy(blade_sharpness))
        {
            mob_living_carbon_human victim = buckled_mobs[1] as mob_living_carbon_human;

            if (victim == null)
                return;

            obj_item_bodypart_head head = victim.get_bodypart("head") as obj_item_bodypart_head;

            playsound(this, drop_sound, 100, TRUE);
            if (head != null)
            {
                if (blade_sharpness >= GUILLOTINE_DECAP_MIN_SHARP || head.brute_dam >= 100)
                {
                    await head.dismember();
                    log_combat(user, victim, "beheaded", this);
                    await victim.regenerate_icons();
                    unbuckle_all_mobs();
                    kill_count += 1;

                    string blood_overlay = "bloody";

                    if (kill_count == 2)
                        blood_overlay = "bloodier";
                    else if (kill_count > 2)
                        blood_overlay = "bloodiest";

                    blood_overlay = "guillotine_" + blood_overlay + "_overlay";
                    cut_overlays();
                    add_overlay(mutable_appearance(icon, blood_overlay));

                    // The crowd is pleased
                    // The delay is to make large crowds have a longer lasting applause
                    float delay_offset = 0;
                    foreach (mob_living_carbon_human spectator in Dm.each<mob_living_carbon_human>(viewers(7, this)))
                    {
                        _addtimer(Dm.@new<datum_callback>(spectator, nameof(mob.emote), "clap"), delay_offset * 0.3f, file: "code/game/objects/structures/guillotine.dm", line: 209);
                        delay_offset++;
                    }
                }
                else
                {
                    await victim.apply_damage(15 * blade_sharpness, BRUTE, head, attacking_item: this);
                    log_combat(user, victim, "dropped the blade on", this, " non-fatally");
                    await victim.emote("scream");
                }

                if (blade_sharpness > 1)
                    blade_sharpness -= 1;
            }
        }

        blade_status = GUILLOTINE_BLADE_DROPPED;
        icon_state = "guillotine";
    }

    // tg: code/game/objects/structures/guillotine.dm:222
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        if (!Dm.truthy(anchored))
        {
            to_chat(usr, span_warning(I18n.T("[] needs to be wrenched to the floor!", this)));
            return FALSE;
        }

        if (!ishuman(M))
        {
            to_chat(usr, span_warning(I18n.T("It doesn't look like [] can fit into this properly!", M.p_they())));
            return FALSE; // Can't decapitate non-humans
        }

        if (blade_status != GUILLOTINE_BLADE_RAISED)
        {
            to_chat(usr, span_warning(I18n.T("You need to raise the blade before buckling someone in!")));
            return FALSE;
        }

        return await base.user_buckle_mob(M, user, check_loc: FALSE); //check_loc = FALSE to allow moving people in from adjacent turfs
    }

    // tg: code/game/objects/structures/guillotine.dm:237
    public override async DmTask post_buckle_mob(mob_living M = null)
    {
        if (!ishuman(M))
            return;

        await M.add_mood_event("dying", typeof(datum_mood_event_deaths_door));
        mob_living_carbon_human victim = (mob_living_carbon_human)M;

        if (victim.dna != null)
        {
            if (victim.dna.species != null)
            {
                datum_species S = victim.dna.species as datum_species;

                if (S is datum_species)
                {
                    await victim.render_only_head();
                    victim.remove_overlay(BODY_ADJ_LAYER);
                    victim.pixel_y += -GUILLOTINE_HEAD_OFFSET; // Offset their body so it looks like they're in the guillotine
                    victim.layer += GUILLOTINE_LAYER_DIFF;
                }
                else
                    unbuckle_all_mobs();
            }
            else
                unbuckle_all_mobs();
        }
        else
            unbuckle_all_mobs();

        await base.post_buckle_mob(M);
    }

    // tg: code/game/objects/structures/guillotine.dm:262
    public override async DmTask post_unbuckle_mob(mob_living M = null)
    {
        await M.regenerate_icons();
        M.pixel_y -= -GUILLOTINE_HEAD_OFFSET; // Move their body back
        M.layer -= GUILLOTINE_LAYER_DIFF;
        M.clear_mood_event("dying");
        await base.post_unbuckle_mob(M);
    }

    // tg: code/game/objects/structures/guillotine.dm:269
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (LAZYLEN(buckled_mobs) > 0)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("Can't unfasten, someone's strapped in!")));
            return FAILED_UNFASTEN;
        }

        if (Dm.truthy(current_action) && current_action != GUILLOTINE_ACTION_WRENCH)
            return FAILED_UNFASTEN;

        current_action = GUILLOTINE_ACTION_WRENCH;
        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/objects/structures/guillotine.dm:281
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool, time: GUILLOTINE_WRENCH_DELAY)))
        {
            await setDir(SOUTH);
            current_action = GUILLOTINE_ACTION_IDLE;
            return ITEM_INTERACT_SUCCESS;
        }
        current_action = GUILLOTINE_ACTION_IDLE;
        return FALSE;
    }
}
