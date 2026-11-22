using System.Globalization;
using System.Linq;

namespace Anomaly.Client.Shared.Tgui;

// M5 slice 3 (D148): the component kit's element tree. tg's interfaces are React components that return tgui-core elements
// (<Section>, <Button>, <LabeledList.Item>, ...); the port's interfaces are C# functions that return the same elements as UiNode
// trees, which the Godot renderer (Hud/Tgui/TguiRender.cs) turns into controls in tg's dark Nanotrasen theme. The tree is plain data
// (engine-free): the tests read it to check what a window shows and which act each button sends.

/// <summary>The tgui-core component an element stands for.</summary>
public enum UiKind : byte
{
    /// <summary>A React fragment (<c>&lt;&gt;...&lt;/&gt;</c>): its children in place.</summary>
    Fragment,

    /// <summary><c>Box</c>: a block with its text or children (color, bold, font size, text align).</summary>
    Box,

    /// <summary><c>Button</c> (with <c>Button.Checkbox</c> as <see cref="UiNode.Checkbox"/>).</summary>
    Button,

    /// <summary><c>ImageButton</c>: a button with a tg icon (dmIcon, dmIconState) and side buttons.</summary>
    ImageButton,

    /// <summary><c>Section</c>: a titled panel with buttons in its title row.</summary>
    Section,

    /// <summary><c>Stack</c> (and <c>Flex</c>): a row, or a column when <see cref="UiNode.Vertical"/>.</summary>
    Stack,

    /// <summary><c>Stack.Item</c> (and <c>Flex.Item</c>).</summary>
    StackItem,

    /// <summary><c>Stack.Divider</c>.</summary>
    Divider,

    /// <summary><c>LabeledList</c> with <see cref="LabeledItem"/> rows.</summary>
    LabeledList,

    /// <summary><c>LabeledList.Item</c>: a label and its content.</summary>
    LabeledItem,

    /// <summary><c>LabeledControls</c> with <see cref="LabeledControl"/> columns (the control above, the label under it).</summary>
    LabeledControls,

    LabeledControl,

    /// <summary><c>Tabs</c> with <see cref="Tab"/> entries.</summary>
    Tabs,

    Tab,

    /// <summary><c>Collapsible</c>: a button that shows or hides its children.</summary>
    Collapsible,

    /// <summary><c>NoticeBox</c>.</summary>
    NoticeBox,

    /// <summary><c>Icon</c>: an icon of tgui's icon font.</summary>
    Icon,

    /// <summary><c>ProgressBar</c>.</summary>
    ProgressBar,

    /// <summary><c>RoundGauge</c>: a half-round gauge with coloured ranges.</summary>
    RoundGauge,

    /// <summary><c>Knob</c>: a dial dragged up and down.</summary>
    Knob,

    /// <summary><c>Input</c> (and <c>SearchBar</c>): a text field.</summary>
    Input,

    /// <summary><c>RestrictedInput</c> and <c>NumberInput</c>: a number field within a range.</summary>
    NumberInput,

    /// <summary>tgui's <c>Loader</c> of the input modals: a thin bar of the time left.</summary>
    Loader,

    /// <summary>
    /// tgui-core's <c>Floating</c> (M5 slice 8, PersonalCrafting's ingredients): its children are the anchor, its
    /// <see cref="UiNode.Content"/> builds the content shown in a popup under the anchor while the anchor is hovered.
    /// </summary>
    Floating,

    /// <summary>tgui-core's <c>Table</c> with <see cref="TableRow"/> rows of <see cref="TableCell"/> cells (the air alarm's thresholds).</summary>
    Table,

    /// <summary><c>Table.Row</c> (tgui's <c>className="candystripe"</c> as <see cref="UiNode.Candystripe"/>).</summary>
    TableRow,

    /// <summary><c>Table.Cell</c> (<see cref="UiNode.Bold"/>, <see cref="UiNode.Color"/> for its text).</summary>
    TableCell,

    /// <summary>
    /// tgui-core's <c>Modal</c>: its children in a box drawn over the whole window content on a dimmed background (the air alarm's
    /// threshold editor). The window host draws it over the content; it takes no room where it stands in the tree.
    /// </summary>
    Modal,

    /// <summary>
    /// tgui-core's <c>Slider</c> (the SMES's input and output levels): a bar dragged sideways (<see cref="UiNode.StepPixelSize"/> pixels a
    /// step); a click without a drag types the value. <see cref="UiNode.FillValue"/> is the lighter bar behind the value (what is available).
    /// </summary>
    Slider,

    /// <summary>
    /// A black box that draws its <see cref="ChartLine"/> children over each other (SolarControl's <c>&lt;Box backgroundColor="black"&gt;</c> holding
    /// <c>Chart.Line</c>s that <c>fillPositionedParent</c>); its <see cref="UiNode.Height"/> is the box's.
    /// </summary>
    Chart,

    /// <summary>
    /// DetectiveBoard's cork board (gaps J4): a free surface whose children stand at <see cref="UiNode.PosX"/>, <see cref="UiNode.PosY"/>, with <see cref="UiNode.BoardLines"/> under them (tgui's
    /// <c>Connections</c>); a child dragged by the mouse reports its new place through <see cref="UiNode.OnMoved"/>.
    /// </summary>
    Board,

    /// <summary>tgui-core's <c>Chart.Line</c>: <see cref="UiNode.Data"/> scaled to <see cref="UiNode.RangeX"/> and <see cref="UiNode.RangeY"/>, filled and stroked.</summary>
    ChartLine,

    /// <summary>
    /// tgui-core's <c>ColorBox</c>: a square of the CSS colour <see cref="UiNode.Color"/> (its <see cref="UiNode.Width"/> and
    /// <see cref="UiNode.Height"/>, else 1em) with <see cref="UiNode.Text"/> on it (PowerMonitor's channel states); with
    /// <see cref="UiNode.OnClick"/> the RPD's paint swatch, <see cref="UiNode.Selected"/> drawing its green border (M7 slice D172).
    /// </summary>
    ColorBox,

    /// <summary>
    /// A <c>&lt;Box style={{ position: 'relative' }}&gt;</c> of <see cref="UiNode.Width"/> by <see cref="UiNode.Height"/> whose children lie over
    /// each other, each filling it, except the ones with an <see cref="UiNode.Anchor"/> (<c>position: absolute</c> in a corner at their own
    /// size): the StripMenu's slot with its picture, its corner letter and its alternate action buttons (M8z slice B). A <see cref="Button"/>
    /// with children is the same box inside the button (tgui-core's Button takes children as content).
    /// </summary>
    Overlay,

    /// <summary>
    /// tgui-core's <c>TextArea</c> and a raw <c>&lt;textarea&gt;</c> (M9 slice E: the notepad, the messenger's mass message): a multi-line text
    /// field of <see cref="UiNode.TextValue"/>; <see cref="UiNode.OnInput"/> on each change, <see cref="UiNode.OnEnter"/> (when set) on Enter
    /// without Shift; <see cref="UiNode.Wrap"/> wraps its lines; <see cref="UiNode.Tooltip"/> is its placeholder.
    /// </summary>
    TextArea,

    /// <summary>tgui-core's <c>Dropdown</c> (M9 slice E: the notepad's files): <see cref="UiNode.Options"/>, the selected one <see cref="UiNode.TextValue"/>, <see cref="UiNode.OnSelected"/> on a pick; <see cref="UiNode.Tooltip"/> is its placeholder.</summary>
    Dropdown,

    /// <summary>
    /// M10x slice D5 (DV209): Godot's own colour picker where tgui's ColorPickerModal draws tgui's ColorSelector: its colour is
    /// <see cref="UiNode.Color"/> (#rrggbb), <see cref="UiNode.OnInput"/> takes each colour picked.
    /// </summary>
    ColorPicker,

    /// <summary>tgui-core's <c>ByondUi</c> with <c>type: 'map'</c> (M10 slice D: the camera console's map): a second 3D view of <see cref="UiNode.MapRef"/>, drawn from <see cref="UiNode.MapFrom"/> (the REF of the atom it shows from, the active camera; none: static).</summary>
    ByondUi,

    /// <summary>
    /// M10x slice G6 (DV216): a browse() page's HTML (<see cref="UiNode.TextValue"/>) drawn as tg's chat draws its lines
    /// (<c>HtmlText</c>), scrolled; a click on one of its links calls <see cref="UiNode.OnInput"/> with the href.
    /// </summary>
    Html,

    /// <summary>
    /// Gaps S3 (D298): PaperSheet's page: <see cref="UiNode.TextValue"/>'s HTML drawn as the <see cref="Html"/> kind draws it, but as tall
    /// as its text (the window's scrolled item scrolls it) on <see cref="UiNode.Background"/>, with the children over it at
    /// <see cref="UiNode.PosX"/>, <see cref="UiNode.PosY"/> turned by <see cref="UiNode.Rotation"/> about their middle (tg's absolutely
    /// placed stamps). With <see cref="UiNode.Ghost"/> set, that node follows the mouse (Shift turns it toward the mouse) and a left click
    /// calls <see cref="UiNode.OnStamp"/> with its place and turn (tg's PaperSheetStamper).
    /// </summary>
    PaperPage,

    /// <summary>
    /// Gaps CI1 (D339, DV571): IntegratedCircuit's InfinitePlane of ObjectComponents, drawn as Godot's node graph (GraphEdit): its
    /// children are <see cref="GraphNode"/> nodes, <see cref="UiNode.GraphLinks"/> the wires between their ports. A wire dragged from an
    /// output port to an input port calls <see cref="UiNode.OnConnect"/>; an input's wire dragged off its port calls
    /// <see cref="UiNode.OnDisconnect"/>. <see cref="UiNode.PosX"/>, <see cref="UiNode.PosY"/> are the plane's offset (tg's screen_x, screen_y),
    /// reported through <see cref="UiNode.OnMoved"/> when a pan ends; <see cref="UiNode.Selected"/> snaps the nodes to a 10-pixel grid.
    /// </summary>
    NodeGraph,

    /// <summary>
    /// A node of a <see cref="NodeGraph"/> (tg's ObjectComponent): its title <see cref="UiNode.Text"/> on <see cref="UiNode.Background"/> in
    /// <see cref="UiNode.Color"/>, the title row's <see cref="UiNode.Buttons"/>, its place <see cref="UiNode.PosX"/>, <see cref="UiNode.PosY"/>
    /// (<see cref="UiNode.OnMoved"/> when dragged), its rows the <see cref="GraphSlot"/> children. A right click on a port calls
    /// <see cref="UiNode.OnPortRightClick"/> (true for an input, the port's index from 0).
    /// </summary>
    GraphNode,

    /// <summary>A row of a <see cref="GraphNode"/>: its children side by side, an input port at its left in <see cref="UiNode.PortIn"/>'s colour, an output port at its right in <see cref="UiNode.PortOut"/>'s (null: none).</summary>
    GraphSlot,
}

/// <summary>
/// One element of a tgui interface: its component, the props tgui-core reads, its children. Sizes are tgui's: a number is in rem
/// (12 px at 100 %), a string carries its unit (<c>"50px"</c>, <c>"15em"</c>, <c>"100%"</c>).
/// </summary>
public sealed class UiNode
{
    public UiNode(UiKind kind) => Kind = kind;

    public UiKind Kind { get; }

    /// <summary>React's key (for the tests and for the local state of a collapsible or a tab list).</summary>
    public string Key { get; set; }

    // ---- text and look ----

    /// <summary>The element's text (a button's content, a box's text, a section's title, a label).</summary>
    public string Text { get; set; }

    public string Title { get; set; }

    public string Label { get; set; }

    /// <summary>A LabeledList item's label colour (tgui's <c>labelColor</c>, a named colour: the gas mixer's green and blue nodes), or null for the label's own.</summary>
    public string LabelColor { get; set; }

    /// <summary>A LabeledList item's label that is an element (tgui's <c>label={&lt;Button&gt;}</c>: the gas analyzer's gas buttons), shown instead of <see cref="Label"/>.</summary>
    public UiNode LabelNode { get; set; }

    /// <summary>An icon of tgui's icon font (Font Awesome names: <c>"eject"</c>, <c>"power-off"</c>, ...). A Dropdown with an icon is
    /// tgui-core's <c>iconOnly</c> (the RandomizationButton): the icon alone, coloured by <see cref="Color"/>, and no selected text.</summary>
    public string Icon { get; set; }

    /// <summary>tgui's colour name (<c>"good"</c>, <c>"average"</c>, <c>"bad"</c>, <c>"label"</c>, <c>"transparent"</c>, <c>"caution"</c>, <c>"danger"</c>, <c>"yellow"</c>, ...) or a CSS colour.</summary>
    public string Color { get; set; }

    public string Tooltip { get; set; }

    public bool Bold { get; set; }

    /// <summary>A <see cref="UiKind.Divider"/> that is tgui-core's <c>&lt;Divider /&gt;</c> component (a horizontal rule) rather than a stack's <c>Stack.Divider</c>.</summary>
    public bool Rule { get; set; }

    /// <summary>A box's text wraps at the width of its column (HTML's normal flow; the kit's labels do not wrap otherwise); a labeled item's content wraps its elements in rows; a stack's items wrap into rows (tgui's <c>Stack wrap</c>).</summary>
    public bool Wrap { get; set; }

    /// <summary>A dotted underline of the text (PersonalCrafting's ingredient that has its own recipe).</summary>
    public bool Underline { get; set; }

    /// <summary>The font size: a number in rem (tgui's <c>fontSize={0.75}</c>), a string with its unit.</summary>
    public object FontSize { get; set; }

    /// <summary><c>"left"</c>, <c>"center"</c>, <c>"right"</c>.</summary>
    public string TextAlign { get; set; }

    // ---- state ----

    public bool Selected { get; set; }

    /// <summary>A button's CSS background colour (tgui's <c>style.background</c>, as <c>#rrggbb</c>), over its look (the StripMenu's slot being used).</summary>
    public string Background { get; set; }

    /// <summary>tgui-core Button's <c>circular</c>: a round button.</summary>
    public bool Circular { get; set; }

    /// <summary>A button crossed out by a thin dark line at 40 degrees (the preferences' Off priority, its <c>--off::after</c>).</summary>
    public bool Slashed { get; set; }

    /// <summary>tgui's <c>opacity</c> (0 to 1) of a picture, a box or a button, or null for 1.</summary>
    public double? Opacity { get; set; }

    /// <summary>
    /// A child of an <see cref="UiKind.Overlay"/> (or of a button) placed in a corner at its own size (<c>position: absolute</c> with
    /// <c>left</c>, <c>right</c>, <c>top</c> or <c>bottom</c> 0): <c>"bottom-left"</c>, <c>"bottom-right"</c>, <c>"top-left"</c>,
    /// <c>"top-right"</c>; null fills the parent.
    /// </summary>
    public string Anchor { get; set; }

    public bool Disabled { get; set; }

    /// <summary><c>Button.Checkbox</c>'s checked (a checkbox button shows a checked or empty square).</summary>
    public bool? Checkbox { get; set; }

    /// <summary>tgui's <c>candystripe</c> class of a table row: every other row drawn darker.</summary>
    public bool Candystripe { get; set; }

    /// <summary>
    /// A number field that is tgui-core's <c>NumberInput</c>: its <see cref="OnChange"/> fires once the typed value is committed (Enter or
    /// the focus leaving, as tgui-core sends a dragged or typed number when it is let go) rather than at each keystroke, as RestrictedInput.
    /// </summary>
    public bool CommitOnly { get; set; }

    /// <summary>A field that takes the focus when its window opens (tgui-core's <c>autoFocus</c>), its text selected (<c>autoSelect</c>).</summary>
    public bool AutoFocus { get; set; }

    /// <summary>A collapsible's starting state.</summary>
    public bool Open { get; set; }

    // ---- layout ----

    public bool Fluid { get; set; }

    public bool Fill { get; set; }

    public bool Scrollable { get; set; }

    /// <summary>A stack's direction (column), a tab list's direction.</summary>
    public bool Vertical { get; set; }

    /// <summary>A stack laid out in reverse (tgui's <c>reverse</c>, <c>row-reverse</c>).</summary>
    public bool Reverse { get; set; }

    /// <summary>A stack item's share of the free space (tgui's <c>grow</c>; true is 1).</summary>
    public double Grow { get; set; }

    /// <summary>A stack's <c>justify</c> (<c>"space-around"</c>, ...) and <c>align</c>.</summary>
    public string Justify { get; set; }

    public string Align { get; set; }

    public object Width { get; set; }

    public object Height { get; set; }

    public object MinWidth { get; set; }

    /// <summary>A gauge's or knob's size (tgui's <c>size</c>, in rem multiples of its base).</summary>
    public double Size { get; set; } = 1;

    // ---- values ----

    public double Value { get; set; }

    public double MinValue { get; set; }

    public double MaxValue { get; set; } = 1;

    public double Step { get; set; } = 1;

    public double StepPixelSize { get; set; } = 1;

    /// <summary>A slider's lighter bar behind its value (tgui's <c>fillValue</c>), or null.</summary>
    public double? FillValue { get; set; }

    public string Unit { get; set; }

    /// <summary>A gauge's alert threshold (tgui's <c>alertAfter</c>), or null.</summary>
    public double? AlertAfter { get; set; }

    /// <summary>A gauge's low alert threshold (tgui's <c>alertBefore</c>: the Hypertorus window's integrity and area power), or null.</summary>
    public double? AlertBefore { get; set; }

    /// <summary>A gauge's or progress bar's coloured ranges (tgui's <c>ranges</c>: name to [from, to]).</summary>
    public IReadOnlyList<(string Color, double From, double To)> Ranges { get; set; }

    /// <summary>How a value is shown (tgui's <c>format</c>).</summary>
    public Func<double, string> Format { get; set; }

    /// <summary>A text field's text.</summary>
    public string TextValue { get; set; }

    /// <summary>M10 slice D: a ByondUi map's id (tg's <c>mapRef</c>).</summary>
    public string MapRef { get; set; }

    /// <summary>M10 slice D: the REF of the atom a ByondUi map is drawn from (<c>"[0x1f]"</c>), null for static.</summary>
    public string MapFrom { get; set; }

    /// <summary>
    /// M10x slice E2 (D284): a ByondUi map with no turfs (tg's CharacterPreview): its screen objects (those whose screen_loc names
    /// <see cref="MapRef"/>) drawn as one 2D picture, each facing its appearance's dir, instead of a second 3D view.
    /// </summary>
    public bool MapPicture { get; set; }

    /// <summary>An icon's turn in degrees, clockwise (tgui-core's <c>rotation</c>: SolarControl's arrow at the azimuth); a picture of tg's icons turns the same way (CSS <c>rotate</c>: the safe's dial).</summary>
    public double Rotation { get; set; }

    /// <summary>An icon that turns round and round (tgui-core's <c>spin</c>, Font Awesome's fa-spin: AirlockController's fan).</summary>
    public bool Spin { get; set; }

    /// <summary>A <see cref="UiKind.Chart"/> that is only its lines, filling its parent (PowerMonitor's <c>fillPositionedParent</c> lines in a section): no black box, no padding.</summary>
    public bool Plain { get; set; }

    /// <summary>A chart line's points (x, y), in tgui's order.</summary>
    public IReadOnlyList<(double X, double Y)> Data { get; set; }

    /// <summary>A chart line's x and y ranges (tgui's <c>rangeX</c>, <c>rangeY</c>): the values at its edges.</summary>
    public (double Min, double Max) RangeX { get; set; }

    public (double Min, double Max) RangeY { get; set; }

    /// <summary>
    /// A chart line's colours: CSS <c>rgba(r, g, b, a)</c> (tgui's <c>strokeColor</c>, <c>fillColor</c>). On a button, a 2 px outline of
    /// that colour (a tgui colour name or #rrggbb; gaps P3: tgui's <c>outline-color-*</c> classes).
    /// </summary>
    public string StrokeColor { get; set; }

    public string FillColor { get; set; }

    public int MaxLength { get; set; }

    /// <summary>A number field takes fractions (tgui's <c>allowFloats</c>).</summary>
    public bool AllowFloats { get; set; }

    // ---- tg's icon of an ImageButton, or of a Box (a sprite sheet class of tg's: drawn at 32 px times Size) ----

    public string DmIcon { get; set; }

    /// <summary>A dropdown's options (tgui's <c>options</c>).</summary>
    public IReadOnlyList<string> Options { get; set; }

    public string DmIconState { get; set; }

    /// <summary>
    /// Silicons SI4d (D326): a picture of raw pixels drawn instead of <see cref="DmIcon"/> (tgui's SpriteEditor canvas, a painting being
    /// painted), sized by <see cref="Width"/> and <see cref="Height"/>; its clicks read as <see cref="OnClickAt"/>.
    /// </summary>
    public UiPixels Pixels { get; set; }

    /// <summary>
    /// A picture shows only its drawn pixels, scaled into its box (M10x slice C: the orbit menu's JobIcon, a sechud icon of a few pixels
    /// in a 32 px state that tg's CSS enlarges and crops in a 20 px box).
    /// </summary>
    public bool CropToPixels { get; set; }

    // ---- events ----

    public Action OnClick { get; set; }

    /// <summary>
    /// A click on a picture (a <see cref="UiKind.Box"/> with <see cref="DmIcon"/>) at a point of it, in pixels from its left and from its
    /// bottom (gaps P1: tgui's BodyZoneSelector, which turns the mouse's place on the doll into a body zone).
    /// </summary>
    public Action<double, double> OnClickAt { get; set; }

    /// <summary>The mouse moving over such a picture, at a point of it as <see cref="OnClickAt"/>; (-1, -1) when it leaves (the selector's hover).</summary>
    public Action<double, double> OnHoverAt { get; set; }

    /// <summary>
    /// A button's click with the modifier keys held at the click, and its right click (tgui-core's <c>onClick(event)</c> reading
    /// <c>event.altKey</c> and the rest, and <c>onContextMenu</c>: the loot panel's items, gaps package G1). Used besides <see cref="OnClick"/>.
    /// </summary>
    public Action<UiClick> OnClickWith { get; set; }

    /// <summary>A button's double click (tgui-core's <c>onDoubleClick</c>; the list input's pick).</summary>
    public Action OnDoubleClick { get; set; }

    /// <summary>An input's Enter (tgui-core Input's <c>onEnter</c>), besides its commit.</summary>
    public Action OnEnter { get; set; }

    /// <summary>A knob's or number field's new value.</summary>
    public Action<double> OnChange { get; set; }

    /// <summary>A text field's text while it is typed (tgui's <c>onChange</c>, <c>onSearch</c>).</summary>
    public Action<string> OnInput { get; set; }

    /// <summary>A dropdown's pick (tgui's <c>onSelected</c>).</summary>
    public Action<string> OnSelected { get; set; }

    /// <summary>A text field's text when it loses focus or Enter is pressed (tgui's <c>onBlur</c>, <c>onEnter</c>).</summary>
    public Action<string> OnCommit { get; set; }

    /// <summary>A number field's validity (tgui's <c>onValidationChange</c>).</summary>
    public Action<bool> OnValidation { get; set; }

    /// <summary>HTML5 drag and drop (tgui's <c>draggable</c> with <c>onDragStart</c>): set, the node can be dragged; it runs as the drag starts.</summary>
    public Action OnDragStart { get; set; }

    /// <summary>tgui's <c>onDragEnd</c>: the drag that <see cref="OnDragStart"/> began has ended, dropped or not.</summary>
    public Action OnDragEnd { get; set; }

    /// <summary>tgui's <c>onDragEnter</c>: a drag came over the node (it takes drops while <see cref="OnDrop"/> is set).</summary>
    public Action OnDragEnter { get; set; }

    /// <summary>tgui's <c>onDrop</c> (with <c>onDragOver</c>'s preventDefault): set, the node takes a drop.</summary>
    public Action OnDrop { get; set; }

    // ---- children ----

    /// <summary>A section's or image button's title-row buttons, a collapsible's header buttons, or a LabeledList item's buttons cell (tgui's <c>buttons</c> prop).</summary>
    public UiNode Buttons { get; set; }

    /// <summary>Gaps S2: a non-fluid image button's <c>buttonsAlt</c> (tgui-core's grid card: the picture, this row, the content under it).</summary>
    public UiNode ButtonsAlt { get; set; }

    /// <summary>
    /// A <see cref="UiKind.Floating"/>'s popup content, built when the popup first opens, as React renders tgui-core's Floating content
    /// only while it is shown (the owner's crash, 2026-10-16: PersonalCrafting's ingredients float their own recipes, whose ingredients
    /// float theirs, and two recipes that need each other recursed without end when built at once). <see cref="Walk"/> does not enter it.
    /// </summary>
    /// <summary>A <see cref="UiKind.Board"/> child's place on the board; an <see cref="UiKind.Overlay"/> child anchored top-left: its offset in pixels.</summary>
    public double PosX { get; set; }

    /// <summary>A <see cref="UiKind.Board"/> child's place on the board; an <see cref="UiKind.Overlay"/> child anchored top-left: its offset in pixels.</summary>
    public double PosY { get; set; }

    /// <summary>A <see cref="UiKind.Board"/>'s connections, drawn under its children.</summary>
    public IReadOnlyList<(double FromX, double FromY, double ToX, double ToY, string Color)> BoardLines { get; set; }

    /// <summary>A <see cref="UiKind.Board"/> child was dragged to this place.</summary>
    public Action<double, double> OnMoved { get; set; }

    /// <summary>Gaps CI1: a <see cref="UiKind.GraphSlot"/>'s input port colour (a tg colour name), or null for none.</summary>
    public string PortIn { get; set; }

    /// <summary>Gaps CI1: a <see cref="UiKind.GraphSlot"/>'s output port colour (a tg colour name), or null for none.</summary>
    public string PortOut { get; set; }

    /// <summary>
    /// Gaps CI1: a <see cref="UiKind.NodeGraph"/>'s wires: from the output port <c>FromPort</c> (from 0, counting the node's output ports)
    /// of the child <c>FromNode</c> (from 0) to the input port <c>ToPort</c> of the child <c>ToNode</c>, in the output port's colour.
    /// </summary>
    public IReadOnlyList<(int FromNode, int FromPort, int ToNode, int ToPort, string Color)> GraphLinks { get; set; }

    /// <summary>Gaps CI1: a <see cref="UiKind.NodeGraph"/>'s wire dragged from an output to an input (from node, from port, to node, to port).</summary>
    public Action<int, int, int, int> OnConnect { get; set; }

    /// <summary>Gaps CI1: a <see cref="UiKind.NodeGraph"/>'s wire dragged off its input port (from node, from port, to node, to port).</summary>
    public Action<int, int, int, int> OnDisconnect { get; set; }

    /// <summary>Gaps CI1: a kit drag (<see cref="OnDragStart"/>) dropped on a <see cref="UiKind.NodeGraph"/>: the place on its plane, unzoomed.</summary>
    public Action<double, double> OnDropAt { get; set; }

    /// <summary>Gaps CI1: a right click on a <see cref="UiKind.GraphNode"/>'s port: true for an input, the port's index from 0.</summary>
    public Action<bool, int> OnPortRightClick { get; set; }

    /// <summary>Gaps S3: a <see cref="UiKind.PaperPage"/>'s stamp that follows the mouse, or null.</summary>
    public UiNode Ghost { get; set; }

    /// <summary>Gaps S3: a <see cref="UiKind.PaperPage"/>'s click with its <see cref="Ghost"/>: the ghost's place (x, y) and turn in degrees.</summary>
    public Action<double, double, double> OnStamp { get; set; }

    public Func<UiNode> Content { get; set; }

    public List<UiNode> Children { get; } = new();

    /// <summary>Adds children (nulls, React's <c>{false &amp;&amp; ...}</c>, are skipped); returns this node.</summary>
    public UiNode With(params UiNode[] children)
    {
        foreach (UiNode child in children)
            if (child != null)
                Children.Add(child);
        return this;
    }

    /// <summary>Adds children from a sequence; returns this node.</summary>
    public UiNode With(IEnumerable<UiNode> children) => With(children.ToArray());

    /// <summary>Every node of the tree, this one first (depth first, the buttons rows, then an image button's buttonsAlt, before the children).</summary>
    public IEnumerable<UiNode> Walk()
    {
        yield return this;
        if (LabelNode != null)
            foreach (UiNode n in LabelNode.Walk())
                yield return n;
        if (Buttons != null)
            foreach (UiNode n in Buttons.Walk())
                yield return n;
        if (ButtonsAlt != null)
            foreach (UiNode n in ButtonsAlt.Walk())
                yield return n;
        foreach (UiNode child in Children)
            foreach (UiNode n in child.Walk())
                yield return n;
    }

    /// <summary>The visible text of the node and its children, joined by spaces (what a reader would see).</summary>
    /// <summary>
    /// tgui-core LabeledList.Item's row: the label cell (a string label gets a colon, an element label is shown as it is), the content
    /// cell (the children) and the buttons cell after the content (null: the content spans its column).
    /// </summary>
    public (UiNode LabelNode, string LabelText, UiNode Buttons) LabeledRow() =>
        (LabelNode, LabelNode != null || string.IsNullOrEmpty(Label) ? "" : Label + ":", Buttons);

    public string AllText() => string.Join(" ", Walk().SelectMany(n => new[] { n.Title, n.Label, n.Text }).Where(t => !string.IsNullOrEmpty(t)));

    public override string ToString() => Kind + (Text != null ? " '" + Text + "'" : "") + (Title != null ? " title='" + Title + "'" : "") + (Label != null ? " label='" + Label + "'" : "");
}

/// <summary>The keys held at a click of <see cref="UiNode.OnClickWith"/>, or the right button (then no key is passed on).</summary>
public readonly record struct UiClick(bool Alt, bool Ctrl, bool Shift, bool Right);

/// <summary>A size of tgui's: pixels, or a share of the parent.</summary>
public readonly record struct UiSize(double Pixels, double Percent)
{
    /// <summary>tgui's unit(): a number is rem (12 px each at 100 %), <c>"Npx"</c> pixels, <c>"Nem"</c>/<c>"Nrem"</c> 12 px each, <c>"N%"</c> a share; null, false or unknown text is none.</summary>
    public static UiSize? Of(object value)
    {
        switch (value)
        {
            case null:
            case bool:
                return null;
            case double d:
                return new UiSize(d * Rem, 0);
            case int i:
                return new UiSize(i * Rem, 0);
            case float f:
                return new UiSize(f * Rem, 0);
            case string s:
                s = s.Trim();
                if (s.EndsWith("px", StringComparison.Ordinal) && double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out double px))
                    return new UiSize(px, 0);
                if (s.EndsWith("rem", StringComparison.Ordinal) && double.TryParse(s[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out double rem))
                    return new UiSize(rem * Rem, 0);
                if (s.EndsWith("em", StringComparison.Ordinal) && double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out double em))
                    return new UiSize(em * Rem, 0);
                if (s.EndsWith('%') && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                    return new UiSize(0, pct);
                return null;
            default:
                return null;
        }
    }

    /// <summary>tgui's rem: 12 px (tgui-core's base font size).</summary>
    public const double Rem = 12;
}

/// <summary>Silicons SI4d: the pixels of a <see cref="UiNode.Pixels"/> picture: each 0xRRGGBBAA, rows from the top.</summary>
public sealed record UiPixels(int Width, int Height, uint[] Rgba);
