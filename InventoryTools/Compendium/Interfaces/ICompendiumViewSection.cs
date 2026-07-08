using InventoryTools.Compendium.Models;
using InventoryTools.Compendium.Sections.Options;

namespace InventoryTools.Compendium.Interfaces;

public interface ICompendiumViewSection
{
    public SectionOptions SectionOptions { get; }
    public void Draw(SectionState sectionState);
    public bool ShouldDraw(SectionState sectionState);
    public bool IsEmpty(SectionState sectionState);
}