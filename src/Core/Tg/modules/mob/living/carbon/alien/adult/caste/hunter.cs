// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/caste/hunter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the hunter and its pounce (a throw at the clicked atom; the leap sprite sheet alienleap.dmi). The
// var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:1
public partial class mob_living_carbon_alien_adult_hunter : mob_living_carbon_alien_adult
{
    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:43  #define MAX_ALIEN_LEAP_DIST 7 (#undef at :99)
    private const int MAX_ALIEN_LEAP_DIST = 7;

    //Hunter verbs

    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:26
    public virtual async DmTask toggle_leap(float? message = null)
    {
        message ??= 1;
        leap_on_click = Dm.num(!Dm.truthy(leap_on_click));
        atom_movable_screen leap_icon = hud_used?.screen_objects[HUD_ALIEN_HUNTER_LEAP] as atom_movable_screen;
        if (leap_icon != null)
            leap_icon.icon_state = Dm.text("leap_[]", Dm.truthy(leap_on_click) ? "on" : "off");
        await update_icons();
        if (Dm.truthy(message))
            to_chat(this, span_noticealien(I18n.T("You will now [] enemies!", Dm.truthy(leap_on_click) ? I18n.T("leap at") : I18n.T("slash at"))));
        else
            return;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:36
    public override async DmTask ClickOn(atom A = null, object @params = null)
    {
        face_atom(A);
        if (Dm.truthy(leap_on_click))
            await leap_at(A);
        else
            await base.ClickOn(A, @params);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:45
    public virtual async DmTask leap_at(atom A = null)
    {
        if (body_position == LYING_DOWN || HAS_TRAIT(this, TRAIT_IMMOBILIZED) || Dm.truthy(leaping))
            return;

        if (pounce_cooldown > world.time)
        {
            to_chat(this, span_alertalien(I18n.T("You are too fatigued to pounce right now!")));
            return;
        }

        if (!Dm.truthy(has_gravity()) || !Dm.truthy(A.has_gravity()))
        {
            to_chat(this, span_alertalien(I18n.T("It is unsafe to leap without gravity!")));
            //It's also extremely buggy visually, so it's balance+bugfix
            return;
        }

        else //Maybe uses plasma in the future, although that wouldn't make any sense...
        {
            leaping = TRUE;
            //Because the leaping sprite is bigger than the normal one
            add_offsets(LEAPING_TRAIT, x_add: -8, animate: FALSE);
            await update_icons();
            ADD_TRAIT(this, TRAIT_MOVE_FLOATING, LEAPING_TRAIT); //Throwing itself doesn't protect mobs against lava (because gulag).
            await throw_at(A, MAX_ALIEN_LEAP_DIST, pounce_speed, this, FALSE, TRUE, callback: Dm.@new<datum_callback>(this, nameof(leap_end)));
        }
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:66
    public virtual async DmTask leap_end()
    {
        leaping = FALSE;
        remove_offsets(LEAPING_TRAIT, animate: FALSE);
        REMOVE_TRAIT(this, TRAIT_MOVE_FLOATING, LEAPING_TRAIT);
        await update_icons();
    }

    // tg: code/modules/mob/living/carbon/alien/adult/caste/hunter.dm:72  (set waitfor = FALSE, inherited from /atom/movable/throw_impact:
    // the wrapper of waiting-model.md 5.4; the pounce sleeps)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, __extra, dot), dot);
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, object[] __extra, DotBox<object> dot)
    {
        if (!Dm.truthy(leaping))
            return base.throw_impact(hit_atom, throwingdatum, __extra);

        pounce_cooldown = world.time + pounce_cooldown_time;
        if (hit_atom != null)
        {
            if (isliving(hit_atom))
            {
                mob_living L = hit_atom as mob_living;
                float blocked = FALSE;
                if (ishuman(hit_atom))
                {
                    mob_living_carbon_human H = hit_atom as mob_living_carbon_human;
                    if (Dm.truthy(await H.check_block(this, 0, I18n.T("\\the []", this), attack_type: LEAP_ATTACK)))
                        blocked = TRUE;
                }
                if (!Dm.truthy(blocked))
                {
                    L.visible_message(span_danger(I18n.T("[] pounces on []!", this, L)), span_userdanger(I18n.T("[] pounces on you!", this)));
                    await L.Paralyze(5 * SECONDS);
                    await sleep(0.2f * SECONDS);//Runtime prevention (infinite bump() calls on hulks)
                    await step_towards(this, L);
                }
                else
                    await Paralyze(40, ignore_canstun: TRUE);

                await toggle_leap(0);
            }
            else if (Dm.truthy(hit_atom.density) && !Dm.truthy(await hit_atom.CanPass(this, get_dir(hit_atom, this))))
            {
                visible_message(span_danger(I18n.T("[] smashes into []!", this, hit_atom)), span_alertalien(I18n.T("[] smashes into []!", this, hit_atom)));
                await Paralyze(40, ignore_canstun: TRUE);
            }
        }
        return dot.Value;
    }
}
