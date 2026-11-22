// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/statue/statue.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:3
public partial class mob_living_basic_statue : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_MUTE, TRAIT_UNOBSERVANT), INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_unobserved_actor), Dm.named("unobserved_flags", Dm.bitor(NO_OBSERVED_MOVEMENT, NO_OBSERVED_ATTACKS))));

        grant_actions_by_list(Dm.list(typeof(datum_action_cooldown_spell_aoe_blindness), typeof(datum_action_cooldown_spell_aoe_flicker_lights)));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_HUNT_TARGET_LIST, typecacheof(Dm.list(typeof(obj_machinery_light))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:70
    public override void med_hud_set_health()
    {
        //we're a statue we're invincible
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:73
    public override object med_hud_set_status()
    {
        return null; //we're a statue we're invincible
    }

    // Turn to dust when gibbed
    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:80
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await dust();
    }
}

// Flicker lights
// tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:86
public partial class datum_action_cooldown_spell_aoe_flicker_lights : datum_action_cooldown_spell_aoe
{
    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:95
    public override DmList get_things_to_cast_on(atom center = null)
    {
        DmList things = Dm.list();
        foreach (obj_machinery_light nearby_light in Dm.each<obj_machinery_light>(range(aoe_radius, center)))
        {
            if (!Dm.truthy(nearby_light.on))
                continue;

            Dm.append(ref things, nearby_light);
        }

        return things;
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:105
    public override async DmTask cast_on_thing_in_aoe(atom victim = null, atom caster = null)
    {
        (victim as obj_machinery_light).flicker();
    }
}

//Blind AOE
// tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:109
public partial class datum_action_cooldown_spell_aoe_blindness : datum_action_cooldown_spell_aoe
{
    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:117
    public override async DmTask<object> cast(atom cast_on = null)
    {
        cast_on.visible_message(span_danger(Dm.text("[] glares their eyes.", cast_on)));
        return await base.cast(cast_on);
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:121
    public override DmList get_things_to_cast_on(atom center = null)
    {
        DmList things = Dm.list();
        foreach (mob_living nearby_mob in Dm.each<mob_living>(range(aoe_radius, center)))
        {
            if (nearby_mob == owner || nearby_mob == center)
                continue;

            Dm.append(ref things, nearby_mob);
        }

        return things;
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:131
    public override async DmTask cast_on_thing_in_aoe(atom victim = null, atom caster = null)
    {
        await (victim as mob_living).adjust_temp_blindness(8 * SECONDS);
    }
}

// tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:142
public partial class mob_living_basic_statue_frosty : mob_living_basic_statue
{
    // tg: code/modules/mob/living/basic/space_fauna/statue/statue.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_dnainjector_geladikinesis)));
        return dot;
    }
}
