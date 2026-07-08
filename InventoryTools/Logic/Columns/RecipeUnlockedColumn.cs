using System.Linq;
using AllaganLib.GameSheets.ItemSources;
using CriticalCommonLib.Crafting;
using CriticalCommonLib.Models;
using InventoryTools.Logic.Columns.Abstract;
using InventoryTools.Services;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Logic.Columns;

public class RecipeUnlockedColumn : CheckboxColumn
{
    private readonly IItemObtainabilityService _obtainabilityService;

    public RecipeUnlockedColumn(ILogger<RecipeUnlockedColumn> logger, ImGuiService imGuiService, IItemObtainabilityService obtainabilityService)
        : base(logger, imGuiService)
    {
        _obtainabilityService = obtainabilityService;
    }

    public override ColumnCategory ColumnCategory => ColumnCategory.Basic;

    public override bool? CurrentValue(ColumnConfiguration columnConfiguration, SearchResult searchResult)
    {
        var recipe = searchResult.Item.Sources.OfType<ItemCraftResultSource>().FirstOrDefault()?.Recipe;
        if (recipe == null) return null;

        var requirements = _obtainabilityService.GetRequirements(searchResult.Item, IngredientPreferenceType.Crafting, recipe);
        if (requirements.Count == 0) return null;

        return requirements.All(r => r.IsMet);
    }

    public override string Name { get; set; } = "Is Recipe Unlocked?";
    public override string RenderName => "Recipe Unlocked?";
    public override float Width { get; set; } = 140.0f;
    public override string HelpText { get; set; } = "Shows whether all requirements to craft this item (job level, mastery book, specialization) are met by the current character.";
    public override bool HasFilter { get; set; } = true;
    public override ColumnFilterType FilterType { get; set; } = ColumnFilterType.Boolean;
    public override FilterType DefaultIn => Logic.FilterType.GameItemFilter;
}
