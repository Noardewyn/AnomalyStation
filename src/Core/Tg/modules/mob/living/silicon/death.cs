// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/death.dm:1
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/death.dm:1
    public override void spawn_dust(float? just_ash = null)
    {
        just_ash ??= FALSE;
        if (Dm.truthy(just_ash))
        {
            base.spawn_dust(just_ash);
            return;
        }

        obj_effect_decal_remains_robot robones = Dm.@new<obj_effect_decal_remains_robot>(loc);
        robones.pixel_z = -6;
        robones.pixel_w = rand(-1, 1);
    }

    // tg: code/modules/mob/living/silicon/death.dm:9
    public override async DmTask<object> set_stat(object new_stat = null)
    {
        object dot = await base.set_stat(new_stat);
        if (!Dm.eq(dot, DEAD) || hud_used == null)
            return dot;
        // Clean up hud element used for the death sequence
        await hud_used.remove_screen_object(HUD_CYBORG_DEATH);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/death.dm:16
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        diag_hud_set_status();
        diag_hud_set_health();
        update_health_hud();
        object dot = await base.death(gibbed, __extra);
        // Runs an animation on the player's HUD
        if (!Dm.truthy(gibbed) && hud_used != null)
            await death_sequence();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/death.dm:25
    public override object get_visible_suicide_message()
    {
        return I18n.T("[] is powering down. It looks like [] trying to commit suicide.", this, p_theyre());
    }

    // tg: code/modules/mob/living/silicon/death.dm:28
    public override object get_blind_suicide_message()
    {
        return I18n.T("You hear a long, hissing electronic whine.");
    }

    /// Plays an animation of the player's hud flavored about their death somewhat
    // tg: code/modules/mob/living/silicon/death.dm:32
    public virtual async DmTask death_sequence()
    {
        string cause_of_death = null;
        if (Dm.gt(Dm.add(get_brute_loss(), get_fire_loss()), 100))
            cause_of_death = I18n.T("Critical damage sustained.");
        if (Dm.gt(get_oxy_loss(), 100))
            cause_of_death = I18n.T("Critically low power.");

        atom_movable_screen_cyborg_death deathhud = await hud_used.add_screen_object(typeof(atom_movable_screen_cyborg_death), HUD_CYBORG_DEATH, HUD_GROUP_SCREEN_OVERLAYS, update_screen: TRUE) as atom_movable_screen_cyborg_death;
        deathhud.run_animation(cause_of_death);
    }
}

// tg: code/modules/mob/living/silicon/death.dm:43-46  the file-local #defines (#undef at :119-122)
public partial class atom_movable_screen_cyborg_death : atom_movable_screen
{
    private static string WARNING_ROBOT(string text) => ("<font color='yellow'>" + text + "</font>");
    private static string DANGER_ROBOT(string text) => ("<font color='red'>" + text + "</font>");
    private static string OKAY_ROBOT(string text) => ("<font color='green'>" + text + "</font>");
    private static string SENTIENT_ROBOT(string text) => ("<font color='cyan'>" + text + "</font>");

    // tg: code/modules/mob/living/silicon/death.dm:85
    public virtual void run_animation(string cause_of_death = null)
    {
        Waiting.WaitforFalse(run_animation__body(cause_of_death));
    }

    private async DmTask run_animation__body(string cause_of_death)
    {
        cause_of_death ??= I18n.T("Unidentified kernel error.");

        messages.Insert(1, WARNING_ROBOT(I18n.T("WARNING: []", cause_of_death)));
        if (prob(1))
            messages[Dm.length(messages)] = SENTIENT_ROBOT(pick("I don't want to go.", "I don't feel good.", "I don't want to die.") as string);

        mob cyborg = hud.mymob;
        atom_movable_screen staticy = cyborg.overlay_fullscreen(type, typeof(atom_movable_screen_fullscreen_static_vision_cyborg)) as atom_movable_screen;
        animate(staticy, Dm.named("alpha", 200), Dm.named("time", Dm.length(messages) * time_per_message * 0.75f));

        foreach (object msg in Dm.each(messages))
        {
            maptext = Dm.str(maptext) + MAPTEXT_PIXELLARI(msg) + "<br>";
            maptext_y -= 14;
            await sleep(time_per_message);
            if (QDELETED(this))
            {
                if (!QDELETED(cyborg))
                    cyborg.clear_fullscreen(type);
                return;
            }
        }

        await sleep(0.5f * SECONDS);
        if (QDELETED(this))
        {
            if (!QDELETED(cyborg))
                cyborg.clear_fullscreen(type, 1.5f * SECONDS);
            return;
        }

        invisibility = INVISIBILITY_ABSTRACT;
        cyborg.overlay_fullscreen(type, typeof(atom_movable_screen_fullscreen_blind_cyborg));
        await sleep(1.5f * SECONDS);
        if (!QDELETED(cyborg))
            cyborg.clear_fullscreen(type);
    }
}
