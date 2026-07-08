using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using InventoryTools.Compendium.Interfaces;
using InventoryTools.Compendium.Models;
using InventoryTools.Compendium.Sections.Options;
using InventoryTools.Services;

namespace InventoryTools.Compendium.Sections;

public abstract class ViewSection : ICompendiumViewSection
{
    private readonly ImGuiService _imGuiService;
    private List<ImGuiService.HeaderButton>? _headerButtons;

    public ViewSection(SectionOptions sectionOptions, ImGuiService imGuiService)
    {
        SectionOptions = sectionOptions;
        _imGuiService = imGuiService;
    }

    public SectionOptions SectionOptions { get; }

    public void Draw(SectionState sectionState)
    {
        if (!ShouldDraw(sectionState))
        {
            return;
        }

        if (_headerButtons == null)
        {
            _headerButtons = new();
            if (DrawMenu != null)
            {
                _headerButtons.Add(new ImGuiService.HeaderButton()
                {
                    Id = "Menu",
                    Label = "Menu",
                    Image = "menu",
                    Callback = () => { },
                });
            }
            if (DrawOptions != null)
            {
                _headerButtons.Add(new ImGuiService.HeaderButton()
                {
                    Id = "Settings",
                    Image = "wrench-icon",
                    Label = "Settings",
                    Callback = () => { },
                });
            }
        }
        if (_imGuiService.CollapsingHeader(SectionName, out var buttonClicked, _headerButtons, ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawSection(sectionState);
        }

        if (buttonClicked == "Menu")
        {
            ImGui.OpenPopup("MenuPopup");
        }

        using (var menuPopup = ImRaii.Popup("MenuPopup"))
        {
            if (menuPopup)
            {
                DrawMenu?.Invoke(sectionState);
            }
        }

        if (buttonClicked == "Settings")
        {
            ImGui.OpenPopup("SettingsPopup");
        }

        using (var menuPopup = ImRaii.Popup("SettingsPopup"))
        {
            if (menuPopup)
            {
                DrawOptions?.Invoke(sectionState);
            }
        }
    }

    public bool ShouldDraw(SectionState sectionState)
    {
        if (IsEmpty(sectionState) && this.SectionOptions.HideWhenEmpty)
        {
            return false;
        }
        return true;
    }

    public virtual bool IsEmpty(SectionState sectionState)
    {
        return false;
    }


    public abstract string SectionName { get; }

    public virtual Action<SectionState>? DrawOptions => null;
    public virtual Action<SectionState>? DrawMenu => null;

    public abstract void DrawSection(SectionState sectionState);
}