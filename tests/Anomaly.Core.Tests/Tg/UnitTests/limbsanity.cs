// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/limbsanity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Anomaly.Tools.Icons;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// M6 slice 4 (dismemberment: a dropped limb shows its own picture): the whole file.
// ADAPTED: icon_exists asks the .dmi files for their icon states, which the server does not read (D104: the client has the
// converted icons, __HELPERS/icons.cs); the test reads the states from the tg clone's .dmi files with the icon converter's
// reader (tools/Anomaly.Tools.Icons), as M3's turf icon test does. Without the clone the test has nothing to read and passes
// with a notice.

// tg: code/modules/unit_tests/limbsanity.dm:1 (/datum/unit_test/limbsanity; class named by rules 2.3)
public sealed class datum_unit_test_limbsanity : TgUnitTest
{
    private static readonly Dictionary<string, HashSet<string>> StatesCache = new(StringComparer.Ordinal);

    // tg: code/modules/unit_tests/limbsanity.dm:3
    public override async DmTask Run()
    {
        if (TgClone.Root == null)
        {
            TEST_NOTICE(this, "no tg clone: the bodypart icon states are not checked");
            return;
        }
        int checked_parts = 0; // not tg's: the port's guard that the type list is not empty
        foreach (Type path in Dm.each_cast<Type>(valid_subtypesof(typeof(obj_item_bodypart)))) /// removes the abstract items.
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(path, (obj_item_bodypart p) => p.bodypart_flags), BODYPART_STUMP)))
                continue; // stumps don't need to have icons

            object icon = Dm.truthy(Dm.initial(path, (obj_item_bodypart p) => p.should_draw_greyscale)) ? Dm.initial(path, (obj_item_bodypart p) => p.icon_greyscale) : Dm.initial(path, (obj_item_bodypart p) => p.icon_static);
            object limb_id = Dm.initial(path, (obj_item_bodypart p) => p.limb_id);
            object body_zone = Dm.initial(path, (obj_item_bodypart p) => p.body_zone);
            if (Dm.truthy(Dm.initial(path, (obj_item_bodypart p) => p.is_dimorphic)))
            {
                if (!icon_exists_adapted(icon, Dm.text("[]_[]_m", limb_id, body_zone)))
                    TEST_FAIL(Dm.text("[] does not have a valid icon for male variants", path));
                if (!icon_exists_adapted(icon, Dm.text("[]_[]_f", limb_id, body_zone)))
                    TEST_FAIL(Dm.text("[] does not have a valid icon for female variants", path));
            }
            else if (!icon_exists_adapted(icon, Dm.text("[]_[]", limb_id, body_zone)))
                TEST_FAIL(Dm.text("[] does not have a valid icon", path));
            checked_parts++;
        }
        TEST_ASSERT(checked_parts > 100, Dm.text("only [] bodypart types were checked", checked_parts));
        await DmTask.CompletedTask;
    }

    // icon_exists (__HELPERS/icons.dm:1214-1220): false for a null file or state, else whether the state is in the file's icon
    // states (ADAPTED, see the header).
    private static bool icon_exists_adapted(object file, string state)
    {
        if (file is not DmResource resource || state == null)
            return false;
        HashSet<string> states;
        lock (StatesCache)
        {
            if (!StatesCache.TryGetValue(resource.Path, out states))
            {
                string dmi = Path.Combine(TgClone.Root, resource.Path);
                states = File.Exists(dmi)
                    ? new HashSet<string>(DmiReader.Read(File.ReadAllBytes(dmi)).States.Select(s => s.Name), StringComparer.Ordinal)
                    : null;
                StatesCache[resource.Path] = states;
            }
        }
        return states != null && states.Contains(state);
    }
}
