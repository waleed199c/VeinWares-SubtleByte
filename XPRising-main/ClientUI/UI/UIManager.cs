﻿using ClientUI.UI.Panel;
using UnityEngine;
using UIBase = ClientUI.UniverseLib.UI.UIBase;
using UniversalUI = ClientUI.UniverseLib.UI.UniversalUI;

namespace ClientUI.UI;

public static class UIManager
{
    public enum Panels
    {
        Base,
    }
    
    public static bool IsInitialised { get; private set; }
    
    internal static void Initialize()
    {
        UniversalUI.Init();
    }

    public static UIBase UiBase { get; private set; }
    public static GameObject UIRoot => UiBase?.RootObject;
    public static ContentPanel ContentPanel { get; private set; }
    public static TextPanel TextPanel { get; private set; }

    public static void OnInitialized()
    {
        if (IsInitialised) return;
        
        UiBase = UniversalUI.RegisterUI(MyPluginInfo.PLUGIN_GUID, UiUpdate);
        
        ContentPanel = new ContentPanel(UiBase);
        ContentPanel.SetActive(false);

        TextPanel = new TextPanel(UiBase);
        TextPanel.SetActive(false);
        
        SetActive(true);
    }

    public static void SetActive(bool active)
    {
        if (ContentPanel == null) return;
        
        ContentPanel.SetActive(active);
        
        // Hide any open menus
        if (!active) ContentPanel.CloseActionPanel();
        
        // Hide the panel, but don't make it reappear
        if (!active) TextPanel.SetActive(false);
        
        IsInitialised = true;
    }

    public static void Reset()
    {
        ContentPanel.Reset();
        TextPanel.Reset();
    }

    private static void UiUpdate()
    {
        // Called once per frame when your UI is being displayed.
    }
}