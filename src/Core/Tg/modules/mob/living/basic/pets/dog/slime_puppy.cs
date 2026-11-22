// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The slime corgi puppy only (gaps package C2): Meta maps one in xenobiology.

//Slime corgi - Chilling Pink
// tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:41
public partial class mob_living_basic_pet_dog_corgi_puppy_slime : mob_living_basic_pet_dog_corgi_puppy
{
    // tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:51
    public override void update_dog_speech(DmList speech_data = null)
    {
        speech_data[BB_EMOTE_SAY] = string_list(Dm.list());
        speech_data[BB_EMOTE_HEAR] = string_list(Dm.list("bubbles!", "splorts.", "splops!"));
        speech_data[BB_EMOTE_SEE] = string_list(Dm.list("gets goop everywhere.", "flops.", "jiggles!"));
    }
}
