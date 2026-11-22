// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/hallucination_icons.dm
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

// M8 slice G (D187): the whole file.
// ADAPTED: icon_exists reads the icon states from the tg clone's .dmi files with the icon converter's reader, as limbsanity does (D104);
// without the clone the test has nothing to read and passes with a notice.

/**
 * Unit tests various image related hallucinations
 * that their icon_states and icons still exist,
 * as often hallucinations are copy and pasted
 * implementations of existing image setups
 * that may be changed and not updated.
 */
// tg: code/modules/unit_tests/hallucination_icons.dm:8 (/datum/unit_test/hallucination_icons; class named by rules 2.3)
public sealed class datum_unit_test_hallucination_icons : TgUnitTest
{
    private static readonly Dictionary<string, HashSet<string>> StatesCache = new(StringComparer.Ordinal);

    // tg: code/modules/unit_tests/hallucination_icons.dm:10
    public override async DmTask Run()
    {
        if (TgClone.Root == null)
        {
            TEST_NOTICE(this, "no tg clone: the hallucination icon states are not checked");
            return;
        }

        // Test nearby_fake_item hallucinations for invalid image setups
        foreach (Type hallucination in Dm.each_cast<Type>(subtypesof(typeof(datum_hallucination_nearby_fake_item))))
        {
            object left_icon = Dm.initial(hallucination, (datum_hallucination_nearby_fake_item p) => p.left_hand_file);
            object right_icon = Dm.initial(hallucination, (datum_hallucination_nearby_fake_item p) => p.right_hand_file);
            object icon_state = Dm.initial(hallucination, (datum_hallucination_nearby_fake_item p) => p.image_icon_state);
            check_hallucination_icon(hallucination, left_icon, icon_state);
            check_hallucination_icon(hallucination, right_icon, icon_state);
        }

        // Test preset delusion hallucinations for invalid image setups
        foreach (Type hallucination in Dm.each_cast<Type>(subtypesof(typeof(datum_hallucination_delusion_preset))))
        {
            if (Dm.truthy(Dm.initial(hallucination, (datum_hallucination_delusion p) => p.dynamic_delusion)))
                continue;
            object icon = Dm.initial(hallucination, (datum_hallucination_delusion p) => p.delusion_icon_file);
            object icon_state = Dm.initial(hallucination, (datum_hallucination_delusion p) => p.delusion_icon_state);
            check_hallucination_icon(hallucination, icon, icon_state);
        }

        // Test fake body hallucinations
        foreach (Type hallucination in Dm.each_cast<Type>(subtypesof(typeof(datum_hallucination_body_husk))))
        {
            object icon = Dm.initial(hallucination, (datum_hallucination_body p) => p.body_image_file);
            object icon_state = Dm.initial(hallucination, (datum_hallucination_body p) => p.body_image_state);
            check_hallucination_icon(hallucination, icon, icon_state);
        }

        // Test on_fire hallucination for if the fire icon state exists
        Type fire_hallucination = typeof(datum_hallucination_fire);
        check_hallucination_icon(fire_hallucination, Dm.initial(fire_hallucination, (datum_hallucination_fire p) => p.fire_icon), Dm.initial(fire_hallucination, (datum_hallucination_fire p) => p.fire_icon_state));

        // Test shock hallucination for if the shock icon state exists
        Type shock_hallucination = typeof(datum_hallucination_shock);
        check_hallucination_icon(shock_hallucination, Dm.initial(shock_hallucination, (datum_hallucination_shock p) => p.electrocution_icon), Dm.initial(shock_hallucination, (datum_hallucination_shock p) => p.electrocution_icon_state));

        // Test fake_flood hallucination for if its fake plasmaflood icon exists
        Type flood_hallucination = typeof(datum_hallucination_fake_flood);
        check_hallucination_icon(flood_hallucination, Dm.initial(flood_hallucination, (datum_hallucination_fake_flood p) => p.image_icon), Dm.initial(flood_hallucination, (datum_hallucination_fake_flood p) => p.image_state));

        // Test hallucination client_image_holders that are used for various hallucinations (bubblegum, xeno attack)
        foreach (Type image_holder in Dm.each_cast<Type>(subtypesof(typeof(obj_effect_client_image_holder_hallucination))))
        {
            object icon = Dm.initial(image_holder, (obj_effect_client_image_holder p) => p.image_icon);
            object icon_state = Dm.initial(image_holder, (obj_effect_client_image_holder p) => p.image_state);
            if (!Dm.truthy(icon_state) || !Dm.truthy(icon))
            {
                // Not having an icon_state or icon set by default is okay, for these.
                continue;
            }

            if (icon_exists_adapted(icon, Dm.str(icon_state)))
                continue;

            TEST_FAIL(Dm.text("Hallucination image holder [] had an invalid / missing icon state for the icon [].", image_holder, icon));
        }

        // Test ice hallucination for if the ice cube icon state exists
        Type ice_hallucination = typeof(datum_hallucination_ice);
        check_hallucination_icon(ice_hallucination, Dm.initial(ice_hallucination, (datum_hallucination_ice p) => p.ice_icon), Dm.initial(ice_hallucination, (datum_hallucination_ice p) => p.ice_icon_state));

        // Test malf_apc hallucination for if the hacked apc icon state exists
        Type malf_apc_hallucination = typeof(datum_hallucination_malf_apc);
        check_hallucination_icon(malf_apc_hallucination, Dm.initial(malf_apc_hallucination, (datum_hallucination_malf_apc p) => p.apc_icon), Dm.initial(malf_apc_hallucination, (datum_hallucination_malf_apc p) => p.apc_icon_state));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/unit_tests/hallucination_icons.dm:75
    private void check_hallucination_icon(object hallucination, object icon, object icon_state)
    {
        if (!Dm.truthy(icon))
            TEST_FAIL(Dm.text("Hallucination [] forgot to set its icon file.", hallucination));
        if (!Dm.truthy(icon_state))
            TEST_FAIL(Dm.text("Hallucination [] forgot to set an icon state.", hallucination));
        if (!Dm.truthy(icon) || !Dm.truthy(icon_state) || icon_exists_adapted(icon, Dm.str(icon_state)))
            return;
        TEST_FAIL(Dm.text("Hallucination [] has an invalid icon_state ([]) for its icon ([]).", hallucination, icon_state, icon));
    }

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
