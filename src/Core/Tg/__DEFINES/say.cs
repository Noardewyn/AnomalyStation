// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice E: the ghosts' PDA message lines): FOLLOW_LINK; M10x slice B (dead chat): TURF_LINK and FOLLOW_OR_TURF_LINK; the
// constants of say.dm are generated into Defines.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/say.dm:121  #define FOLLOW_LINK(alice, bob) "<a href=byond://?src=[REF(alice)];follow=[REF(bob)]>(F)</a>"
    public static string FOLLOW_LINK(object alice, object bob) => Dm.text("<a href=byond://?src=[];follow=[]>(F)</a>", REF(alice), REF(bob));

    // tg: code/__DEFINES/say.dm:122  #define TURF_LINK(alice, turfy) "<a href=byond://?src=[REF(alice)];x=[turfy.x];y=[turfy.y];z=[turfy.z]>(T)</a>"
    public static string TURF_LINK(object alice, turf turfy) => Dm.text("<a href=byond://?src=[];x=[];y=[];z=[]>(T)</a>", REF(alice), turfy.x, turfy.y, turfy.z);

    // tg: code/__DEFINES/say.dm:123  #define FOLLOW_OR_TURF_LINK(alice, bob, turfy) "<a href=byond://?src=[REF(alice)];follow=[REF(bob)];x=[turfy.x];y=[turfy.y];z=[turfy.z]>(F)</a>"
    public static string FOLLOW_OR_TURF_LINK(object alice, object bob, turf turfy) => Dm.text("<a href=byond://?src=[];follow=[];x=[];y=[];z=[]>(F)</a>", REF(alice), REF(bob), turfy.x, turfy.y, turfy.z);
}
