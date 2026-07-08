using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using AllaganLib.Interface.FormFields;
using Dalamud.Plugin.Services;
using InventoryTools.Compendium.Interfaces;
using InventoryTools.Compendium.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventoryTools.Compendium.Models;

public class SectionState : BaseConfiguration
{
    [JsonIgnore]
    public ICompendiumType CompendiumType { get; set; }

    public CompendiumSectionType SectionType { get; set; }

    [JsonIgnore]
    public bool EditMode { get; set; }

    [JsonIgnore]
    public List<string>? SectionOrder
    {
        get
        {
            var json = ((IConfigurable<string?>)this).Get("section_order");
            return json == null ? null : JsonConvert.DeserializeObject<List<string>>(json);
        }
        set => Set("section_order", value == null ? null : JsonConvert.SerializeObject(value));
    }

    public bool IsSectionVisible(string sectionKey) =>
        ((IConfigurable<bool?>)this).Get(sectionKey + "_hidden") != true;

    public void SetSectionVisible(string sectionKey, bool visible) =>
        Set(sectionKey + "_hidden", visible ? (bool?)null : true);
}