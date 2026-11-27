// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/randoms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4a, D316): random_nukecode, for the red pill junk mail; ion_num (silicons SI1, D326); get_random_food and
// get_random_drink (the silver slime extract's food and drinks); get_random_coin (the gold crossbreeds' coins).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/randoms.dm:30
    public static string random_nukecode()
    {
        float val = rand(0, 99999);
        string str = Dm.text("[]", val);
        while (length(str) < 5)
            str = "0" + str;
        return str;
    }
}

public static partial class GlobalProcs
{
    ///Picks a string of symbols to display as the law number for hacked or ion laws
    // tg: code/__HELPERS/randoms.dm:26
    public static string ion_num() //! is at the start to prevent us from changing say modes via get_message_mode()
    {
        return Dm.text("![][][][]", pick("!", "@", "#", "$", "%", "^", "&"), pick("!", "@", "#", "$", "%", "^", "&", "*"), pick("!", "@", "#", "$", "%", "^", "&", "*"), pick("!", "@", "#", "$", "%", "^", "&", "*"));
    }
}

public static partial class GlobalProcs
{
    ///Get a random food item exluding the blocked ones
    // tg: code/__HELPERS/randoms.dm:2
    public static object get_random_food()
    {
        // tg: code/__HELPERS/randoms.dm:3  var/static/list/allowed_food = list() (a proc-level static, rules 10.6)
        if (!Dm.truthy(LAZYLEN(get_random_food__allowed_food))) //it's static so we only ever do this once
        {
            DmList blocked = Dm.list(); // This used to be populated

            get_random_food__allowed_food = Dm.sub(get_sane_item_types(typeof(obj_item_food)), blocked) as DmList;
        }

        return pick(get_random_food__allowed_food);
    }

    ///Gets a random drink excluding the blocked type
    // tg: code/__HELPERS/randoms.dm:13
    public static object get_random_drink()
    {
        // tg: code/__HELPERS/randoms.dm:14  var/static/list/allowed_drinks = list() (a proc-level static, rules 10.6)
        if (!Dm.truthy(LAZYLEN(get_random_drink__allowed_drinks)))
        {
            DmList blocked = Dm.list(
                typeof(obj_item_reagent_containers_cup_glass_bottle)
            );

            get_random_drink__allowed_drinks = Dm.sub(Dm.add(get_sane_item_types(typeof(obj_item_reagent_containers_cup_glass)), get_sane_item_types(typeof(obj_item_reagent_containers_cup_soda_cans))), blocked) as DmList;
        }

        return pick(get_random_drink__allowed_drinks);
    }

    private static ref DmList get_random_food__allowed_food => ref TgState.Current.GlobalProcs__get_random_food__allowed_food;
    private static ref DmList get_random_drink__allowed_drinks => ref TgState.Current.GlobalProcs__get_random_drink__allowed_drinks;
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/randoms.dm:3  /proc/get_random_food var/static/list/allowed_food
    internal DmList GlobalProcs__get_random_food__allowed_food = Dm.list();
    // tg: code/__HELPERS/randoms.dm:14  /proc/get_random_drink var/static/list/allowed_drinks
    internal DmList GlobalProcs__get_random_drink__allowed_drinks = Dm.list();
}
