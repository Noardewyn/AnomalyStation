using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosScipaper.jsx: the NT Frontier app in an NtOS
// window: the four tabs (the submission form with its file, experiment, tier and partner dropdowns, the author, title and abstract
// and the expected results with the publish button; the published papers; the ordnance experiments with their optimal amounts per
// tier; the scientific partners with their relations, bonuses, accepted experiments and node boosts to purchase). tgui-core's
// BlockQuote is a box. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosScipaper
{
    // tg: NtosScipaper.jsx:19
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 600, 600, null, NtosScipaperContent(c));

    // tg: NtosScipaper.jsx:384
    public static UiNode NtosScipaperContent(TguiContext c)
    {
        UiValue data = c.Data;
        double currentTab = data.Num("currentTab");
        var content = new UiNode(UiKind.Fragment);
        if (!data.Truthy("has_techweb"))
            content.With(new UiNode(UiKind.Section) { Key = "no-techweb", Title = "No techweb detected!", Text = "Please sync this application to a valid techweb to upload progress!" });
        UiNode Tab(int tab, string text) => new UiNode(UiKind.Tab)
        {
            Key = "tab" + tab, Selected = currentTab == tab, Text = text, OnClick = () => c.Act("change_tab", ("new_tab", tab)),
        };
        content.With(new UiNode(UiKind.Tabs) { Key = "navigation", Fluid = true, Align = "center" }.With(
            Tab(1, "Publish Papers"), Tab(2, "Publications"), Tab(3, "Experiments"), Tab(4, "Scientific Partners")));
        if (currentTab == 1)
            content.With(PaperPublishing(c));
        else if (currentTab == 2)
            content.With(PaperBrowser(c));
        else if (currentTab == 3)
            content.With(ExperimentBrowser(c));
        else if (currentTab == 4)
            content.With(PartnersBrowser(c));
        return content;
    }

    // A labeled row whose label carries the info button tg puts in its buttons slot.
    private static UiNode Info(string key, string label, string tooltip, UiNode content) =>
        new UiNode(UiKind.LabeledItem)
        {
            Key = key, Label = label, Buttons = new UiNode(UiKind.Button) { Key = key + "-info", Tooltip = tooltip, Icon = "info-circle" },
        }.With(content);

    // tg: NtosScipaper.jsx:28
    private static UiNode PaperPublishing(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<KeyValuePair<string, UiValue>> fileList = data.Obj("fileList");
        IReadOnlyList<KeyValuePair<string, UiValue>> expList = data.Obj("expList");
        IReadOnlyList<UiValue> allowedTiers = data.Arr("allowedTiers");
        IReadOnlyList<KeyValuePair<string, UiValue>> allowedPartners = data.Obj("allowedPartners");
        UiValue gains = data.Field("gains");
        string coopIndex = data.Str("coopIndex");
        string fundingIndex = data.Str("fundingIndex");
        UiValue tier = data.Field("tier");

        var form = new UiNode(UiKind.Section) { Key = "form", Title = "Submission Form" };
        if (fileList.Count == 0)
            form.With(new UiNode(UiKind.NoticeBox) { Key = "no-files", Text = "Use data disk to download files from compressor or doppler array." });
        form.With(new UiNode(UiKind.LabeledList) { Key = "fields" }.With(
            Info("file", "File (required)", "The selected file containing experimental data for our paper. Must be present in the local file system or a data disk to be accesible.",
                new UiNode(UiKind.Dropdown)
                {
                    Key = "select_file", Width = "100%", Options = fileList.Select(f => f.Key).ToList(), TextValue = data.Str("selectedFile", null),
                    OnSelected = name => c.Act("select_file", ("selected_uid", fileList.FirstOrDefault(f => f.Key == name).Value)),
                }),
            Info("experiment", "Experiment (required)", "The topic we want to publish our paper on. Different topics unlock different technologies and possible partners.",
                new UiNode(UiKind.Dropdown)
                {
                    Key = "select_experiment", Width = "100%", Options = expList.Select(e => e.Key).ToList(), TextValue = data.Str("selectedExperiment", null),
                    OnSelected = name => c.Act("select_experiment", ("selected_expath", expList.FirstOrDefault(e => e.Key == name).Value)),
                }),
            Info("tier", "Tier (required)", "The tier we want to publish on. Higher tiers can confer better rewards but means our data will be judged more harshly.",
                new UiNode(UiKind.Dropdown)
                {
                    Key = "select_tier", Width = "100%", Options = allowedTiers.Select(t => t.AsStr()).ToList(), TextValue = tier == null ? "null" : tier.AsStr(),
                    OnSelected = value => c.Act("select_tier", ("selected_tier", double.Parse(value, System.Globalization.CultureInfo.InvariantCulture))),
                }),
            Info("partner", "Partner (required)", "Which organization to partner with. We can obtain research boosts in techs related to the partner's interests.",
                new UiNode(UiKind.Dropdown)
                {
                    Key = "select_partner", Width = "100%", Options = allowedPartners.Select(p => p.Key).ToList(), TextValue = data.Str("selectedPartner", null),
                    OnSelected = name => c.Act("select_partner", ("selected_partner", allowedPartners.FirstOrDefault(p => p.Key == name).Value)),
                }),
            new UiNode(UiKind.LabeledItem)
            {
                Key = "author", Label = "Principal Author",
                Buttons = new UiNode(UiKind.Button) { Key = "et_alia", Tooltip = "Multiple", Selected = data.Truthy("etAlia"), Icon = "users", OnClick = () => c.Act("et_alia") },
            }.With(new UiNode(UiKind.Input) { Key = "author-input", Fluid = true, TextValue = data.Str("author"), OnCommit = value => c.Act("rewrite", ("author", value)) }),
            new UiNode(UiKind.LabeledItem) { Key = "title", Label = "Title" }.With(
                new UiNode(UiKind.Input) { Key = "title-input", Fluid = true, TextValue = data.Str("title"), OnCommit = value => c.Act("rewrite", ("title", value)) }),
            new UiNode(UiKind.LabeledItem) { Key = "abstract", Label = "Abstract" }.With(
                new UiNode(UiKind.Input) { Key = "abstract-input", Fluid = true, TextValue = data.Str("abstract"), OnCommit = value => c.Act("rewrite", ("abstract", value)) })));

        UiNode results = new UiNode(UiKind.Section) { Key = "rewards", Title = "Expected Results" }.With(
            new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "coop", Grow = 1 }.With(
                    new UiNode(UiKind.Button) { Key = "coop-info", Tooltip = "How much will our relation improve with the particular partner. Cooperation will be used to unlock boosts.", Icon = "info-circle" },
                    new UiNode(UiKind.Box) { Key = "coop-label", Text = " Cooperation: " },
                    new UiNode(UiKind.Box) { Key = "coop-value", Text = gains.Str(coopIndex) }),
                new UiNode(UiKind.StackItem) { Key = "funding", Grow = 1 }.With(
                    new UiNode(UiKind.Button) { Key = "funding-info", Tooltip = "How much grant will we be endowed with upon the publication of this paper.", Icon = "info-circle" },
                    new UiNode(UiKind.Box) { Key = "funding-label", Text = " Funding: " },
                    new UiNode(UiKind.Box) { Key = "funding-value", Text = gains.Str(fundingIndex) })),
            new UiNode(UiKind.Button) { Key = "publish", Icon = "upload", TextAlign = "center", Fluid = true, Text = "Publish Paper", OnClick = () => c.Act("publish") });
        return new UiNode(UiKind.Fragment).With(form, results);
    }

    // tg: NtosScipaper.jsx:212
    private static UiNode PaperBrowser(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> publishedPapers = data.Arr("publishedPapers");
        string coopIndex = data.Str("coopIndex");
        string fundingIndex = data.Str("fundingIndex");
        if (publishedPapers.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "no-papers", Text = " No Published Papers! " };
        return new UiNode(UiKind.Fragment).With(publishedPapers.Select(paper =>
            new UiNode(UiKind.Collapsible) { Key = "paper-" + paper.Str("experimentName") + paper.Str("tier"), Title = paper.Str("title") }.With(
                new UiNode(UiKind.Section).With(new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "topic", Label = "Topic", Text = paper.Str("experimentName") + " - " + paper.Str("tier") },
                    new UiNode(UiKind.LabeledItem) { Key = "author", Label = "Author", Text = paper.Str("author") + (paper.Truthy("etAlia") ? " et al." : "") },
                    new UiNode(UiKind.LabeledItem) { Key = "partner", Label = "Partner", Text = paper.Str("partner") },
                    new UiNode(UiKind.LabeledItem) { Key = "yield", Label = "Yield" }.With(new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Key = "cooperation", Label = "Cooperation", Text = paper.Field("gains").Str(coopIndex) },
                        new UiNode(UiKind.LabeledItem) { Key = "funding", Label = "Funding", Text = paper.Field("gains").Str(fundingIndex) })),
                    new UiNode(UiKind.LabeledItem) { Key = "abstract", Label = "Abstract", Text = paper.Str("abstract") })))));
    }

    // tg: NtosScipaper.jsx:256
    private static UiNode ExperimentBrowser(TguiContext c)
    {
        IReadOnlyList<UiValue> experimentInformation = c.Data.Arr("experimentInformation");
        return new UiNode(UiKind.Fragment).With(experimentInformation.Select(experiment =>
        {
            IReadOnlyList<UiValue> target = experiment.Arr("target");
            return new UiNode(UiKind.Section) { Key = "exp-" + experiment.Str("name"), Title = experiment.Str("name") }.With(
                new UiNode(UiKind.Box) { Key = "description", Text = experiment.Str("description") },
                new UiNode(UiKind.LabeledList) { Key = "tiers" }.With(target.Select((amount, tier) =>
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "tier" + tier,
                        Label = "Optimal " + experiment.Str("prefix") + " Amount - Tier " + (tier + 1),
                        Text = amount.AsStr() + " " + experiment.Str("suffix"),
                    })));
        }));
    }

    // tg: NtosScipaper.jsx:284
    private static UiNode PartnersBrowser(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> partnersInformation = data.Arr("partnersInformation");
        string coopIndex = data.Str("coopIndex");
        string fundingIndex = data.Str("fundingIndex");
        UiValue purchaseableBoosts = data.Field("purchaseableBoosts");
        UiValue relations = data.Field("relations");
        var visibleNodes = data.Arr("visibleNodes").Select(n => n.AsStr()).ToHashSet();
        return new UiNode(UiKind.Fragment).With(partnersInformation.Select(partner =>
        {
            string path = partner.Str("path");
            var boosts = purchaseableBoosts.Arr(path).Select(n => n.AsStr()).ToHashSet();
            string relation = relations.Str(path, "undefined");
            UiNode table = new UiNode(UiKind.Table) { Key = "boosts" }.With(partner.Arr("boostedNodes").Select(node =>
            {
                string id = node.Str("id");
                return new UiNode(UiKind.TableRow) { Key = "node-" + id }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = visibleNodes.Contains(id) ? node.Str("name") : "Unknown Technology" },
                    new UiNode(UiKind.TableCell) { Key = "buy" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "purchase-" + id, Fluid = true, TextAlign = "center", Disabled = !boosts.Contains(id), Text = "Purchase",
                        Tooltip = "Discount: " + node.Str("discount"),
                        OnClick = () => c.Act("purchase_boost", ("purchased_boost", id), ("boost_seller", path)),
                    }));
            }));
            return new UiNode(UiKind.Section) { Key = "partner-" + path, Title = partner.Str("name") }.With(
                new UiNode(UiKind.Collapsible) { Key = "relations-" + path, Title = "Relations: " + relation }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Key = "description", Label = "Description", Text = partner.Str("flufftext") },
                        new UiNode(UiKind.LabeledItem) { Key = "relations", Label = "Relations", Text = relation },
                        new UiNode(UiKind.LabeledItem) { Key = "coop", Label = "Cooperation Bonus", Text = partner.Field("multipliers").Str(coopIndex) + "x" },
                        new UiNode(UiKind.LabeledItem) { Key = "funding", Label = "Funding Bonus", Text = partner.Field("multipliers").Str(fundingIndex) + "x" },
                        new UiNode(UiKind.LabeledItem) { Key = "accepted", Label = "Accepted Experiments" }.With(
                            partner.Arr("acceptedExperiments").Select(e => new UiNode(UiKind.Box) { Key = "accepted-" + e.AsStr(), Text = e.AsStr() })),
                        new UiNode(UiKind.LabeledItem) { Key = "sharing", Label = "Technology Sharing" }.With(table))));
        }));
    }
}
