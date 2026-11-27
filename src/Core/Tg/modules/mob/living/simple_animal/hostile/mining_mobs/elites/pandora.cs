// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the attack actions and pandora's blast are var-only). The var blocks are generated.

/**
 * # Pandora
 *
 * A box with a similar design to the Hierophant which trades large, single attacks for more frequent smaller ones.
 * As its health gets lower, the time between its attacks decrease.
 * Its attacks are as follows:
 * - Fires hierophant blasts in a straight line.  Can only fire in a straight line in 8 directions, being the diagonals and cardinals.
 * - Creates a box of hierophant blasts around the target.  If they try to run away to avoid it, they'll very likely get hit.
 * - Teleports the pandora from one location to another, almost identical to Hierophant.
 * - Spawns a 7x7 AOE at the location of choice, spreading out from the center.
 * Pandora's fight mirrors Hierophant's closely, but has stark differences in attack effects.  Instead of long-winded dodge times and long cooldowns, Pandora constantly attacks the opponent, but leaves itself open for attack.
 */
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:19
public partial class mob_living_simple_animal_hostile_asteroid_elite_pandora : mob_living_simple_animal_hostile_asteroid_elite
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:1-4 (file-local #defines)
    private const float SINGULAR_SHOT = 1;
    private const float MAGIC_BOX = 2;
    private const float PANDORA_TELEPORT = 3;
    private const float AOE_SQUARES = 4;

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:76
    public override async DmTask OpenFire(atom A = null)
    {
        if (client != null)
        {
            if (chosen_attack == SINGULAR_SHOT)
                singular_shot(target);
            else if (chosen_attack == MAGIC_BOX)
                magic_box(target);
            else if (chosen_attack == PANDORA_TELEPORT)
                pandora_teleport(target);
            else if (chosen_attack == AOE_SQUARES)
                aoe_squares(target);
            return;
        }
        float aiattack = rand(1, 4);
        if (aiattack == SINGULAR_SHOT)
            singular_shot(target);
        else if (aiattack == MAGIC_BOX)
            magic_box(target);
        else if (aiattack == PANDORA_TELEPORT)
            pandora_teleport(target);
        else if (aiattack == AOE_SQUARES)
            aoe_squares(target);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:99
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (Dm.num(health) >= Dm.num(maxHealth) * 0.5f)
        {
            cooldown_time = 2 * SECONDS;
            return dot;
        }
        if (Dm.num(health) < Dm.num(maxHealth) * 0.5f && Dm.num(health) > Dm.num(maxHealth) * 0.25f)
        {
            cooldown_time = 1.5f * SECONDS;
            return dot;
        }
        else
            cooldown_time = 1 * SECONDS;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:110
    public virtual void singular_shot(object target = null)
    {
        ranged_cooldown = world.time + (Dm.num(cooldown_time) * 0.5f);
        object dir_to_target = get_dir(get_turf(this), get_turf(target as atom));
        turf T = get_step(get_turf(this), Dm.num(dir_to_target));
        singular_shot_line(sing_shot_length, dir_to_target, T);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:116
    public virtual void singular_shot_line(object procsleft = null, object angleused = null, turf T = null)
    {
        if (Dm.num(procsleft) <= 0)
            return;
        Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(T, this);
        T = get_step(T, Dm.num(angleused));
        procsleft = Dm.num(procsleft) - 1;
        _addtimer(Dm.@new<datum_callback>(this, nameof(singular_shot_line), procsleft, angleused, T), Dm.num(cooldown_time) * 0.1f, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm", line: 122);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:124
    public virtual void magic_box(object target = null)
    {
        ranged_cooldown = world.time + Dm.num(cooldown_time);
        turf T = get_turf(target as atom);
        foreach (object t in Dm.each(spiral_range_turfs(3, T)))
        {
            if (Dm.num(get_dist(t, T)) > 1)
                Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(t, this);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:131
    public virtual void pandora_teleport(object target = null)
    {
        turf turf_target = get_turf(target as atom);
        if (!Dm.@in(turf_target, view(12, this)))
            return;
        ranged_cooldown = world.time + (Dm.num(cooldown_time) * 2);
        turf source = get_turf(this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(turf_target, this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph>(source, this);
        playsound(source, Dm.resource("sound/machines/airlock/airlockopen.ogg"), 200, 1);
        _addtimer(Dm.@new<datum_callback>(this, nameof(pandora_teleport_2), turf_target, source), 0.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm", line: 141);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:142
    public virtual void pandora_teleport_2(turf T = null, turf source = null)
    {
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(T, this);
        Dm.@new<obj_effect_temp_visual_hierophant_telegraph_teleport>(source, this);
        foreach (object t in Dm.each(RANGE_TURFS(1, T)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(t, this);
        foreach (object t in Dm.each(RANGE_TURFS(1, source)))
            Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(t, this);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 2), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT))); //fade out
        visible_message(span_hierophant_warning(I18n.T("[] fades out!", this)));
        ADD_TRAIT(this, TRAIT_UNDENSE, VANISHING_TRAIT);
        _addtimer(Dm.@new<datum_callback>(this, nameof(pandora_teleport_3), T), 0.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm", line: 153);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:154
    public virtual async DmTask pandora_teleport_3(turf T = null)
    {
        await forceMove(T);
        animate(this, Dm.named("alpha", 255), Dm.named("time", 2), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN))); //fade IN
        REMOVE_TRAIT(this, TRAIT_UNDENSE, VANISHING_TRAIT);
        visible_message(span_hierophant_warning(I18n.T("[] fades in!", this)));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:160
    public virtual void aoe_squares(object target = null)
    {
        ranged_cooldown = world.time + Dm.num(cooldown_time);
        turf T = get_turf(target as atom);
        Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(T, this);
        float max_size = 3;
        _addtimer(Dm.@new<datum_callback>(this, nameof(aoe_squares_2), T, 0, max_size), 0.2f * SECONDS, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm", line: 166);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:167
    public virtual void aoe_squares_2(turf T = null, object ring = null, object max_size = null)
    {
        if (Dm.num(ring) > Dm.num(max_size))
            return;
        foreach (object t in Dm.each(spiral_range_turfs(ring, T)))
        {
            if (Dm.eq(get_dist(t, T), ring))
                Dm.@new<obj_effect_temp_visual_hierophant_blast_damaging_pandora>(t, this);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(aoe_squares_2), T, (Dm.num(ring) + 1), max_size), Dm.num(cooldown_time) * 0.1f, file: "code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm", line: 174);
    }
}

//Pandora's loot: Hope
// tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:181
public partial class obj_item_clothing_accessory_pandora_hope : obj_item_clothing_accessory
{
    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:188
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        await user.add_mood_event("hope_lavaland", typeof(datum_mood_event_hope_lavaland));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/mining_mobs/elites/pandora.dm:191
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        user.clear_mood_event("hope_lavaland");
        await DmTask.CompletedTask;
    }
}
