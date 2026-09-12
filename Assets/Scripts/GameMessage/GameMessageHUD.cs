using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight bottom-left message/chat feed.
/// Uses IMGUI intentionally so it remains drop-in and scene/prefab independent.
/// GameMessageService owns history; this component is presentation only.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameMessageHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField, Min(220f)] private float expandedWidth = 520f;
    [SerializeField, Min(120f)] private float expandedHeight = 280f;
    [SerializeField, Min(0f)] private float leftMargin = 18f;
    [SerializeField, Min(0f)] private float bottomMargin = 18f;
    [SerializeField, Min(8)] private int fontSize = 16;

    [Header("Toggle")]
    [SerializeField] private bool startExpanded = false;
    [SerializeField, Min(36f)] private float toggleWidth = 76f;
    [SerializeField, Min(18f)] private float toggleHeight = 24f;
    [SerializeField, Min(18f)] private float closeButtonSize = 24f;

    [Header("Collapsed Preview")]
    [Tooltip("Recent messages still appear briefly while the full log is closed, so important feedback is not hidden behind the LOG button.")]
    [SerializeField, Min(0)] private int maxCollapsedMessages = 2;
    [SerializeField, Min(80f)] private float collapsedPreviewHeight = 86f;

    [Header("Message Lifetimes")]
    [SerializeField, Min(0.5f)] private float infoLifetimeSeconds = 8f;
    [SerializeField, Min(0.5f)] private float warningLifetimeSeconds = 12f;
    [SerializeField, Min(0.5f)] private float errorLifetimeSeconds = 16f;
    [SerializeField, Min(0.5f)] private float playerChatLifetimeSeconds = 20f;
    [SerializeField, Min(0.1f)] private float fadeDurationSeconds = 2f;

    private readonly List<GameMessage> _collapsedVisible =
        new List<GameMessage>();

    private GUIStyle _messageStyle;
    private GUIStyle _boxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _buttonStyle;

    private bool _expanded;
    private Vector2 _scroll;
    private long _lastReadSequence;

    private GameMessageService _subscribedService;

    private void Awake()
    {
        _expanded = startExpanded;
    }

    private void OnEnable()
    {
        BindService();
    }

    private void OnDisable()
    {
        UnbindService();
    }

    private void OnGUI()
    {
        GameMessageService service = GameMessageService.I;
        if (service == null)
            return;

        if (!ReferenceEquals(service, _subscribedService))
            BindService();

        EnsureStyles();

        Rect toggleRect = new Rect(
            leftMargin,
            Screen.height - bottomMargin - toggleHeight,
            toggleWidth,
            toggleHeight);

        int unread = CountUnread(service.History);

        string toggleLabel =
            _expanded
                ? "HIDE"
                : unread > 0
                    ? $"LOG ({unread})"
                    : "LOG";

        if (GUI.Button(toggleRect, toggleLabel, _buttonStyle))
        {
            _expanded = !_expanded;

            if (_expanded)
            {
                MarkAllRead(service.History);
                ScrollToBottom();
            }
        }

        if (_expanded)
        {
            DrawExpanded(service.History, toggleRect);
        }
        else
        {
            DrawCollapsedPreview(service.History, toggleRect);
        }
    }

    private void DrawExpanded(
        IReadOnlyList<GameMessage> history,
        Rect toggleRect)
    {
        float panelY =
            Mathf.Max(
                8f,
                toggleRect.y - expandedHeight - 6f);

        Rect panel = new Rect(
            leftMargin,
            panelY,
            expandedWidth,
            expandedHeight);

        GUI.Box(panel, GUIContent.none, _boxStyle);

        const float inner = 8f;
        const float headerHeight = 26f;

        GUI.Label(
            new Rect(
                panel.x + inner,
                panel.y + inner,
                panel.width - inner * 2f - closeButtonSize - 4f,
                headerHeight),
            "SHIP LOG",
            _headerStyle);

        Rect closeRect = new Rect(
            panel.xMax - inner - closeButtonSize,
            panel.y + inner,
            closeButtonSize,
            closeButtonSize);

        if (GUI.Button(closeRect, "X", _buttonStyle))
        {
            _expanded = false;
            return;
        }

        Rect scrollRect = new Rect(
            panel.x + inner,
            panel.y + inner + headerHeight + 2f,
            panel.width - inner * 2f,
            panel.height - inner * 2f - headerHeight - 2f);

        float contentWidth =
            Mathf.Max(
                100f,
                scrollRect.width - 22f);

        float contentHeight =
            CalculateExpandedContentHeight(
                history,
                contentWidth);

        Rect contentRect = new Rect(
            0f,
            0f,
            contentWidth,
            contentHeight);

        _scroll = GUI.BeginScrollView(
            scrollRect,
            _scroll,
            contentRect);

        float y = 0f;

        if (history != null)
        {
            for (int i = 0; i < history.Count; i++)
            {
                GameMessage message = history[i];
                if (message == null)
                    continue;

                string text = FormatMessage(message);

                float messageHeight =
                    Mathf.Max(
                        22f,
                        _messageStyle.CalcHeight(
                            new GUIContent(text),
                            contentWidth));

                GUI.Label(
                    new Rect(
                        0f,
                        y,
                        contentWidth,
                        messageHeight),
                    text,
                    _messageStyle);

                y += messageHeight + 2f;
            }
        }

        GUI.EndScrollView();
    }

    private void DrawCollapsedPreview(
        IReadOnlyList<GameMessage> history,
        Rect toggleRect)
    {
        BuildCollapsedMessages(history);

        if (_collapsedVisible.Count == 0)
            return;

        Rect panel = new Rect(
            leftMargin,
            Mathf.Max(
                8f,
                toggleRect.y - collapsedPreviewHeight - 6f),
            expandedWidth,
            collapsedPreviewHeight);

        GUI.Box(panel, GUIContent.none, _boxStyle);

        GUILayout.BeginArea(
            new Rect(
                panel.x + 6f,
                panel.y + 4f,
                panel.width - 12f,
                panel.height - 8f));

        GUILayout.FlexibleSpace();

        for (int i = 0; i < _collapsedVisible.Count; i++)
        {
            GameMessage message = _collapsedVisible[i];
            float alpha = CalculateAlpha(message);

            Color previousColor = GUI.color;
            GUI.color = new Color(
                previousColor.r,
                previousColor.g,
                previousColor.b,
                alpha);

            GUILayout.Label(
                FormatMessage(message),
                _messageStyle);

            GUI.color = previousColor;
        }

        GUILayout.EndArea();
    }

    private void BuildCollapsedMessages(
        IReadOnlyList<GameMessage> history)
    {
        _collapsedVisible.Clear();

        int limit =
            Mathf.Max(
                0,
                maxCollapsedMessages);

        if (limit <= 0 ||
            history == null ||
            history.Count == 0)
        {
            return;
        }

        double now =
            Time.realtimeSinceStartupAsDouble;

        for (int i = history.Count - 1;
             i >= 0 &&
             _collapsedVisible.Count < limit;
             i--)
        {
            GameMessage message =
                history[i];

            if (message == null)
                continue;

            double age =
                now -
                message.PostedRealtimeSeconds;

            if (age >
                LifetimeFor(
                    message.Type))
            {
                continue;
            }

            _collapsedVisible.Add(
                message);
        }

        _collapsedVisible.Reverse();
    }

    private float CalculateExpandedContentHeight(
        IReadOnlyList<GameMessage> history,
        float width)
    {
        float total = 2f;

        if (history == null)
            return total;

        for (int i = 0; i < history.Count; i++)
        {
            GameMessage message =
                history[i];

            if (message == null)
                continue;

            string text =
                FormatMessage(
                    message);

            total +=
                Mathf.Max(
                    22f,
                    _messageStyle.CalcHeight(
                        new GUIContent(text),
                        width)) +
                2f;
        }

        return
            Mathf.Max(
                total,
                expandedHeight - 46f);
    }

    private int CountUnread(
        IReadOnlyList<GameMessage> history)
    {
        if (history == null ||
            history.Count == 0)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < history.Count;
             i++)
        {
            GameMessage message =
                history[i];

            if (message != null &&
                message.Sequence >
                _lastReadSequence)
            {
                count++;
            }
        }

        return count;
    }

    private void MarkAllRead(
        IReadOnlyList<GameMessage> history)
    {
        if (history == null ||
            history.Count == 0)
        {
            return;
        }

        for (int i = history.Count - 1;
             i >= 0;
             i--)
        {
            if (history[i] == null)
                continue;

            _lastReadSequence =
                history[i].Sequence;

            break;
        }
    }

    private void ScrollToBottom()
    {
        _scroll.y =
            float.MaxValue;
    }

    private void BindService()
    {
        UnbindService();

        _subscribedService =
            GameMessageService.I;

        if (_subscribedService == null)
            return;

        _subscribedService.MessagePosted +=
            HandleMessagePosted;
    }

    private void UnbindService()
    {
        if (_subscribedService != null)
        {
            _subscribedService.MessagePosted -=
                HandleMessagePosted;
        }

        _subscribedService = null;
    }

    private void HandleMessagePosted(
        GameMessage message)
    {
        if (!_expanded ||
            message == null)
        {
            return;
        }

        _lastReadSequence =
            message.Sequence;

        ScrollToBottom();
    }

    private float CalculateAlpha(
        GameMessage message)
    {
        if (message == null)
            return 0f;

        float lifetime =
            LifetimeFor(
                message.Type);

        float age =
            (float)(
                Time.realtimeSinceStartupAsDouble -
                message.PostedRealtimeSeconds);

        float fade =
            Mathf.Max(
                0.1f,
                fadeDurationSeconds);

        float fadeStart =
            Mathf.Max(
                0f,
                lifetime -
                fade);

        if (age <= fadeStart)
            return 1f;

        return
            Mathf.Clamp01(
                1f -
                ((age - fadeStart) / fade));
    }

    private float LifetimeFor(
        GameMessageType type)
    {
        switch (type)
        {
            case GameMessageType.Warning:
                return warningLifetimeSeconds;

            case GameMessageType.Error:
                return errorLifetimeSeconds;

            case GameMessageType.PlayerChat:
                return playerChatLifetimeSeconds;

            case GameMessageType.System:
            case GameMessageType.Info:
            default:
                return infoLifetimeSeconds;
        }
    }

    private string FormatMessage(
        GameMessage message)
    {
        if (message == null)
            return string.Empty;

        string prefix;

        switch (message.Type)
        {
            case GameMessageType.Warning:
                prefix = "[!] ";
                break;

            case GameMessageType.Error:
                prefix = "[ERROR] ";
                break;

            case GameMessageType.PlayerChat:
                prefix =
                    string.IsNullOrWhiteSpace(
                        message.Sender)
                        ? ""
                        : $"[{message.Sender}] ";
                break;

            case GameMessageType.System:
                prefix = "[SYSTEM] ";
                break;

            case GameMessageType.Info:
            default:
                prefix = "";
                break;
        }

        return
            prefix +
            message.Text;
    }

    private void EnsureStyles()
    {
        if (_messageStyle == null)
        {
            _messageStyle =
                new GUIStyle(
                    GUI.skin.label)
                {
                    alignment =
                        TextAnchor.UpperLeft,
                    wordWrap = true,
                    richText = false,
                    fontSize = fontSize,
                    padding =
                        new RectOffset(
                            4,
                            4,
                            1,
                            1)
                };
        }

        if (_headerStyle == null)
        {
            _headerStyle =
                new GUIStyle(
                    GUI.skin.label)
                {
                    alignment =
                        TextAnchor.MiddleLeft,
                    fontSize =
                        Mathf.Max(
                            fontSize,
                            16),
                    fontStyle =
                        FontStyle.Bold
                };
        }

        if (_boxStyle == null)
        {
            _boxStyle =
                new GUIStyle(
                    GUI.skin.box)
                {
                    padding =
                        new RectOffset(
                            6,
                            6,
                            6,
                            6)
                };
        }

        if (_buttonStyle == null)
        {
            _buttonStyle =
                new GUIStyle(
                    GUI.skin.button)
                {
                    alignment =
                        TextAnchor.MiddleCenter,
                    fontSize =
                        Mathf.Max(
                            10,
                            fontSize - 3)
                };
        }
    }
}
