// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fish_growth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a fish that grows into another fish (an evolution) or lays or becomes something else (the tadpole's frog, the
// moonfish's eggs, the chasm chrab's lobstrosity) as it lives. The var block is generated (D111).

///A simple component that manages raising things from aquarium fish.
// tg: code/datums/components/fish_growth.dm:2
public partial class datum_component_fish_growth : datum_component
{
    // tg: code/datums/components/fish_growth.dm:17
    [DmParams("result_type", "growth_time", "use_drop_loc", "del_on_grow", "inherit_name")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object result_type = Dm.arg(__extra, 0);
        object growth_time = Dm.arg(__extra, 1);
        object use_drop_loc = Dm.arg(__extra, 2) ?? TRUE;
        object del_on_grow = Dm.arg(__extra, 3) ?? TRUE;
        object inherit_name = Dm.arg(__extra, 4) ?? TRUE;
        object dot = await base.Initialize(__extra);
        if (!isfish(parent))
            return COMPONENT_INCOMPATIBLE;
        this.result_type = result_type as Type;
        growth_rate = 100 / Dm.num(growth_time);
        this.use_drop_loc = use_drop_loc;
        this.del_on_grow = del_on_grow;
        this.inherit_name = inherit_name;
        return dot;
    }

    // tg: code/datums/components/fish_growth.dm:27
    [DmParams("new_growth", "result_type", "growth_time", "use_drop_loc", "del_on_grow", "inherit_name")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        object result_type = Dm.arg(__extra, 0);
        object growth_time = Dm.arg(__extra, 1);
        if (Dm.eq(result_type, this.result_type))
        {
            growth_rate = 100 / Dm.num(growth_time);
            return TRUE; //copy the growth rate and kill the new component
        }
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/datums/components/fish_growth.dm:40
    public override async DmTask RegisterWithParent()
    {
        bool evo_growth = ispath(result_type, typeof(datum_fish_evolution));
        RegisterSignal(parent, COMSIG_FISH_LIFE, nameof(on_fish_life));
        if (!evo_growth)
            return;
        datum_fish_evolution evolution = GLOB.fish_evolutions[result_type] as datum_fish_evolution;
        evolution.RegisterSignal(parent, COMSIG_FISH_BEFORE_GROWING, nameof(datum_fish_evolution.growth_checks));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/fish_growth.dm:48
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_FISH_LIFE, COMSIG_FISH_BEFORE_GROWING));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/fish_growth.dm:51
    [SignalHandler]
    public virtual void on_fish_life(obj_item_fish source = null, object seconds_per_tick = null)
    {
        if (source.status == FISH_DEAD) //It died just now.
            return;
        float deciseconds_elapsed = Dm.num(seconds_per_tick) * 10;
        float growth = Dm.num(growth_rate) * deciseconds_elapsed;
        if (HAS_TRAIT(source, TRAIT_FISH_QUICK_GROWTH))
            growth *= 2;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(source._listen_lookup?[COMSIG_FISH_BEFORE_GROWING]) ? NONE : source._SendSignal(COMSIG_FISH_BEFORE_GROWING, Dm.list(source, seconds_per_tick, growth, result_type)), COMPONENT_DONT_GROW)))
            return;
        maturation = Dm.num(maturation) + growth;
        if (Dm.num(maturation) >= 100)
            Waiting.NoSleep(finish_growing(source));
    }

    // tg: code/datums/components/fish_growth.dm:65
    public virtual async DmTask finish_growing(obj_item_fish source = null)
    {
        atom location = (Dm.truthy(use_drop_loc) ? source.drop_location() : source.loc) as atom;
        bool is_evo = ispath(result_type, typeof(datum_fish_evolution));
        atom_movable result = null;
        if (is_evo)
        {
            datum_fish_evolution evolution = GLOB.fish_evolutions[result_type] as datum_fish_evolution;
            result = await source.create_offspring(evolution.new_fish_type, evolution: evolution) as atom_movable;
            obj_item_fish fishie = result as obj_item_fish;
            fishie.breeding_wait = source.breeding_wait;
            fishie.last_feeding = source.last_feeding;
            fishie.update_integrity(fishie.max_integrity * Dm.num(source.get_integrity_percentage()));
        }
        else
        {
            result = Dm.@new(result_type, location) as atom_movable;
            if (location != source.loc)
            {
                result.visible_message(span_boldnotice(I18n.T("[] jumps out of []!", Dm.text("\\A []", result), source.loc)));
                playsound(result, Dm.resource("sound/effects/fish_splash.ogg"), 60);
            }
            if (isbasicmob(result))
            {
                foreach (object trait_type in Dm.each(source.fish_traits))
                {
                    datum_fish_trait trait = GLOB.fish_traits[trait_type] as datum_fish_trait;
                    await trait.apply_to_mob(result as mob_living_basic);
                }

                _addtimer(Dm.@new<datum_callback>(result, nameof(mob_living_basic.hop_on_nearby_turf)), 0.1f * SECONDS, file: "code/datums/components/fish_growth.dm", line: 87);
            }
        }

        if (is_evo || location == source.loc)
        {
            string message_verb = Dm.truthy(del_on_grow) ? I18n.T("grows into") : I18n.T("lays");
            location.visible_message(span_notice(I18n.T("[] [] [].", source, message_verb, Dm.text("\\a []", result))), vision_distance: 3);
        }

        if (Dm.truthy(inherit_name) && HAS_TRAIT(source, TRAIT_WAS_RENAMED))
        {
            if (ismob(result))
            {
                mob mob = result as mob;
                await mob.fully_replace_character_name(mob.name, source.name);
            }
            else
                await result.AddComponent(typeof(datum_component_rename), source.name, result.desc);
        }

        if (Dm.truthy(source._listen_lookup?[COMSIG_FISH_FINISH_GROWING]))
            source._SendSignal(COMSIG_FISH_FINISH_GROWING, Dm.list(source, result));

        if (Dm.truthy(del_on_grow))
            qdel(parent);
        else
            maturation = 0;
    }
}
