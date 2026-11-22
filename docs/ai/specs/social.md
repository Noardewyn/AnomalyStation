# M9 Social interactions: design and plan

Written 2026-10-22 against main (0.8.3, protocol 37). tg = `../tgstation/code` (snapshot 1d99bd7f). `Tg/` = `src/Core/Tg/`. The owner's scope
is D246-D256 (ROADMAP M9); the chat UX is `docs/ai/specs/chat.md`. Worked in the main session, no agents (the owner, 2026-10-22). New choices
start at D257, deviations at DV160, not-translated sections at 70.

## 0. What already exists (verified)

- **Chat lines reach the client:** `to_chat` (`Tg/modules/tgchat/to_chat.cs`) queues `{type, html, text}` on SSchat, which the seam
  forwards to the client (presentation-seam.md 3); the client shows the last lines at the lower left (`godot/scripts/Hud/ChatLines.cs`,
  `ChatFeed`). The message `type` (tg's `MESSAGE_TYPE_*`) is what the big window's tabs filter on.
- **Speech:** only `compose_message`, trimmed, for the hallucinations' fake chat (`Tg/game/say.cs`, DV112). `Hear`, `send_speech`, `say`,
  `whisper`, `treat_message`, the say verbs, `chatmessage.dm`, the typing indicator and the spam check are not translated.
  `get_hearers_in_view` exists; `visible_message` and `audible_message` exist (their runechat and emphasis leaves are deferred).
- **Radio, telecomms, PDA:** `radio.cs`, `headset.cs`, `pda.cs` are type skeletons; no `datum_signal`, no telecomms machines. MetaStation
  has the whole tcomms room (2 receivers, 4 buses, 4 processors, 8 servers, the hub, 2 broadcasters, the message server, the announcement
  system) and about 190 intercoms. tg routes PDA messages through telecomms too (`messenger_program.dm:608`, a subspace signal to the hub).
- **Emotes:** `datum/emote` is a generated type only; about 10 `emote(...)` calls are deferred.
- **Contact:** `help_shake_act` (hug, shake, pat) exists; grabs are the passive pull only (`grippedby` is `Dm.NotTranslated`,
  `living_defense.cs`); no offering, no riding.
- **Client kit:** the tgui bridge and native windows (`Shared/Tgui/TguiWindows.cs`), the radial (`Hud/Radial.cs`, `RadialModel`), a resize
  grip (`Hud/ResizeGrip.cs`), the maptext renderer (`Hud/Maptext`), `HtmlText` for tg's spans.
- **Deferred calls tagged M9:** 336 `Dm.Deferred(..., "...M9...")` in `src/`. By topic: examine and context tips 96, tgui windows of
  machines 42, speech 39, radio and announcements 30, mood 13, preferences 12, languages 11, emotes 10, grabs and riding 6, identity 4,
  offering 3, the rest about 60. Those inside M9's scope are ported in the slices below; slice H re-tags the rest with the milestone that
  owns them (no silent cut: each lands in not-translated.md).

## 1. Slices (in order; each is a branch, reviewed, tested and merged before the next)

**A. Speech core and the compact bar.** tg `game/say.dm`, `mob/mob_say.dm`, `mob/living/living_say.dm`, the human's voice
(`GetVoice`, masks: "Unknown", D56), `treat_message`, `can_speak`, whisper (tg ranges), the "!!" shout with a longer range (D253, a DV),
`Hear` on mobs, the spam check (`client/handle_spam_prevention`), the OOC verb (`client/verbs/ooc.dm`) and pray (no admins yet: the
prayer goes to the log), the typing indicator (`say_modal/typing.dm`, its visual), runechat (`datums/chatmessage.dm`, drawn in 3D over the
speaker), `force_say`. Client: tg-say ported as it is (D250; `tgui/packages/tgui-say`): channel button, Tab cycle, history, growth, keys
T/Y/M/O/P; the fading feed kept. Languages: only the common one (others are the alpha's). Protocol bump.
*Done 2026-10-23 (protocol 38, D257, DV160-DV162, not-translated 70, `Integration/SpeechGameplayTests`); also closed: the hearers'
messages (`audible_message`, DV105 retired), machine speech, forced speech, the speech impediments, muzzles, ghost hearing.*

**B. Emotes and the emote wheel.** tg `datums/emotes.dm`, `mob/emote.dm`, `mob/living/emote.dm`, carbon and human emotes, with their
sounds, the custom `me` emote, cooldowns, and the emote calls deferred elsewhere (giggle on N2O and the rest). Client: the wheel on J
(D256) with the default eight and "More…" (chat.md 5).
*Done 2026-10-23 (protocol 39, D258, DV163, not-translated 71, `Integration/EmoteGameplayTests`); also closed: the bodypart overlays of the
emotes (blush, tears, tongue), the eyelids' blink procs, tg's emote panel as "More…". Moved to slice F: the emotes that put a hand item in
the hand (kiss, slap, circle, noogie, hand, shoesteal) with the hand items.*

**C. The big chat window.** tg's chat panel (`tgui-panel/chat`): tabs filtered by message type, the log in tg's styles, scroll lock and
"new messages", the action buttons, the input row, resizing and the saved geometry; the feed hidden while it is open (D252). Every
`to_chat` the port sends carries its tg type.
*Done 2026-10-23 (D259, not-translated 72, `Integration/ChatWindowGameplayTests`, no protocol change: tg's panel types an untyped line by
its selectors, as the port's `to_chat` already passes tg's types); also, from the owner's playtest of slice B: tgui's icons from tg's Font
Awesome (they were dots), the windows' status eye in the title bar, and the emote cooldown's balloon alert (DV164).*

**D. Radio and telecomms.** tg `game/communications.dm` (frequencies, `SSradio`), `devices/radio/radio.dm`, `headset.dm`,
`encryptionkey.dm`, `intercom.dm`, the radio prefixes and channel colours, `machinery/telecomms` (receiver, bus, processor, server, hub,
relay, broadcaster, message server: the signal's whole path, so breaking the tcomms room silences the radio as in tg), the machines'
icon states, the radio's tgui window (frequency, listen and broadcast), the announcement system and `priority_announce` (the deferred
radio announcements: delamination, the rift).
Done (D260): all of it but the message server (slice E, with the PDA), plus the remote signaler (the chem implant's signal) and the
radio, signaler, telecomms and announcement system windows; the chat window's Radio list moves to slice E (one protocol change with the
PDA). D2 (D261): the telecomms monitoring and server monitoring consoles, the station name (so the announcements that name the station
are live) and the radio's and intercom's screentips. Proved on MetaStation by `RadioGameplayTests`.

**E. The PDA.** tg `modular_computers`: the computer and PDA items (ID slot, pen slot, overlays, the screen), `file_system/program.dm`,
NtOS's main screen and the starting apps (D251): messenger (through the D slice's hub and message server), notepad, crew manifest
(`datums/records/manifest.dm`), file manager, app downloader, themes. NT Pay later (D255). The PDA ring and its messages in the log's
Messages tab; the big window's Message button opens the messenger. Job PDAs on MetaStation's outfits.
Done (D262): all of it, plus the message server, its key and the message monitor console, SSmodular_computers and the NTNet relay,
the chat window's Radio list (protocol 40) and Themeify's NtOS themes as the client's palettes (DV172); the other apps are listed but not
translated (not-translated.md 74). Proved on MetaStation by `PdaGameplayTests`.

**F. Contact.** Grabs past the passive pull (`grippedby`, aggressive, neck and kill grabs, choking, resisting a grab), the offering status
effect and the give key G (handing an item over, the hand items: high-five, secret handshake), the fireman carry and piggyback
(`datums/components/riding/riding_mob.dm`, `carbon/fireman_carry`), hugs and pats checked against tg.
Done (D263): the grabs, the offering with the give key G (protocol 41), the hand items and their emotes, hugs and pats in full and
the carbon's screentips; the fireman carry and the piggyback are not now, by the owner. Proved on MetaStation by `GrabGameplayTests` and
`OfferingGameplayTests`.

**G. Examine and identity.** Human examine in full (what is worn, injuries, the ID card read up close, "Unknown"), the deferred
`examine`, `examine_more` and context tips in M9's scope, identity in chat lines and runechat.
Done (D264): the human's examine in full with its HUD and ID links, the closer look, the identity leftovers (see_no_names, the
blind examine, the assignment and authentication names) and the masked voice "Unknown" (DV173); the objects' deferred examines and
screentips are not now, by the owner (not-translated.md 76). Proved on MetaStation by `ExamineGameplayTests`.

**H. Closing.** The M9-tagged deferred calls outside the scope re-tagged with their owner (mood, preferences, machine tgui windows,
languages, sprite accessories) and listed in not-translated.md; the scenario test; the C5 checklist; the docs; release 0.9.0.
Done (D265): the 556 sites still labelled M9 re-tagged to M10+ (not-translated.md 77), the five the sweep found in M9's scope
translated (the shove's and the disarm's lines, the storage's viewers, the ID and PDA rename, the airlock's area name); the scenario
test `M9ScenarioTests` (section 2) and `docs/ai/C5-checklist.md`.

## 2. Done when

ROADMAP M9's "Done when", proved by a gameplay scenario on a real server with three clients (Alice speaks, Bob listens, Carol is out of
range or behind a door): speech, whisper and shout ranges; an opaque closed door blocks speech and glass does not; a headset message
reaches the channel's holders station-wide and stops when the tcomms room is unpowered; a PDA message reaches another PDA; emote, hug,
high-five and hand-over show to bystanders; a masked speaker is "Unknown"; flooding is rate-limited; both chat windows and the emote
wheel work. Full Core and Server suites green, 0-warning builds.
