using System.Collections.Generic;
using System.Linq;
using AllaganLib.GameSheets.Caches;
using AllaganLib.GameSheets.ItemSources;
using AllaganLib.GameSheets.Sheets;
using CriticalCommonLib.Enums;
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

public class CofferLootTooltip : BaseTooltip
{
    private readonly TooltipCofferLootColorSetting _colorSetting;
    private readonly TooltipDisplayCofferLootSetting _displaySetting;
    private readonly TooltipCofferLootScopeSetting _scopeSetting;
    private readonly IInventoryMonitor _inventoryMonitor;
    private readonly InventoryScopeCalculator _inventoryScopeCalculator;
    private readonly IUnlockTrackerService _unlockTrackerService;

    public CofferLootTooltip(ILogger<CofferLootTooltip> logger, TooltipCofferLootColorSetting colorSetting, TooltipDisplayCofferLootSetting displaySetting, TooltipCofferLootScopeSetting scopeSetting, IInventoryMonitor inventoryMonitor, InventoryScopeCalculator inventoryScopeCalculator, ItemSheet itemSheet, InventoryToolsConfiguration configuration, IGameGui gameGui, IChatGui chatGui, IUnlockTrackerService unlockTrackerService) : base(6910, logger, itemSheet, configuration, gameGui, chatGui)
    {
        _colorSetting = colorSetting;
        _displaySetting = displaySetting;
        _scopeSetting = scopeSetting;
        _inventoryMonitor = inventoryMonitor;
        _inventoryScopeCalculator = inventoryScopeCalculator;
        _unlockTrackerService = unlockTrackerService;
    }

    public override bool IsEnabled => Configuration.DisplayTooltip && _displaySetting.CurrentValue(Configuration);

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
    {
        if (!ShouldShow()) return;
        var item = HoverItem;
        if (item == null) return;

        // Coffer path: hovering the container itself
        var lootItems = item.GetUsesByType<ItemLootSource>(ItemInfoType.Loot).Select(s => s.Item)
            .Concat(item.GetUsesByType<ItemCofferSource>(ItemInfoType.Coffer).Select(s => s.Item))
            .Concat(item.GetUsesByType<ItemCardPackSource>(ItemInfoType.CardPack).Select(s => s.Item))
            .DistinctBy(r => r.RowId)
            .ToList();

        // Loot item path: hovering an item that comes from a container
        var cofferSources = new List<ItemSupplementSource>();
        cofferSources.AddRange(item.GetSourcesByType<ItemLootSource>(ItemInfoType.Loot));
        cofferSources.AddRange(item.GetSourcesByType<ItemCofferSource>(ItemInfoType.Coffer));
        cofferSources.AddRange(item.GetSourcesByType<ItemCardPackSource>(ItemInfoType.CardPack));

        if (lootItems.Count == 0 && cofferSources.Count == 0) return;

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
            allItems = allItems.Where(c => _inventoryScopeCalculator.Filter(scope, c));

        var allItemsList = allItems.ToList();
        var newText = "";

        if (lootItems.Count > 0)
        {
            var ownedCount = lootItems.Count(loot => (_unlockTrackerService.IsUnlocked(loot, false) == true) ||  allItemsList.Any(i => i.ItemId == loot.RowId));
            newText += $"\nLoot: {ownedCount}/{lootItems.Count} items owned";
        }
        else
        {
            var uniqueCoffers = cofferSources
                .DistinctBy(s => s.CostItem!.RowId)
                .ToList();

            newText += "\nAvailable in:";
            foreach (var source in uniqueCoffers)
            {
                var cofferItem = source.CostItem!;
                var cofferLoot = cofferItem.GetUsesByType<ItemLootSource>(ItemInfoType.Loot).Select(s => s.Item)
                    .Concat(cofferItem.GetUsesByType<ItemCofferSource>(ItemInfoType.Coffer).Select(s => s.Item))
                    .Concat(cofferItem.GetUsesByType<ItemCardPackSource>(ItemInfoType.CardPack).Select(s => s.Item))
                    .DistinctBy(r => r.RowId)
                    .ToList();
                var ownedFromCoffer = cofferLoot.Count(loot => (_unlockTrackerService.IsUnlocked(loot, false) == true) || allItemsList.Any(i => i.ItemId == loot.RowId));
                newText += $"\n {cofferItem.NameString} ({ownedFromCoffer} of {cofferLoot.Count})";
            }

            var alreadyAcquired = allItemsList.Any(i => i.ItemId == HoverItemId);
            if (alreadyAcquired)
                newText += "\nAlready acquired";
        }

        if (newText == "") return;

        var lines = new List<Payload>
        {
            new UIForegroundPayload((ushort)(_colorSetting.CurrentValue(Configuration) ?? Configuration.TooltipColor ?? 1)),
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

    public override uint Order => 5;
}
