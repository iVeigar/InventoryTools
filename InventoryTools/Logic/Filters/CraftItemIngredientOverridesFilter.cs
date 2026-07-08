using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AllaganLib.GameSheets.Sheets;
using AllaganLib.GameSheets.Sheets.Rows;
using AllaganLib.Shared.Extensions;
using CriticalCommonLib.Crafting;
using CriticalCommonLib.Models;
using DalaMock.Shared.Interfaces;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using InventoryTools.Extensions;
using InventoryTools.Localizers;
using InventoryTools.Logic.Filters.Abstract;
using InventoryTools.Services;
using InventoryTools.Ui;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Logic.Filters;

public class CraftItemIngredientOverridesFilter : Filter<bool>
{
    private readonly ItemSheet _itemSheet;
    private readonly CraftingCache _craftingCache;
    private readonly IngredientPreferenceLocalizer _localizer;
    private readonly IFont _font;
    private readonly PopupService _popupService;

    private string _itemSearchString = "";
    private List<ItemRow>? _itemSearchCache = null;
    private uint? _selectedItemId = null;
    private IngredientPreference? _selectedPreference = null;
    private uint? _editingItemId = null;

    public CraftItemIngredientOverridesFilter(
        ILogger<CraftItemIngredientOverridesFilter> logger,
        ImGuiService imGuiService,
        ItemSheet itemSheet,
        CraftingCache craftingCache,
        IngredientPreferenceLocalizer localizer,
        IFont font,
        PopupService popupService) : base(logger, imGuiService)
    {
        _itemSheet = itemSheet;
        _craftingCache = craftingCache;
        _localizer = localizer;
        _font = font;
        _popupService = popupService;
    }

    public override string Key { get; set; } = "CraftItemIngredientOverrides";
    public override string Name { get; set; } = "Per-Item Ingredient Source Overrides";
    public override string HelpText { get; set; } = "Override the ingredient source for specific items. Overrides set here take precedence over the list's default ingredient sourcing order.";
    public override FilterCategory FilterCategory { get; set; } = FilterCategory.ItemIngredientOverrides;
    public override FilterType AvailableIn { get; set; } = FilterType.CraftFilter;
    public override bool DefaultValue { get; set; } = false;
    public override bool ShowReset { get; set; } = false;

    public override bool CurrentValue(FilterConfiguration configuration) => false;
    public override void UpdateFilterConfiguration(FilterConfiguration configuration, bool newValue) { }
    public override void ResetFilter(FilterConfiguration configuration) { }
    public override bool HasValueSet(FilterConfiguration configuration) => configuration.CraftList.IngredientPreferences.Count != 0;
    public override bool? FilterItem(FilterConfiguration configuration, InventoryItem item) => null;
    public override bool? FilterItem(FilterConfiguration configuration, ItemRow item) => null;

    private string ItemSearchString
    {
        get => _itemSearchString;
        set
        {
            if (_itemSearchString == value)
            {
                return;
            }
            _itemSearchString = value;
            _itemSearchCache = null;
        }
    }

    private List<ItemRow> SearchItems
    {
        get
        {
            if (_itemSearchString == "")
            {
                return _itemSearchCache ??= new List<ItemRow>();
            }
            return _itemSearchCache ??= _itemSheet
                .Where(c => c.NameString.ToLower().PassesFilter(_itemSearchString.ToLower()))
                .Take(100)
                .Select(c => _itemSheet.GetRow(c.RowId))
                .ToList();
        }
    }

    public override void Draw(FilterConfiguration configuration)
    {
        ImGui.TextUnformatted(Name);
        ImGui.SameLine();
        ImGuiService.HelpMarker(HelpText);
        ImGui.Separator();

        ImGui.TextUnformatted("Add Override:");
        ImGui.SameLine();

        var selectedItemName = _selectedItemId != null
            ? _itemSheet.GetRowOrDefault(_selectedItemId.Value)?.NameString ?? "Unknown"
            : "Select Item...";

        ImGui.SetNextItemWidth(LabelSize);
        using (var itemCombo = ImRaii.Combo("##ItemOverrideItemSearch", selectedItemName, ImGuiComboFlags.HeightLarge))
        {
            if (itemCombo.Success)
            {
                var search = _itemSearchString;
                ImGui.InputText("##ItemOverrideSearch", ref search, 100);
                if (search != _itemSearchString)
                {
                    ItemSearchString = search;
                }

                ImGui.Separator();
                if (_itemSearchString == "")
                {
                    ImGui.TextUnformatted("Start typing to search...");
                }
                foreach (var item in SearchItems)
                {
                    if (ImGui.Selectable(item.NameString, _selectedItemId == item.RowId))
                    {
                        _selectedItemId = item.RowId;
                        _selectedPreference = null;
                    }
                }
            }
        }

        ImGui.SameLine();

        var previewSource = _selectedPreference != null
            ? _localizer.FormattedName(_selectedPreference)
            : "Select Source...";

        using (ImRaii.Disabled(_selectedItemId == null))
        {
            ImGui.SetNextItemWidth(LabelSize);
            using (var sourceCombo = ImRaii.Combo("##ItemOverrideSourcePref", previewSource, ImGuiComboFlags.HeightLarge))
            {
                if (sourceCombo.Success && _selectedItemId != null)
                {
                    var preferences = _craftingCache.GetIngredientPreferences(_selectedItemId.Value);
                    foreach (var pref in preferences)
                    {
                        var prefName = _localizer.FormattedName(pref);
                        var isSelected = _selectedPreference != null &&
                            _selectedPreference.Type == pref.Type &&
                            _selectedPreference.LinkedItemId == pref.LinkedItemId;
                        if (ImGui.Selectable(prefName, isSelected))
                        {
                            _selectedPreference = pref;
                        }
                    }
                }
            }
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(_selectedItemId == null || _selectedPreference == null))
        {
            if (ImGui.Button("Add##ItemOverride"))
            {
                configuration.CraftList.UpdateIngredientPreference(_selectedItemId!.Value, _selectedPreference);
                configuration.NeedsRefresh = true;
                configuration.NotifyConfigurationChange();
                _selectedItemId = null;
                _selectedPreference = null;
                ItemSearchString = "";
            }
        }

        if (configuration.CraftList.IngredientPreferences.Count > 0)
        {
            var clearButtonWidth = ImGui.CalcTextSize("Clear All").X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetContentRegionMax().X - clearButtonWidth);
            if (ImGui.Button("Clear All##ItemOverrides"))
            {
                _popupService.AddPopup(new ConfirmPopup(typeof(CraftsWindow), "clearAllItemOverrides",
                    "Are you sure you want to clear all per-item overrides?",
                    confirmed =>
                    {
                        if (!confirmed)
                        {
                            return;
                        }
                        foreach (var itemId in configuration.CraftList.IngredientPreferences.Keys.ToList())
                        {
                            configuration.CraftList.UpdateIngredientPreference(itemId, null);
                        }
                        configuration.NeedsRefresh = true;
                        configuration.NotifyConfigurationChange();
                        _editingItemId = null;
                    }));
            }
        }

        ImGui.Separator();

        var overrides = configuration.CraftList.IngredientPreferences;
        if (overrides.Count == 0)
        {
            ImGui.TextUnformatted("No per-item overrides set.");
            return;
        }

        uint? toRemove = null;
        using (var table = ImRaii.Table("##ItemOverridesTable", 3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            if (table.Success)
            {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Source Preference", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 24);
            ImGui.TableHeadersRow();

            foreach (var (itemId, preference) in overrides)
            {
                var itemRow = _itemSheet.GetRowOrDefault(itemId);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (itemRow != null)
                {
                    ImGui.Image(
                        ImGuiService.GetIconTexture(itemRow.Icon).Handle,
                        new Vector2(16, 16) * ImGui.GetIO().FontGlobalScale);
                    ImGui.SameLine();
                    ImGui.TextUnformatted(itemRow.NameString);
                }
                else
                {
                    ImGui.TextUnformatted("Unknown Item (" + itemId + ")");
                }

                ImGui.TableNextColumn();
                if (_editingItemId == itemId)
                {
                    ImGui.SetNextItemWidth(-1);
                    using (var editCombo = ImRaii.Combo("##EditSourcePref" + itemId, _localizer.FormattedName(preference), ImGuiComboFlags.HeightLarge))
                    {
                        if (editCombo.Success)
                        {
                            var preferences = _craftingCache.GetIngredientPreferences(itemId);
                            foreach (var pref in preferences)
                            {
                                var isSelected = pref.Type == preference.Type && pref.LinkedItemId == preference.LinkedItemId;
                                if (ImGui.Selectable(_localizer.FormattedName(pref), isSelected))
                                {
                                    configuration.CraftList.UpdateIngredientPreference(itemId, pref);
                                    configuration.NeedsRefresh = true;
                                    configuration.NotifyConfigurationChange();
                                    _editingItemId = null;
                                }
                            }
                        }
                    }
                }
                else
                {
                    ImGui.TextUnformatted(_localizer.FormattedName(preference));
                    ImGui.SameLine();
                    using (ImRaii.PushFont(_font.IconFont))
                    {
                        if (ImGui.SmallButton(FontAwesomeIcon.Edit.ToIconString() + "##Edit" + itemId))
                        {
                            _editingItemId = itemId;
                        }
                    }
                }

                ImGui.TableNextColumn();
                using (ImRaii.PushFont(_font.IconFont))
                {
                    if (ImGui.SmallButton(FontAwesomeIcon.Times.ToIconString() + "##Remove" + itemId))
                    {
                        toRemove = itemId;
                    }
                }
            }

            }
        }

        if (toRemove != null)
        {
            configuration.CraftList.UpdateIngredientPreference(toRemove.Value, null);
            configuration.NeedsRefresh = true;
            configuration.NotifyConfigurationChange();
            if (_editingItemId == toRemove)
            {
                _editingItemId = null;
            }
        }
    }

    public override void InvalidateSearchCache()
    {
        _itemSearchCache = null;
    }
}
