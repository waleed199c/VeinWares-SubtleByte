using BepInEx.Logging;
using ClientUI.UniverseLib.UI.Panels;
using PanelBase = ClientUI.UniverseLib.UI.Panels.PanelBase;
using UIBase = ClientUI.UniverseLib.UI.UIBase;

namespace ClientUI.UI.Panel;

public abstract class ResizeablePanelBase : PanelBase
{
    protected ResizeablePanelBase(UIBase owner) : base(owner) { }

    protected abstract UIManager.Panels PanelType { get; }
    public override PanelDragger.ResizeTypes CanResize => PanelDragger.ResizeTypes.All;
    public virtual bool ResizeWholePanel => true;

    private bool ApplyingSaveData { get; set; } = true;

    private string PanelConfigKey => $"{PanelType}-{Name}";

    protected override void ConstructPanelContent()
    {
        // Disable the title bar, but still enable the draggable box area (this now being set to the whole panel)
        TitleBar.SetActive(false);
        if (ResizeWholePanel)
        {
            Dragger.DraggableArea = Rect;
            // Update resizer elements
            Dragger.OnEndResize();
        }
    }

    /// <summary>
    /// Intended to be called when leaving a server to ensure joining the next can build up the UI correctly again
    /// </summary>
    internal abstract void Reset();

    protected override void OnClosePanelClicked()
    {
        // Do nothing for now
    }

    public override void OnFinishDrag()
    {
        base.OnFinishDrag();
        SaveInternalData();
    }

    public override void OnFinishResize()
    {
        base.OnFinishResize();
        SaveInternalData();
    }

    public void SaveInternalData()
    {
        if (ApplyingSaveData) return;

        SetSaveDataToConfigValue();
    }

    private void SetSaveDataToConfigValue()
    {
        Plugin.Instance.Config.Bind("Panels", PanelConfigKey, "", "Serialised panel data").Value = this.ToSaveData();
    }

    private string ToSaveData()
    {
        try
        {
            return string.Join("|", new string[]
            {
                Rect.RectAnchorsToString(),
                Rect.RectPositionToString()
            });
        }
        catch (Exception ex)
        {
            Plugin.Log(LogLevel.Warning,$"Exception generating Panel save data: {ex}");
            return "";
        }
    }

    private void ApplySaveData()
    {
        var data = Plugin.Instance.Config.Bind("Panels", PanelConfigKey, "", "Serialised panel data").Value;
        // Load from the old key if the new key is empty. This ensures a good transition to the new format, while not losing existing config.
        // This is deprecated and should be removed in a later version.
        if (string.IsNullOrEmpty(data))
        {
            data = Plugin.Instance.Config.Bind("Panels", $"{PanelType}", "", "Serialised panel data").Value;
        }
        ApplySaveData(data);
    }

    private void ApplySaveData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        string[] split = data.Split('|');

        try
        {
            Rect.SetAnchorsFromString(split[0]);
            Rect.SetPositionFromString(split[1]);
            this.EnsureValidSize();
            this.EnsureValidPosition();
        }
        catch
        {
            Plugin.Log(LogLevel.Warning, "Invalid or corrupt panel save data! Restoring to default.");
            SetDefaultSizeAndPosition();
            SetSaveDataToConfigValue();
        }
    }

    protected override void LateConstructUI()
    {
        ApplyingSaveData = true;

        base.LateConstructUI();

        // apply panel save data or revert to default
        try
        {
            ApplySaveData();
        }
        catch (Exception ex)
        {
            Plugin.Log(LogLevel.Error,$"Exception loading panel save data: {ex}");
            SetDefaultSizeAndPosition();
        }

        ApplyingSaveData = false;

        Dragger.OnEndResize();
    }
}