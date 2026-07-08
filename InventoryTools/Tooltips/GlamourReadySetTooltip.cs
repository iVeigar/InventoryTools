using System.Collections.Generic;
using System.Linq;
using AllaganLib.GameSheets.Caches;
using AllaganLib.GameSheets.ItemSources;
using AllaganLib.GameSheets.Sheets;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Extensions;
using CriticalCommonLib.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InventoryTools.Logic.Editors;
using InventoryTools.Logic.Settings;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Tooltips;

public class GlamourReadySetTooltip : BaseTooltip
{
    private readonly TooltipGlamourReadySetColorSetting _colorSetting;
    private readonly TooltipDisplayGlamourReadySetSetting _displaySetting;
    private readonly TooltipGlamourReadySetDisplayModeSetting _displayModeSetting;
    private readonly TooltipGlamourReadySetAcquiredColorSetting _acquiredColorSetting;
    private readonly TooltipGlamourReadySetNotAcquiredColorSetting _notAcquiredColorSetting;
    private readonly TooltipGlamourReadySetScopeSetting _scopeSetting;
    private readonly IInventoryMonitor _inventoryMonitor;
    private readonly InventoryScopeCalculator _inventoryScopeCalculator;

    public GlamourReadySetTooltip(ILogger<GlamourReadySetTooltip> logger,
        TooltipGlamourReadySetColorSetting colorSetting, TooltipDisplayGlamourReadySetSetting displaySetting,
        TooltipGlamourReadySetDisplayModeSetting displayModeSetting,
        TooltipGlamourReadySetAcquiredColorSetting acquiredColorSetting,
        TooltipGlamourReadySetNotAcquiredColorSetting notAcquiredColorSetting,
        TooltipGlamourReadySetScopeSetting scopeSetting,
        IInventoryMonitor inventoryMonitor,
        InventoryScopeCalculator inventoryScopeCalculator,
        ItemSheet itemSheet, InventoryToolsConfiguration configuration, IGameGui gameGui, IChatGui chatGui) : base(6909, logger, itemSheet, configuration, gameGui, chatGui)
    {
        _colorSetting = colorSetting;
        _displaySetting = displaySetting;
        _displayModeSetting = displayModeSetting;
        _acquiredColorSetting = acquiredColorSetting;
        _notAcquiredColorSetting = notAcquiredColorSetting;
        _scopeSetting = scopeSetting;
        _inventoryMonitor = inventoryMonitor;
        _inventoryScopeCalculator = inventoryScopeCalculator;
    }

    public override bool IsEnabled => Configuration.DisplayTooltip && _displaySetting.CurrentValue(Configuration);

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        if (!ShouldShow()) return;
        var item = HoverItem;
        if (item == null) return;

        var isSet = item.HasUsesByType(ItemInfoType.GlamourReadySet);
        var isSetItem = item.HasUsesByType(ItemInfoType.GlamourReadySetItem);
        if (!isSet && !isSetItem) return;

        TooltipService.ItemTooltipField itemTooltipField = TooltipService.ItemTooltipField.ItemDescription;
        SeString? seStr = null;
        if (GetTooltipVisibility(ItemTooltipFieldVisibility.Description))
        {
            itemTooltipField = TooltipService.ItemTooltipField.ItemDescription;
            seStr = GetTooltipString(stringArrayData, itemTooltipField);
        }

        if (seStr == null && GetTooltipVisibility(ItemTooltipFieldVisibility.Effects))
        {
            itemTooltipField = TooltipService.ItemTooltipField.Effects;
            seStr = GetTooltipString(stringArrayData, itemTooltipField);
        }

        if (seStr == null && GetTooltipVisibility(ItemTooltipFieldVisibility.Levels))
        {
            itemTooltipField = TooltipService.ItemTooltipField.Levels;
            seStr = GetTooltipString(stringArrayData, itemTooltipField);
        }

        if (seStr == null) return;

        if (seStr.Payloads.Any(payload =>
                payload is DalamudLinkPayload linkPayload && linkPayload.CommandId == TooltipIdentifier))
        {
            return;
        }
        seStr.Payloads.Add(GetLinkPayload());
        seStr.Payloads.Add(RawPayload.LinkTerminator);

        var scope = _scopeSetting.CurrentValue(Configuration);
        var allItems = _inventoryMonitor.AllItems;
        if (scope != null && scope.Count != 0)
        {
            allItems = allItems.Where(c => _inventoryScopeCalculator.Filter(scope, c));
        }
        var allItemsList = allItems.ToList();

        bool IsOwned(uint itemId) => allItemsList.Any(i => i.ItemId == itemId);

        string BuildOwnershipLine(uint itemId)
        {
            var categories = allItemsList
                .Where(i => i.ItemId == itemId)
                .Select(i => i.SortedCategory)
                .Distinct();
            return string.Concat(categories.Select(c => $"Already in {c.FormattedDetailedName()}\n"));
        }

        var newText = "";
        var mode = _displayModeSetting.CurrentValue(Configuration);
        var baseColor = (ushort)(_colorSetting.CurrentValue(Configuration) ?? Configuration.TooltipColor ?? 1);
        List<Payload>? detailedPayloads = null;

        if (isSet)
        {
            var source = item.GetUsesByType<ItemGlamourReadySetSource>(ItemInfoType.GlamourReadySet).FirstOrDefault();
            if (source != null)
            {
                var ownershipLine = BuildOwnershipLine(HoverItemId);
                if (mode == GlamourReadySetDisplayMode.Compact)
                {
                    var ownedCount = 0;
                    var visualizer = "";
                    foreach (var c in source.SetItems)
                    {
                        if (IsOwned(c.RowId))
                        {
                            visualizer += "X";
                            ownedCount++;
                        }
                        else
                        {
                            visualizer += "O";
                        }
                    }

                    newText += $"\nOutfit Glamour: {ownedCount}/{source.SetItems.Count} {visualizer}\n";
                    newText += ownershipLine;
                }
                else
                {
                    var acquiredColor = (ushort)(_acquiredColorSetting.CurrentValue(Configuration) ?? baseColor);
                    var notAcquiredColor = (ushort)(_notAcquiredColorSetting.CurrentValue(Configuration) ?? baseColor);
                    detailedPayloads = new List<Payload>();
                    var header = "\nOutfit Glamour\n" + ownershipLine;
                    detailedPayloads.Add(new UIForegroundPayload(baseColor));
                    detailedPayloads.Add(new UIGlowPayload(0));
                    detailedPayloads.Add(new TextPayload(header.TrimEnd('\n')));
                    detailedPayloads.Add(new UIGlowPayload(0));
                    detailedPayloads.Add(new UIForegroundPayload(0));
                    foreach (var component in source.SetItems)
                    {
                        var owned = IsOwned(component.RowId);
                        detailedPayloads.Add(new UIForegroundPayload(owned ? acquiredColor : notAcquiredColor));
                        detailedPayloads.Add(new UIGlowPayload(0));
                        detailedPayloads.Add(new TextPayload("\n" + (owned ? SeIconChar.Glamoured.ToIconString() : SeIconChar.Cross.ToIconString()) + " " + component.NameString));
                        detailedPayloads.Add(new UIGlowPayload(0));
                        detailedPayloads.Add(new UIForegroundPayload(0));
                    }
                }
            }
        }
        else if (isSetItem)
        {
            var source = item.GetUsesByType<ItemGlamourReadySetItemSource>(ItemInfoType.GlamourReadySetItem).FirstOrDefault();
            if (source != null)
            {
                var ownershipLine = BuildOwnershipLine(HoverItemId);
                if (mode == GlamourReadySetDisplayMode.Compact)
                {
                    var ownedCount = 0;
                    var visualizer = "";
                    foreach (var c in source.SetItems)
                    {
                        if (IsOwned(c.RowId))
                        {
                            visualizer += "X";
                            ownedCount++;
                        }
                        else
                        {
                            visualizer += "O";
                        }
                    }

                    newText += $"\nPart of: {source.ConvertedItem.NameString} ({ownedCount}/{source.SetItems.Count} {visualizer})\n";
                    newText += ownershipLine;
                }
                else
                {
                    var acquiredColor = (ushort)(_acquiredColorSetting.CurrentValue(Configuration) ?? baseColor);
                    var notAcquiredColor = (ushort)(_notAcquiredColorSetting.CurrentValue(Configuration) ?? baseColor);
                    detailedPayloads = new List<Payload>();
                    var header = "\nPart of: " + source.ConvertedItem.NameString + "\n" + ownershipLine;
                    detailedPayloads.Add(new UIForegroundPayload(baseColor));
                    detailedPayloads.Add(new UIGlowPayload(0));
                    detailedPayloads.Add(new TextPayload(header.TrimEnd('\n')));
                    detailedPayloads.Add(new UIGlowPayload(0));
                    detailedPayloads.Add(new UIForegroundPayload(0));
                    foreach (var component in source.SetItems)
                    {
                        var owned = IsOwned(component.RowId);
                        detailedPayloads.Add(new UIForegroundPayload(owned ? acquiredColor : notAcquiredColor));
                        detailedPayloads.Add(new UIGlowPayload(0));
                        detailedPayloads.Add(new TextPayload("\n" + (owned ? SeIconChar.Glamoured.ToIconString() : SeIconChar.Cross.ToIconString()) + " " + component.NameString));
                        detailedPayloads.Add(new UIGlowPayload(0));
                        detailedPayloads.Add(new UIForegroundPayload(0));
                    }
                }
            }
        }

        if (detailedPayloads != null)
        {
            foreach (var p in detailedPayloads)
                seStr.Payloads.Add(p);
            SetTooltipString(stringArrayData, itemTooltipField, seStr);
        }
        else
        {
            newText = newText.TrimEnd('\n');
            if (newText != "")
            {
                var lines = new List<Payload>
                {
                    new UIForegroundPayload(baseColor),
                    new UIGlowPayload(0),
                    new TextPayload(newText),
                    new UIGlowPayload(0),
                    new UIForegroundPayload(0),
                };
                foreach (var line in lines)
                {
                    seStr.Payloads.Add(line);
                }
                SetTooltipString(stringArrayData, itemTooltipField, seStr);
            }
        }
    }

    public override uint Order => 4;
}
