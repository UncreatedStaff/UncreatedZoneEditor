#if CLIENT
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System.Collections.Generic;
using Uncreated.ZoneEditor.Tools;

namespace Uncreated.ZoneEditor.UI;

[UIExtension(typeof(EditorUI))]
internal class EditorUIExtension : ContainerUIExtension
{
    private readonly Dictionary<LocationDevkitNode, ISleekLabel> _tags = new Dictionary<LocationDevkitNode, ISleekLabel>(new NodeEqualityComparer());
    private bool _subbed;
    private bool _lastFadeSetting;
    private bool _lastWasPolyEditing;
    private bool _isEnabled;
    private bool _useOrthoOffset;
    private TransformUpdateTracker? _tracker;

    public bool UseOrthoOffset
    {
        get => _useOrthoOffset;
        set
        {
            ThreadUtil.assertIsGameThread();

            _useOrthoOffset = value;
            foreach (ISleekLabel label in _tags.Values)
            {
                UpdateOrthoSettings(label);
            }
        }
    }
    internal bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value == _isEnabled)
                return;

            if (value)
            {
                UseOrthoOffset = MainCamera.instance.orthographic;
                if (!_subbed)
                {
                    _subbed = true;
                    _tracker = new TransformUpdateTracker(MainCamera.instance.transform);
                    TimeUtility.updated += OnUpdate;
                    LevelHierarchy.itemAdded += ItemAdded;
                    LevelHierarchy.itemRemoved += ItemRemoved;
                }

                UpdateAllLocationTags();
            }
            else
            {
                if (_subbed)
                {
                    _subbed = false;
                    _tracker = null;

                    TimeUtility.updated -= OnUpdate;
                    LevelHierarchy.itemAdded -= ItemAdded;
                    LevelHierarchy.itemRemoved -= ItemRemoved;
                }

                Container.RemoveAllChildren();
                _tags.Clear();
            }

            _isEnabled = value;
            Container.IsVisible = value;
        }
    }

    protected override SleekWindow Parent => EditorUI.window;

    protected override void OnShown()
    {
        IsEnabled = UserControl.ActiveTool is ZoneEditorTool;
    }

    protected override void OnHidden()
    {
        IsEnabled = false;
    }

    protected override void OnDestroyed()
    {
        IsEnabled = false;
    }

    private void ItemAdded(IDevkitHierarchyItem item)
    {
        if (item is LocationDevkitNode node)
        {
            UpdateLocationTag(node);
        }
    }
    private void ItemRemoved(IDevkitHierarchyItem item)
    {
        if (item is LocationDevkitNode node)
        {
            UpdateLocationTag(node);
        }
    }

    private void OnUpdate()
    {
        if (_tracker == null)
            return;

        _tracker.OnUpdate();
        if (_lastFadeSetting != OptionsSettings.shouldNametagFadeOut
            || _tracker.HasPositionChanged
            || _tracker.HasRotationChanged
            || UserControl.ActiveTool is ZoneEditorTool { PolygonEditTarget: not null } != _lastWasPolyEditing)
        {
            UpdateAllLocationTags();
        }
    }

    internal void UpdateLocationTag(LocationDevkitNode node)
    {
        if (_tags.TryGetValue(node, out ISleekLabel label))
        {
            if (node.isActiveAndEnabled)
            {
                UpdateTag(label, node);
            }
            else
            {
                _tags.Remove(node);
            }
        }
        else
        {
            CreateTag(node);
        }
    }

    internal void UpdateAllLocationTags()
    {
        if (Container == null)
            return;

        _lastFadeSetting = OptionsSettings.shouldNametagFadeOut;
        bool lastWasPolyEditing = _lastWasPolyEditing;
        _lastWasPolyEditing = UserControl.ActiveTool is ZoneEditorTool { PolygonEditTarget: not null };
        if (_lastWasPolyEditing)
        {
            if (lastWasPolyEditing)
                return;

            foreach (ISleekLabel label in _tags.Values)
            {
                label.IsVisible = false;
            }

            return;
        }

        List<LocationDevkitNode>? valuesToRemove = null;

        foreach (KeyValuePair<LocationDevkitNode, ISleekLabel> label in _tags)
        {
            if (label.Key.isActiveAndEnabled)
            {
                UpdateTag(label.Value, label.Key);
            }
            else
            {
                (valuesToRemove ??= new List<LocationDevkitNode>(1)).Add(label.Key);
            }
        }

        if (valuesToRemove != null)
        {
            for (int i = 0; i < valuesToRemove.Count; i++)
            {
                LocationDevkitNode node = valuesToRemove[i];
                _tags.Remove(node);
            }
        }

        foreach (LocationDevkitNode node in LocationDevkitNodeSystem.Get().GetAllNodes())
        {
            if (!_tags.ContainsKey(node))
                CreateTag(node);
        }
    }

    private void CreateTag(LocationDevkitNode node)
    {
        if (Container == null || node == null)
            return;

        // PlayerGroupUI.addGroup
        ISleekLabel label = Glazier.Get().CreateLabel();
        UpdateOrthoSettings(label);
        label.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        label.Text = node.locationName;
        label.IsVisible = !_lastWasPolyEditing;
        UpdateTag(label, node);
        Container.AddChild(label);
        _tags.Add(node, label);
    }

    private void UpdateOrthoSettings(ISleekLabel label)
    {
        if (!UseOrthoOffset)
        {
            label.PositionOffset_X = -100;
            label.PositionOffset_Y = -15;
            label.SizeOffset_X = 200;
            label.SizeOffset_Y = 30;
        }
        else
        {
            label.PositionOffset_X = -200;
            label.PositionOffset_Y = -30;
            label.SizeOffset_X = 400;
            label.SizeOffset_Y = 60;
            label.TextColor = ESleekTint.FONT;
        }
    }

    private void UpdateTag(ISleekLabel nametag, LocationDevkitNode node)
    {
        if (Container == null || node == null)
            return;

        Vector3 screenPos = MainCamera.instance.WorldToViewportPoint(node.transform.position + Vector3.up * 30f);
        if (screenPos.z <= 0.0)
        {
            if (nametag.IsVisible)
                nametag.IsVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = Container.ViewportToNormalizedPosition(screenPos);
            nametag.PositionScale_X = adjScreenPos.x;
            nametag.PositionScale_Y = adjScreenPos.y;

            if (!nametag.IsVisible)
                nametag.IsVisible = !_lastWasPolyEditing;

            float alpha;
            if (OptionsSettings.shouldNametagFadeOut)
            {
                float magnitude = new Vector2(adjScreenPos.x - 0.5f, adjScreenPos.y - 0.5f).magnitude;
                float t = Mathf.InverseLerp(0.0125f, 0.1f, magnitude);
                alpha = Mathf.Lerp(0.1f, 0.75f, t);
            }
            else
            {
                alpha = 0.75f;
            }

            nametag.TextColor = new SleekColor(ESleekTint.FONT, alpha);
        }
    }

    private class NodeEqualityComparer : IEqualityComparer<LocationDevkitNode>
    {
        public bool Equals(LocationDevkitNode x, LocationDevkitNode y) => ReferenceEquals(x, y);

        // ReSharper disable once RedundantCast
        public int GetHashCode(LocationDevkitNode obj) => ((object)obj).GetHashCode();
    }
}
#endif