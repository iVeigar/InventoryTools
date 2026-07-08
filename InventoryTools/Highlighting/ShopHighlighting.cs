using System;
using System.Collections.Generic;
using System.Linq;
using AllaganLib.GameSheets.Sheets;
using AllaganLib.GameSheets.Sheets.Rows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using InventoryTools.Logic.Settings;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace InventoryTools.Highlighting;

public class ShopHighlighting : IDisposable
{
    private readonly IGameGui _gameGui;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IPluginLog _pluginLog;
    private readonly ShopHighlightingDisableItemsSetting _shopHighlightingDisableItemsSetting;
    private readonly ShopHighlightingNpcSetting _shopHighlightingNpcSetting;
    private readonly ShopHighlightingNpcColorSetting _shopHighlightingNpcColorSetting;
    private readonly ShopHighlightingNpcNameplateIconSetting _shopHighlightingNpcNameplateIconSetting;
    private readonly InventoryToolsConfiguration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly INamePlateGui _namePlateGui;
    private readonly ItemSheet _itemSheet;
    private readonly uint _shopItemsAtkIndex = 441;
    private readonly uint _shopCountAtkIndex = 2;
    private HashSet<uint> _highlightedItems = new HashSet<uint>();
    private Dictionary<int, uint>? _itemIndexMap = null;
    private HashSet<uint> _highlightedNpcBaseIds = new();
    private HashSet<ulong> _currentlyHighlightedObjectIds = new();


    public ShopHighlighting(IGameGui gameGui,
        IAddonLifecycle addonLifecycle,
        IPluginLog pluginLog,
        ShopHighlightingDisableItemsSetting shopHighlightingDisableItemsSetting,
        ShopHighlightingNpcSetting shopHighlightingNpcSetting,
        ShopHighlightingNpcColorSetting shopHighlightingNpcColorSetting,
        ShopHighlightingNpcNameplateIconSetting shopHighlightingNpcNameplateIconSetting,
        InventoryToolsConfiguration configuration,
        IObjectTable objectTable,
        IFramework framework,
        INamePlateGui namePlateGui,
        ItemSheet itemSheet)
    {
        this._gameGui = gameGui;
        this._addonLifecycle = addonLifecycle;
        this._pluginLog = pluginLog;
        _shopHighlightingDisableItemsSetting = shopHighlightingDisableItemsSetting;
        _shopHighlightingNpcSetting = shopHighlightingNpcSetting;
        _shopHighlightingNpcColorSetting = shopHighlightingNpcColorSetting;
        _shopHighlightingNpcNameplateIconSetting = shopHighlightingNpcNameplateIconSetting;
        _configuration = configuration;
        _objectTable = objectTable;
        _framework = framework;
        _namePlateGui = namePlateGui;
        _itemSheet = itemSheet;
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Shop", AddonSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostDraw, "Shop", AddonPostDraw);
        _framework.Update += OnFrameworkUpdate;
        _namePlateGui.OnDataUpdate += OnNamePlateUpdate;
    }

    public void AddItem(uint itemId)
    {
        _highlightedItems.Add(itemId);
        RefreshHighlightedNpcs();
    }

    public void RemoveItem(uint itemId)
    {
        _highlightedItems.Remove(itemId);
        RefreshHighlightedNpcs();
    }

    public void SetItems(List<uint> items)
    {
        _highlightedItems = [..items];
        RefreshHighlightedNpcs();
    }

    public void SetItems(HashSet<uint> items)
    {
        _highlightedItems = items;
        RefreshHighlightedNpcs();
    }

    public void ClearItems()
    {
        _highlightedItems.Clear();
        RefreshHighlightedNpcs();
    }

    public List<ENpcBaseRow> GetRelatedNpcs(uint itemId)
    {
        if (itemId == 0)
        {
            return [];
        }

        var itemRow = _itemSheet.GetRowOrDefault(itemId);
        if (itemRow == null)
        {
            return [];
        }

        return itemRow.AllShopSources.SelectMany(c => c.Shop.ENpcs).DistinctBy(c => c.RowId).ToList();
    }

    public List<ENpcBaseRow> GetRelatedNpcs(List<uint> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var itemRows = itemIds.Select(c => _itemSheet.GetRowOrDefault(c)).Where(c => c != null!);

        return itemRows.SelectMany(c => c!.AllShopSources.SelectMany(d => d.Shop.ENpcs)).DistinctBy(d => d.RowId).ToList();
    }

    private void RefreshHighlightedNpcs()
    {
        _highlightedNpcBaseIds = _highlightedItems.Count > 0
            ? GetRelatedNpcs(_highlightedItems.ToList()).Select(n => n.RowId).ToHashSet()
            : new HashSet<uint>();
        _namePlateGui.RequestRedraw();
    }

    private string itemIdString = "";
    private uint itemId;

    public unsafe void DrawDebug()
    {
        var addon = _gameGui.GetAddonByName("Shop");
        if (addon != IntPtr.Zero)
        {
            var atkUnitBase = (AtkUnitBase*)addon.Address;
            var atkComponentBase = atkUnitBase->GetComponentByNodeId(16);
            if (atkComponentBase != null)
            {
                var listNode = (AtkComponentList*)atkComponentBase;
                var listItemIndex = listNode->ItemRendererList->AtkComponentListItemRenderer->ListItemIndex;
                ImGui.TextUnformatted($"List Item Index: {listItemIndex}");
                if (_itemIndexMap != null)
                {
                    foreach (var item in _itemIndexMap)
                    {
                        ImGui.TextUnformatted(item.Key + ": " + item.Value);
                    }
                }

                if (ImGui.InputText("Item", ref itemIdString, 128))
                {
                    if (uint.TryParse(itemIdString, out itemId))
                    {
                        itemId = itemId;
                    }
                    itemIdString = itemId.ToString();
                }
                if (ImGui.Button("Add Item"))
                {
                    if (uint.TryParse(itemIdString, out itemId))
                    {
                        _highlightedItems.Add(itemId);
                    }
                }
                if (ImGui.Button("Remove Item"))
                {
                    if (uint.TryParse(itemIdString, out itemId))
                    {
                        _highlightedItems.Remove(itemId);
                    }
                }
            }
        }
    }


    private unsafe void AddonPostDraw(AddonEvent type, AddonArgs args)
    {
        if (args.Addon != IntPtr.Zero)
        {
            var atkUnitBase = (AtkUnitBase*)args.Addon.Address;
            var atkComponentBase = atkUnitBase->GetComponentByNodeId(16);
            if (atkComponentBase != null)
            {
                var listNode = (AtkComponentList*)atkComponentBase;
                if (this._itemIndexMap == null)
                {
                    CalculateItemIndexMap(atkUnitBase);
                }

                for (int i = 0; i < listNode->ListLength; i++)
                {
                    if (!_highlightedItems.Any())
                    {
                        if (_shopHighlightingDisableItemsSetting.CurrentValue(_configuration))
                        {
                            listNode->SetItemDisabledState(i, false);
                        }
                        listNode->SetItemHighlightedState(i, false);
                    }
                    else if (_itemIndexMap!.ContainsKey(i))
                    {
                        if (_highlightedItems.Contains(_itemIndexMap[i]))
                        {
                            if (!listNode->GetItemHighlightedState(i))
                            {
                                listNode->SetItemHighlightedState(i, true);
                            }

                            if (_shopHighlightingDisableItemsSetting.CurrentValue(_configuration))
                            {
                                if (listNode->GetItemDisabledState(i))
                                {
                                    listNode->SetItemDisabledState(i, false);
                                }
                            }
                        }
                        else
                        {
                            if (_shopHighlightingDisableItemsSetting.CurrentValue(_configuration))
                            {
                                if (!listNode->GetItemDisabledState(i))
                                {
                                    listNode->SetItemDisabledState(i, true);
                                }
                            }

                            if (listNode->GetItemHighlightedState(i))
                            {
                                listNode->SetItemHighlightedState(i, false);
                            }
                        }
                    }
                }
            }
        }
    }

    private unsafe void CalculateItemIndexMap(AtkUnitBase* atkUnitBase)
    {
        var itemIndexMap = new Dictionary<int, uint>();
        var shopLength = atkUnitBase->AtkValues[_shopCountAtkIndex].UInt;
        for (var i = _shopItemsAtkIndex; i < _shopItemsAtkIndex + shopLength; i++)
        {
            var atkValue = atkUnitBase->AtkValues[i];
            if (atkValue.Type != AtkValueType.UInt)
            {
                break;
            }

            itemIndexMap[(int)(i - _shopItemsAtkIndex)] = atkValue.UInt;
        }
        this._itemIndexMap = itemIndexMap;
    }

    private unsafe void AddonSetup(AddonEvent type, AddonArgs args)
    {
        if (args.Addon != IntPtr.Zero)
        {
            var atkUnitBase = (AtkUnitBase*)args.Addon.Address;
            CalculateItemIndexMap(atkUnitBase);
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework fw)
    {
        if (!_shopHighlightingNpcSetting.CurrentValue(_configuration) || _highlightedNpcBaseIds.Count == 0)
        {
            ClearNpcHighlights();
            return;
        }

        var color = _shopHighlightingNpcColorSetting.CurrentValue(_configuration);
        var nowHighlighted = new HashSet<ulong>();

        foreach (var obj in _objectTable)
        {
            var address = obj.Address;
            if (address == nint.Zero)
            {
                continue;
            }
            if (_highlightedNpcBaseIds.Contains(obj.BaseId))
            {
                ((GameObject*)address)->Highlight(color);
                nowHighlighted.Add(obj.GameObjectId);
            }
            else if (_currentlyHighlightedObjectIds.Contains(obj.GameObjectId))
            {
                ((GameObject*)address)->Highlight(ObjectHighlightColor.None);
            }
        }

        _currentlyHighlightedObjectIds = nowHighlighted;
    }

    private unsafe void ClearNpcHighlights()
    {
        if (!_framework.IsInFrameworkUpdateThread)
        {
            _framework.RunOnFrameworkThread(ClearNpcHighlights);
            return;
        }
        if (_currentlyHighlightedObjectIds.Count == 0) return;
        foreach (var obj in _objectTable)
        {
            if (_currentlyHighlightedObjectIds.Contains(obj.GameObjectId))
            {
                var address = obj.Address;
                if (address != nint.Zero)
                {
                    ((GameObject*)address)->Highlight(ObjectHighlightColor.None);
                }
            }
        }
        _currentlyHighlightedObjectIds.Clear();
        _highlightedNpcBaseIds.Clear();
    }

    private unsafe void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!_shopHighlightingNpcNameplateIconSetting.CurrentValue(_configuration) || _highlightedNpcBaseIds.Count == 0)
            return;

        var baseIds = _highlightedNpcBaseIds;
        foreach (var handler in handlers)
        {
            if (handler.NamePlateKind == NamePlateKind.EventNpcCompanion)
            {
                var baseId = handler.GameObject?.BaseId;
                if (baseId.HasValue && baseIds.Contains(baseId.Value))
                {
                    handler.MarkerIconId = 60094;
                }
            }
        }
    }

    public void Dispose()
    {
        _namePlateGui.OnDataUpdate -= OnNamePlateUpdate;
        _framework.Update -= OnFrameworkUpdate;
        if (!_framework.IsFrameworkUnloading)
        {
            ClearNpcHighlights();
        }

        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Shop", AddonSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "Shop", AddonPostDraw);
    }
}