﻿using ClientUI.UI.Util;
using UnityEngine;
using UnityEngine.UI;
using XPShared.Transport.Messages;
using UIFactory = ClientUI.UniverseLib.UI.UIFactory;

namespace ClientUI.UI.Panel;

public class ProgressBarPanel
{
    private readonly GameObject _contentRoot;
    private bool _resetGroupActiveState = false;
    
    public ProgressBarPanel(GameObject root)
    {
        _contentRoot = root;
        
        UIFactory.SetLayoutGroup<VerticalLayoutGroup>(_contentRoot, true, false, true, true, 0);
    }
    
    public bool Active
    {
        get => _contentRoot.active;
        set => _contentRoot.SetActive(value);
    }
    
    private const int Spacing = 4;
    private const int VPadding = 2;
    private const int HPadding = 2;
    private readonly Vector4 _paddingVector = new Vector4(VPadding, VPadding, HPadding, HPadding);

    private readonly Dictionary<string, ProgressBar> _bars = new();
    private readonly Dictionary<string, Group> _groups = new();

    private class Group
    {
        public readonly GameObject GameObject;
        public readonly RectTransform RectTransform;
        public readonly List<string> BarLabels = new();

        public Group(RectTransform rectTransform, GameObject gameObject)
        {
            RectTransform = rectTransform;
            GameObject = gameObject;
        }
    }

    private void FlagGroupsForActiveCheck()
    {
        _resetGroupActiveState = true;
    }
    
    public void ChangeProgress(ProgressSerialisedMessage data)
    {
        if (!_bars.TryGetValue(data.Label, out var progressBar))
        {
            // Don't add a bar just to remove it
            if (data.Active == ProgressSerialisedMessage.ActiveState.Remove) return;
            progressBar = AddBar(data.Group, data.Label);
        }

        var nullProgress = data.ProgressPercentage < 0;
        var validatedProgress = nullProgress ? 1f : Math.Min(data.ProgressPercentage, 1f);
        var tooltip = nullProgress ? data.Tooltip : $"{data.Tooltip} ({validatedProgress:P})";
        var colour = Colour.ParseColour(data.Colour, validatedProgress);
        progressBar.SetProgress(validatedProgress, data.Header, tooltip, data.Active, colour, data.Change, data.Flash);

        // Set all other labels to disappear if this is set to OnlyActive
        if (data.Active == ProgressSerialisedMessage.ActiveState.OnlyActive)
        {
            var group = _groups[data.Group];
            group.BarLabels.ForEach(label =>
            {
                if (label == data.Label) return;
                if (_bars.TryGetValue(label, out var otherProgressBar))
                {
                    otherProgressBar.FadeOut();
                }
            });
        } else if (data.Active == ProgressSerialisedMessage.ActiveState.Remove)
        {
            // Remove from group.BarLabels
            var group = _groups[data.Group];
            group.BarLabels.Remove(data.Label);
            // Remove from _bars
            _bars.Remove(data.Label);
            
            // Remove the progress bar after the fadeout
            progressBar.FadeOut(true);
        }

        // TODO work out how/when this should happen
        // if (data.Change != "")
        // {
        //     FloatingText.SpawnFloatingText(_contentRoot, data.Change, Colour.Highlight);
        // }
    }

    internal void Reset()
    {
        foreach (var (_, group) in _groups)
        {
            GameObject.Destroy(group.GameObject);
        }
        _groups.Clear();

        // Cancel any existing timers
        foreach (var (_, bar) in _bars)
        {
            bar.Reset();
        }
        _bars.Clear();
        FlagGroupsForActiveCheck();
    }

    private ProgressBar AddBar(string groupName, string label)
    {
        if (!_groups.TryGetValue(groupName, out var group))
        {
            var groupGameObject = UIFactory.CreateVerticalGroup(_contentRoot, groupName, true, false, true, true, Spacing, padding: _paddingVector);
            group = new Group(groupGameObject.GetComponent<RectTransform>(), groupGameObject);
            _groups.Add(groupName, group);
        }
        group.BarLabels.Add(label);
        var progressBar = new ProgressBar(group.GameObject, Colour.DefaultBar);
        _bars.Add(label, progressBar);
        progressBar.ProgressBarMinimised += (_, _) => { FlagGroupsForActiveCheck(); }; 
        
        return progressBar;
    }

    public void Update()
    {
        if (!_resetGroupActiveState) return;
        _resetGroupActiveState = false;
        foreach (var (_, group) in _groups)
        {
            var activeBarCount = group.RectTransform.GetAllChildren().Count(transform => transform.gameObject.active);
            group.GameObject.SetActive(activeBarCount > 0);
        }
    }
}