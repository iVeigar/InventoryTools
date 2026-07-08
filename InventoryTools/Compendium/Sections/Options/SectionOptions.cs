namespace InventoryTools.Compendium.Sections.Options;

/// <summary>
/// Configuration options for a compendium section, controlling its display name and visibility behaviour.
/// </summary>
public record SectionOptions
{
    /// <summary>
    /// The unique identifier for the section, used when reordering a section or saving it' state.
    /// </summary>
    public required string SectionKey { get; init; }

    /// <summary>
    /// The display name shown as the section header.
    /// </summary>
    public virtual required string SectionName { get; init; }

    /// <summary>
    /// When true, the section header row is not rendered.
    /// </summary>
    public bool HideHeader { get; init; }

    /// <summary>
    /// When true, the entire section is hidden if it contains no items.
    /// </summary>
    public bool HideWhenEmpty { get; init; } = true;
}