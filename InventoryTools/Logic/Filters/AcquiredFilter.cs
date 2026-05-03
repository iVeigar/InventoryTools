using AllaganLib.GameSheets.Sheets.Rows;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services;
using Dalamud.Plugin.Services;
using InventoryTools.Logic.Filters.Abstract;
using InventoryTools.Services;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Logic.Filters
{
    public class AcquiredFilter : BooleanFilter
    {
        private readonly InventoryToolsConfiguration _configuration;
        private readonly IPlayerState _playerState;
        private readonly IGameInterface _gameInterface;

        public AcquiredFilter(ILogger<AcquiredFilter> logger, ImGuiService imGuiService, InventoryToolsConfiguration configuration, IPlayerState playerState) : base(logger, imGuiService)
        {
            _configuration = configuration;
            _playerState = playerState;
        }
        public override string Key { get; set; } = "Acquired";
        public override string Name { get; set; } = "Is Acquired?";
        public override string HelpText { get; set; } = "Has this item be acquired by your active character?";
        public override FilterCategory FilterCategory { get; set; } = FilterCategory.Acquisition;

        public override bool? FilterItem(FilterConfiguration configuration,InventoryItem item)
        {

            return FilterItem(configuration, item.Item);
        }

        public override bool? FilterItem(FilterConfiguration configuration, ItemRow item)
        {
            var currentValue = CurrentValue(configuration);
            if (currentValue == null)
            {
                return true;
            }
            var action = item.Base.ItemAction.ValueNullable;
            if (!ActionTypeExt.IsValidAction(action)) {
                return false;
            }

            var isUnlocked = false;
            if(this._configuration.AcquiredItems.TryGetValue(_playerState.ContentId, out var value))
            {
                if (value.Contains(item.RowId))
                {
                    isUnlocked = true;
                }
            }

            return currentValue.Value && isUnlocked || !currentValue.Value && !isUnlocked;
        }
    }
}