# C5 playtest checklist (M9 social interactions)

The owner's playtest after M9 (D246-D265). Play with two or three clients on one server (the README's "Run" section; `--client-console` for the dev commands). Each line names tg's behaviour to expect; the automated check that covers it is in brackets. Lines marked "look" have no automated check of their pixels.

## 1. Speech (slice A)

- [ ] T opens the compact bar (tg's tgui-say): Say, then Tab cycles Radio, Me, OOC; Y, M and O open it on their channel. While you type the others see tg's typing bubble over you, and your line rises over your head as runechat. [SpeechGameplayTests]
- [ ] Ranges: a friend 3 tiles off hears you, one 10 tiles off does not; "!!" (or the bar's shout) reaches 14 tiles (DV162); "#" whispers: next to you the words, farther "whispers something". [SpeechGameplayTests, M9ScenarioTests]
- [ ] Walls and doors: a closed airlock between you stops your voice, a glass one does not; in low pressure your voice carries one tile. [M9ScenarioTests]
- [ ] Hit while typing: you blurt your line out cut short with tg's "-GACK!". OOC reaches everyone. A ghost hears the living. [SpeechGameplayTests]
- [ ] Flood the bar (many lines in a few seconds): tg's spam filter drops the lines past its limit. [M9ScenarioTests]

## 2. Emotes and the emote wheel (slice B)

- [ ] J: hold, move to a segment and release, or tap and click: Wave, Nod, Laugh, Shrug, Clap, Salute, Sigh, Scream with tg's sounds; "More..." opens tg's emote panel. `*jump` and `*help` from the bar; Me for a custom emote. A second wave at once: "emote on cooldown!" (DV164). [EmoteGameplayTests, EmoteWheelTests]
- [ ] Look: the blush and other emote overlays on the body; the sounds of scream, laugh and clap by gender.

## 3. The big chat window (slice C)

- [ ] Shift+T: the log with tg's tabs (All, Local, Radio, Combat, OOC...), unread counts, the input with its channel buttons (Say, Whisper, Shout, Radio, Me, OOC) and the Message button. Closed, the recent lines fade in a feed. [ChatWindowGameplayTests, M9ScenarioTests]
- [ ] Look: the colours of tg's spans (danger, notice, radio channels), links in lines (a PDA's Reply, an examined ID) clickable; resize and move the window.

## 4. Radio and telecomms (slice D)

- [ ] Your headset: ";" for Common, ":e" with an engineering headset, the bar's Radio list shows your keys. A friend across the station hears you; the telecomms room's APCs off, nobody does. [RadioGameplayTests, M9ScenarioTests]
- [ ] An intercom on a wall: its screentips, its microphone and speaker switches; speech next to a live microphone goes out on its frequency (look: no gameplay test). The telecomms monitor and server consoles list the machines and the logged lines. Station announcements reach everyone with their sound. [RadioGameplayTests, RadioScreentipTests]

## 5. The PDA (slice E)

- [ ] Your PDA on the belt (tg's NtOS): the messenger lists the crew; send a line, the other's PDA rings and his chat shows tg's line with (Reply); Reply asks for the answer. The notepad, the manifest, the file manager, the app store and the themes open [NtosWindowsTests]. With telecomms dark: "Server is not responding." [PdaGameplayTests, M9ScenarioTests]

## 6. Grabs, the offering and the hand items (slice F)

- [ ] Ctrl-click: pull; in combat mode again for an aggressive grab and a neck grab; resist with B; throw a grabbed person. [GrabGameplayTests]
- [ ] G with an item: the other gets tg's alert and takes it ("takes X from Y" for the bystanders); `*slap` then G: a high-five; `*hand`, `*kiss`, `*circle`, `*noogie`. An empty-hand click: a hug or a head pat; in combat mode a right click shoves (into a wall or door: knocked down, the bystanders within 3 tiles read it). [OfferingGameplayTests, M9ScenarioTests]

## 7. Examine and identity (slice G)

- [ ] Shift-click a person: "This is X!" with what they wear and hold, the injuries, the ID as a link (click: the card read up close); shift-click again within a second: the closer look (age). With a medical or security HUD: tg's analysis and the records' links. [ExamineGameplayTests]
- [ ] A gas mask and no ID: "Unknown" in the examine, the speech and the emotes (DV173, the radio too); knocked out you see no names; blind, you feel around first. [ExamineGameplayTests, M9ScenarioTests]

## 8. Known gaps (expected, not bugs)

- Objects' examine lines and screentips (airlocks, machines, tables...) are not now, by the owner (D264); chat icons (the item's picture in a line) later (not-translated.md 76).
- The fireman carry and piggyback (D263), leaning on walls and the table slam (D249) come after M10; moods, preferences, languages, sprite accessories (hair, underwear) and machine tgui windows are M10+ (not-translated.md 77).
- Text to speech and languages are after the core version.

## 9. Report

Note anything that differs from tg with the room and the steps, and any runtime the server console prints beyond the boot's.
