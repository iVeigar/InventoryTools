using System;
using System.Collections.Generic;
using Lumina.Excel;

namespace InventoryTools.Compendium.Sections.Options;

public sealed record CollectionRowRefSectionOptions : SectionOptions
{
    public required List<RowRef> RelatedRefs { get; init; }
    public Type? Filter { get; init; } = null;
}