using System.Collections.Generic;
using AllaganLib.GameSheets.ItemSources;
using InventoryTools.Ui;

namespace InventoryTools.Compendium.Sections.Options;

public record ItemSourcesSectionOptions : SectionOptions
{
    public required List<ItemSource> Sources { get; init; }
    public required SourceType SourceType { get; init; }
}