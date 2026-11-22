// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/cat/runtime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the legacy savefile (gaps package C2).

// tg: code/modules/mob/living/basic/pets/cat/runtime.dm:6
public partial class mob_living_basic_pet_cat_runtime : mob_living_basic_pet_cat
{
    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:2
    private const string RUNTIME_JSON_DATA = "data/npc_saves/Runtime.json";
    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:3
    private const int MAX_CAT_DEPLOY = 50;

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_family = Dm.@new<datum_callback>(this, nameof(Write_Memory));
        SSticker.OnRoundend(register_family);
        if (Dm.truthy(mapload))
        {
            read_memory();
            deploy_the_cats();
        }

        if (prob(5))
        {
            icon_state = "original";
            icon_living = "original";
            icon_dead = "original_dead";
            held_state = "original";
            update_appearance();
        }

        post_birth_callback = Dm.@new<datum_callback>(this, nameof(after_birth));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:43
    public virtual void after_birth(mob_living baby = null)
    {
        if (baby == null)
            return;
        LAZYADD(ref children, baby);
    }

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:48
    public virtual void read_memory()
    {
        // the legacy savefile branch (data/npc_saves/Runtime.sav, tg's old format) has no port: no such file exists here
        DmFileRef json_file = Dm.file(RUNTIME_JSON_DATA);
        if (!Dm.truthy(Dm.fexists(json_file)))
            return;
        DmList json_list = Dm.json_decode(Dm.file2text(json_file)) as DmList;
        family = json_list["family"] as DmList;
    }

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:60
    public override object Destroy(float? force = null)
    {
        LAZYREMOVE(ref SSticker.round_end_events, register_family);
        register_family = null;
        post_birth_callback = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:66
    public override object Write_Memory(object dead = null, object gibbed = null)
    {
        object dot = base.Write_Memory(dead, gibbed);
        if (!Dm.truthy(dot))
            return dot;
        DmFileRef json_file = Dm.file(RUNTIME_JSON_DATA);
        DmList file_data = Dm.list();
        if (!Dm.truthy(dead))
        {
            foreach (mob_living_basic_pet_cat_kitten kitten in Dm.each<mob_living_basic_pet_cat_kitten>(children))
            {
                if (kitten.stat == DEAD)
                    continue;
                family ??= Dm.list(); // tg's own runtime on a null family ("in" reads, the += crashes) with a first kitten: a runtime in tg
                object kitten_key = Dm.text("[]", kitten.type);
                if (Dm.@in(kitten_key, family))
                {
                    family[kitten_key] = Dm.num(family[kitten_key]) + 1;
                    continue;
                }
                family[kitten_key] = 1;
            }
        }
        file_data["family"] = family;
        Dm.fdel(json_file);
        Dm.output_to(json_file, Dm.json_encode(file_data, JSON_PRETTY_PRINT));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/cat/runtime.dm:84
    public virtual void deploy_the_cats()
    {
        cats_deployed = TRUE;
        foreach (object cat_type in Dm.each<object>(family))
        {
            if (family[cat_type] == null)
                return;
            for (int index = 1; index <= Dm.min(Dm.num(family[cat_type]), MAX_CAT_DEPLOY); index++)
                Dm.@new(Dm.text2path(cat_type as string) ?? cat_type as Type, loc);
        }
    }
}
