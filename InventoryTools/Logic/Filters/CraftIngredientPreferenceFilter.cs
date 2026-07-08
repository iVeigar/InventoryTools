using System.Collections.Generic;
using System.Linq;
using AllaganLib.GameSheets.Sheets;
using AllaganLib.GameSheets.Sheets.Rows;
using AllaganLib.Shared.Extensions;
using CriticalCommonLib.Crafting;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.Models;
using Dalamud.Bindings.ImGui;
using InventoryTools.Logic.Filters.Abstract;
using Dalamud.Interface.Utility.Raii;
using InventoryTools.Extensions;
using InventoryTools.Services;
using InventoryTools.Ui;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Logic.Filters;

public class CraftIngredientPreferenceFilter : SortedListFilter<(IngredientPreferenceType, uint?), (IngredientPreferenceType, uint?)>
{
    private readonly ItemSheet _itemSheet;
    private readonly PopupService _popupService;

    public CraftIngredientPreferenceFilter(ILogger<CraftIngredientPreferenceFilter> logger, ImGuiService imGuiService, ItemSheet itemSheet, PopupService popupService) : base(logger, imGuiService)
    {
        _itemSheet = itemSheet;
        _popupService = popupService;
    }

    public override Dictionary<(IngredientPreferenceType, uint?), (string, string?)> CurrentValue(FilterConfiguration configuration)
    {
        (string, string?) GetIngredientPreferenceDetails((IngredientPreferenceType, uint?) c)
        {
            var itemName = "";
            if (c.Item2 != null)
            {
                var itemRow = _itemSheet.GetRowOrDefault((uint)c.Item2);
                if (itemRow != null)
                {
                    itemName = " (" + itemRow.NameString + ")";
                }
                else
                {
                    itemName = " (Unknown Item)";
                }
            }
            return (c.Item1.FormattedName() + itemName, null);
        }

        return configuration.CraftList.IngredientPreferenceTypeOrder.Distinct().ToDictionary(c => c, GetIngredientPreferenceDetails);
    }

    public override void ResetFilter(FilterConfiguration configuration)
    {
        configuration.CraftList.ResetIngredientPreferences();
        configuration.NotifyConfigurationChange();
    }

    public override void UpdateFilterConfiguration(FilterConfiguration configuration, Dictionary<(IngredientPreferenceType, uint?), (string, string?)> newValue)
    {
        configuration.CraftList.IngredientPreferenceTypeOrder = newValue.Select(c => c.Key).ToList();
        configuration.NotifyConfigurationChange();
    }

    public override string Key { get; set; } = "CraftIngredientPreference";
    public override string Name { get; set; } = "Default Ingredient Sourcing";

    public override string HelpText { get; set; } =
        "When generating the materials for a craft, the 'Ingredient Sourcing' setting determines the preferred method of acquisition. The craft list will refer to this sorted list to determine the appropriate method. Please note that this assumes the item in the craft list can be obtained through this method. If not, the next item in the ingredient sourcing list will be considered.";

    public override FilterCategory FilterCategory { get; set; } = FilterCategory.IngredientSourcing;
    public override Dictionary<(IngredientPreferenceType, uint?), (string, string?)> DefaultValue { get; set; } = new();

    public override bool HasValueSet(FilterConfiguration configuration)
    {
        return false;
    }

    public override FilterType AvailableIn { get; set; } = FilterType.CraftFilter;
    public override bool? FilterItem(FilterConfiguration configuration, InventoryItem item)
    {
        return null;
    }

    public override bool? FilterItem(FilterConfiguration configuration, ItemRow item)
    {
        return null;
    }

    public override bool CanRemove { get; set; } = true;
    public override bool CanRemoveItem(FilterConfiguration configuration, (IngredientPreferenceType, uint?) item)
    {
        return true;
    }

    public override (IngredientPreferenceType, uint?) GetItem(FilterConfiguration configuration, (IngredientPreferenceType, uint?) item)
    {
        return item;
    }

    private List<IngredientPreferenceType> _preferenceTypes = new List<IngredientPreferenceType>()
    {
        IngredientPreferenceType.Botany,
        IngredientPreferenceType.Buy,
        IngredientPreferenceType.Crafting,
        IngredientPreferenceType.Desynthesis,
        IngredientPreferenceType.Fishing,
        IngredientPreferenceType.SpearFishing,
        IngredientPreferenceType.Gardening,
        IngredientPreferenceType.Marketboard,
        IngredientPreferenceType.Mining,
        IngredientPreferenceType.Mobs,
        IngredientPreferenceType.Reduction,
        IngredientPreferenceType.Venture,
        IngredientPreferenceType.Duty,
        IngredientPreferenceType.ExplorationVenture,
        IngredientPreferenceType.ResourceInspection,
        IngredientPreferenceType.HouseVendor,
        IngredientPreferenceType.Empty,
    };

    private (IngredientPreferenceType, uint?)? _draggedItem = null;

    public void AddItem(FilterConfiguration configuration, IngredientPreferenceType type, uint? itemId = null)
    {
        var value = CurrentValue(configuration);
        value.Add((type, itemId), ("", null));
        UpdateFilterConfiguration(configuration, value);
    }

    public override void DrawTable(FilterConfiguration configuration)
    {
        var value = CurrentValue(configuration);
        var entries = value.ToList();

        using var table = ImRaii.Table(Key + "ColumnEditTable", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.SizingStretchSame);
        if (!table.Success) return;

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 20);
        ImGui.TableSetupColumn("Preference", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 24);

        (IngredientPreferenceType, uint?)? toRemove = null;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var itemKey = entry.Key;

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Button("=##DragHandle" + i);
            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text("Click and drag to reorder");
                }
            }

            using (var source = ImRaii.DragDropSource())
            {
                if (source)
                {
                    _draggedItem = itemKey;
                    ImGui.SetDragDropPayload("##IngredientPrefReorder", []);
                    ImGui.TextUnformatted("Moving: " +entry.Value.Item1);
                }
            }

            using (var target = ImRaii.DragDropTarget())
            {
                if (target)
                {
                    if (OtterGui.ImGuiUtil.IsDropping("##IngredientPrefReorder") &&
                        _draggedItem != null && !_draggedItem.Value.Equals(itemKey))
                    {
                        var sourceIdx = entries.FindIndex(c => c.Key.Equals(_draggedItem!.Value));
                        if (sourceIdx >= 0)
                        {
                            var delta = i - sourceIdx;
                            if (delta > 0)
                            {
                                for (var d = 0; d < delta; d++)
                                {
                                    MoveItemDown(configuration, _draggedItem!.Value);
                                }
                            }
                            else
                            {
                                for (var d = 0; d < -delta; d++)
                                {
                                    MoveItemUp(configuration, _draggedItem!.Value);
                                }
                            }
                        }
                        _draggedItem = null;
                    }
                }
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Value.Item1);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("X##Remove" + i))
            {
                toRemove = itemKey;
            }
        }

        if (toRemove != null)
        {
            RemoveItem(configuration, toRemove.Value);
        }
    }

    public override void Draw(FilterConfiguration configuration)
    {
        ImGui.TextUnformatted(GetName(configuration));
        ImGui.SameLine();
        ImGuiService.HelpMarker(GetHelpText(configuration));
        ImGui.Separator();

        var currentValue = CurrentValue(configuration);

        ImGui.TextUnformatted("Add Preference:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(LabelSize);
        using (var combo = ImRaii.Combo("##Add" + Key, "", ImGuiComboFlags.HeightLarge))
        {
            if (combo.Success)
            {
                foreach (var preferenceType in _preferenceTypes.Where(c => !currentValue.ContainsKey((c, null))))
                {
                    if (ImGui.Selectable(preferenceType.FormattedName()))
                    {
                        AddItem(configuration, preferenceType);
                    }
                }
            }
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("Add Item Preference:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(LabelSize);
        using (var combo = ImRaii.Combo("##AddItem" + Key, "", ImGuiComboFlags.HeightLarge))
        {
            if (combo.Success)
            {
                var searchString = SearchString;
                ImGui.InputText("##ItemSearch", ref searchString, 50);
                if (_searchString != searchString)
                {
                    SearchString = searchString;
                }

                ImGui.Separator();
                if (_searchString == "")
                {
                    ImGui.TextUnformatted("Start typing to search...");
                }

                foreach (var item in SearchItems.Where(c => !currentValue.ContainsKey((IngredientPreferenceType.Item, c.RowId))))
                {
                    if (ImGui.Selectable(item.NameString))
                    {
                        AddItem(configuration, IngredientPreferenceType.Item, item.RowId);
                    }
                }
            }
        }

        var resetWidth = ImGui.CalcTextSize("Reset to Default").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - resetWidth);
        if (ImGui.Button("Reset to Default##IngredientPref"))
        {
            _popupService.AddPopup(new ConfirmPopup(typeof(CraftsWindow), "resetIngredientPref",
                "Are you sure you want to reset the ingredient sourcing order to default?",
                confirmed =>
                {
                    if (confirmed)
                    {
                        ResetFilter(configuration);
                    }
                }));
        }

        ImGui.Separator();
        DrawTable(configuration);
    }

    private string _searchString = "";
    private List<ItemRow>? _searchItems = null;
    public List<ItemRow> SearchItems
    {
        get
        {
            if (SearchString == "")
            {
                _searchItems = new List<ItemRow>();
                return _searchItems;
            }
            if (_searchItems == null)
            {
                _searchItems = _itemSheet.Where(c => c.NameString.ToLower().PassesFilter(SearchString.ToLower())).Take(100)
                    .Select(c => _itemSheet.GetRow(c.RowId)).ToList();
            }

            return _searchItems;
        }
    }

    public string SearchString
    {
        get => _searchString;
        set
        {
            _searchString = value;
            _searchItems = null;
        }
    }
}