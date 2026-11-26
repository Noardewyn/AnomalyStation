# M9 chat, emote wheel and PDA messages: UX draft (D246-D254)

Status: draft for the owner's review (2026-10-22). The owner's requirements are D247, D248 and D250-D256; everything marked *proposal* is my choice and open to change.

tg references: the compact say window `tgui/packages/tgui-say` (DM side `code/modules/tgui_input/say_modal/`), the chat panel `tgui/packages/tgui-panel/chat` (pages filtered by message type), floating speech `code/datums/chatmessage.dm` (runechat), the typing indicator `say_modal/typing.dm`, hotkeys `code/datums/keybinding/communication.dm`.

## 1. Keys (tg defaults where tg has them)

| Key | Opens | Source |
|---|---|---|
| T | compact bar, Say | tg `say` [T] |
| Y | compact bar, Radio | tg `radio` [Y] |
| M | compact bar, Me | tg `me` [M] |
| O | compact bar, OOC | tg `ooc` [O] |
| Shift+T | the big chat window | owner (D247) |
| J | the emote wheel | D256: tg has no emote key; G was tg's `give` (offering an item, M9), so J, a letter tg leaves free |
| G | the crafting menu (was give; the offer is the radial's "Offer item" since the UI overhaul's package 2, slice E) | Space Station 14's `OpenCraftingMenu` [G] |
| P | compact bar, Pray | tg `pray` [P] |

Opening the compact bar closes the big window and the other way round (D247). All keys are rebindable in the options like every other action.

## 2. The compact bar: tg's tgui-say as it is (D250)

- tg's small draggable window, 231 px wide, a light and a dark look (tg's `tgui_say_light_mode` preference). Left: the **channel button**, coloured by channel; a click cycles the channel. Right of it: the input.
- tg's limit: `MAX_MESSAGE_LEN` (1024 characters). The window grows to 2 and then 3 lines at tg's line lengths (20, 39, 59 characters) and shrinks back.
- Keyboard only, the cursor is not needed:
  - **Tab** cycles the channel: Say → Radio → Me → OOC → Pray (tg's `ChannelIterator`; Admin is skipped unless you are an admin).
  - Typing a radio prefix (`:e `, `:m ` and the rest of tg's `RADIO_PREFIXES`) relabels the button with the channel's name.
  - **Up / Down** walk the sent-message history (tg `ChatHistory`).
  - **Enter** sends and closes; **Esc** closes and clears, as in tg.
- While it is open the game gets no keys, as in tg.
- Others see tg's **typing indicator** over your head on Say, Radio and Me (tg's quiet channels OOC, Admin and Pray show none).
- Whisper and shout are not channels in tg's window: they are the big window's buttons, tg's `whisper` verb, and the "!!" ending for a shout (D253).

## 3. The big chat window

- Opened and closed with Shift+T; opening it hides the fading feed (D252). Movable by its title bar, **resizable** by any edge or corner; minimum about 360×240. Size and position are saved in the options. Everything inside follows the size (Godot containers): the log takes the free space, the button rows wrap onto a second line when narrow.
- **Top: tabs** as tg's chat pages: All, Local (speech and emotes), Radio, Combat, OOC, Messages. *Proposal*: tg's own page editor (custom tabs with type filters) comes later.
- **Middle: the log**: everything said, heard, seen and done to you, in tg's colours and styles; up to 2500 lines kept; text can be selected and copied; when you scroll up, new lines do not pull you down and a "↓ new messages" button appears.
- **Bottom: the action row**: Say, Whisper, Shout, Radio ▾ (lists the channels your headset has), Me, Emotes ▾ (the full emote list), OOC, Message (opens your PDA's messenger, D251). A click picks the channel and puts the caret in the input.
- **Bottom: the input row**: the channel button, a multi-line input (tg's 1024 characters), Send. Same keys as the compact bar; Tab here also reaches Whisper and Shout (tg has no LOOC).
- The cursor is visible while the window is open. With the input not focused the game keeps its keys (you can walk and read); Esc leaves the input; the close button or Shift+T closes the window (D282: a second Esc opens the menu).
- Done in M9 slice C (D259): the tabs are All, Local, Radio, Combat and OOC (Messages comes with the PDA, slice E; the Radio list too, D260:
  one protocol change with the PDA); a whisper goes out with tg's `#`, a shout with tg's `!!`; the binding is `Shift_T` (a key with Shift held).
  Slice E (D262): the Messages tab (the PDA's lines, DV171), the Radio list (protocol 40) and the Message button.
- **The fading feed** (D252): while the big window is closed, the last few lines show at the screen's bottom-left and fade after about 10 s; opening the big window hides it.
- The feed's lines are in tg's dark chat colours (`tgui-panel/styles/tgchat/chat-dark.scss`, `Shared/Play/ChatStyles.cs`: the classes' colour, bold and italic; the owner, 2026-10-23); runechat keeps tg's colour of the speaker's name.

### Writing to another player: the PDA (D251)

In tg, players message each other in character with the PDA's messenger (NtOS, `modular_computers/file_system/programs/messenger`). M9 ports the station PDA with NtOS and its starting apps; the "Message" button opens the messenger of the PDA you carry (none carried: "You have no PDA."). Received messages also land in the log's Messages tab, with tg's PDA ring.

## 4. Speech in the world

- **Runechat**: tg's floating text over the speaker (`chatmessage.dm`), drawn in 3D over the head, with tg's colours, timing and stacking (D142).
- **Whisper**: tg's whisper (heard clearly at 1 tile, as "whispers something" a bit further).
- **Shout** (D253): tg's yell (`verb_yell`, the "!!" ending) with a longer range than say, because the 3D world is seen and heard farther; the numbers are set in the M9 plan and registered as a deviation.
- **Radio**: headsets, the station's channels and their prefixes, intercoms; tg's `SSradio` and telecomms subset (the full telecomms machinery is decided in the M9 plan).
- Identity (D56): a masked speaker is "Unknown" in chat and runechat.

## 5. The emote wheel

- J opens a wheel in the screen centre, the same radial as the context menu. Hold J and release over a segment to emote; a tap keeps the wheel open until a click or Esc.
- Default eight: wave, nod, laugh, shrug, clap, salute, sigh, scream. A ninth segment, "More…", opens the full list of tg's emotes for your mob. *Proposal*: choosing your own eight comes later.
- Done in M9 slice B (D258): "More…" opens tg's own emote panel (its search, filters and sound previews); a second J, Esc or a right click closes a wheel left open by a tap.

## 6. Voice and text-to-speech

Not in M9 (D254): added later.
