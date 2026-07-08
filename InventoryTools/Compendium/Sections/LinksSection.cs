using System.Collections.Generic;
using System.Numerics;
using AllaganLib.Shared.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using InventoryTools.Compendium.Models;
using InventoryTools.Compendium.Sections.Options;
using InventoryTools.Services;
using OtterGui;

namespace InventoryTools.Compendium.Sections;

public class LinksSection : ViewSection
{
    private readonly LinksSectionOptions _options;

    public delegate LinksSection Factory(LinksSectionOptions options);

    public LinksSection(LinksSectionOptions options, ImGuiService imGuiService) : base(options, imGuiService)
    {
        _options = options;
    }

    public override string SectionName => "Links";
    public override void DrawSection(SectionState sectionState)
    {
        for (var index = 0; index < _options.Links.Count; index++)
        {
            var link = _options.Links[index];
            if (ImGui.ImageButton(link.texture.GetWrapOrEmpty().Handle,
                    new Vector2(32, 32) * ImGui.GetIO().FontGlobalScale))
            {
                link.Link.OpenBrowser();
            }

            ImGuiUtil.HoverTooltip(link.HelpText);
            if (index != _options.Links.Count - 1)
            {
                ImGui.SameLine();
            }
        }
    }
}

public record LinksSectionOptions : SectionOptions
{
    public LinksSectionOptions()
    {
        this.HideHeader = true;
        this.HideWhenEmpty = true;
    }
    public override required string SectionName { get; init; } = "Links";
    public List<(string Link, string HelpText, ISharedImmediateTexture texture)> Links { get; init; } = new();
}