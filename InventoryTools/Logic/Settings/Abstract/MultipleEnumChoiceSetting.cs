using System;
using System.Collections.Generic;
using InventoryTools.Services;
using Microsoft.Extensions.Logging;

namespace InventoryTools.Logic.Settings.Abstract;

public abstract class MultipleEnumChoiceSetting<TEnum> : MultipleChoiceSetting<TEnum>
    where TEnum : Enum, IComparable
{
    protected MultipleEnumChoiceSetting(ILogger logger, ImGuiService imGuiService) : base(logger, imGuiService)
    {
    }

    public abstract Dictionary<TEnum, string> Choices { get; }

    public override Dictionary<TEnum, string> GetChoices(InventoryToolsConfiguration configuration) => Choices;

    public override bool HideAlreadyPicked { get; set; } = true;
}
