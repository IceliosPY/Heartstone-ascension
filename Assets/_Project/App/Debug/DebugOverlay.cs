using System.Collections.Generic;
using System.Text;
using CoH.Core.Diagnostics;
using CoH.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoH.App
{
    /// <summary>
    /// The developer panel: what the match holds, what has been submitted, and
    /// the buttons that make a bug reproducible.
    ///
    /// It builds its own interface when it wakes up rather than living in the
    /// scene. A tool that exists only while it is running cannot be shipped by
    /// accident, cannot be nudged out of place by someone editing the board, and
    /// costs the scene file nothing.
    ///
    /// Plain and rectangular on purpose. It is read, not admired, and every
    /// minute spent styling it is a minute not spent on the game.
    ///
    /// It refreshes when something happens, never every frame: rebuilding a
    /// canonical description of a whole match sixty times a second to show a
    /// number that changes twice a minute would be a strange way to spend a
    /// budget.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const int FontSize = 15;

        [SerializeField] private MatchDebugTools tools;

        [Tooltip("Shown from the start. Off by default: this is not part of the game.")]
        [SerializeField] private bool openOnStart;

        private readonly List<string> _replayPaths = new List<string>();

        private GameObject _root;
        private TextMeshProUGUI _stateText;
        private TextMeshProUGUI _commandText;
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _statusText;
        private RectTransform _replayList;

        private string _selectedReplay = string.Empty;
        private string _status = "F1 closes this panel.";

        /// <summary>Whether the panel is showing. Read by tests.</summary>
        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            Build();
            SetOpen(openOnStart);
        }

        private void OnEnable()
        {
            if (tools != null && tools.Session != null)
            {
                tools.Session.CommandExecuted += OnSomethingHappened;
            }

            if (tools != null && tools.Bootstrap != null)
            {
                tools.Bootstrap.MatchReplaced += Refresh;
            }
        }

        private void OnDisable()
        {
            if (tools != null && tools.Session != null)
            {
                tools.Session.CommandExecuted -= OnSomethingHappened;
            }

            if (tools != null && tools.Bootstrap != null)
            {
                tools.Bootstrap.MatchReplaced -= Refresh;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void OnSomethingHappened(
            CoH.Core.Commands.GameCommand command, CoH.Core.Commands.CommandResult result) => Refresh();

        // ------------------------------------------------------------------

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (_root == null)
            {
                return;
            }

            _root.SetActive(open);

            if (open)
            {
                ReloadReplayList();
                Refresh();
            }
        }

        /// <summary>Rebuilds every readout. Called when something changed, not on a timer.</summary>
        public void Refresh()
        {
            if (!IsOpen || tools == null)
            {
                return;
            }

            _stateText.text = tools.Session != null && tools.Session.IsReady
                ? StateDump.Readable(tools.Session.State)
                : "No match.";

            _commandText.text = DescribeCommands();
            _eventText.text = DescribeEvents();
            _statusText.text = _status;
        }

        private string DescribeCommands()
        {
            ReplayRecord record = tools.Recording;

            if (record == null || record.CommandCount == 0)
            {
                return "No commands recorded yet.";
            }

            StringBuilder text = new StringBuilder();
            text.Append("Source: ").Append(tools.SourceDescription).Append('\n');
            text.Append("Commands: ").Append(record.CommandCount).Append("\n\n");

            int from = Mathf.Max(0, record.Entries.Count - 14);

            for (int index = from; index < record.Entries.Count; index++)
            {
                ReplayEntry entry = record.Entries[index];

                text.Append('#').Append(entry.Sequence).Append(' ')
                    .Append(entry.Command.Describe()).Append('\n');

                text.Append("    ").Append(entry.Accepted ? "Accepted" : "Rejected " + entry.Reason)
                    .Append("  events=").Append(entry.EventCount)
                    .Append("  state=").Append(Short(entry.StateFingerprint))
                    .Append('\n');
            }

            return text.ToString();
        }

        private string DescribeEvents()
        {
            IReadOnlyList<string> events = tools.EventHistory;

            if (events == null || events.Count == 0)
            {
                return "No events yet.";
            }

            StringBuilder text = new StringBuilder();
            int from = Mathf.Max(0, events.Count - 22);

            for (int index = from; index < events.Count; index++)
            {
                text.Append(events[index]).Append('\n');
            }

            return text.ToString();
        }

        private static string Short(string fingerprint) =>
            string.IsNullOrEmpty(fingerprint) ? "-" : fingerprint.Substring(0, 8);

        // ------------------------------------------------------------------
        //  Buttons
        // ------------------------------------------------------------------

        private void ExportReplay()
        {
            string path = tools.ExportCurrentReplay();
            _status = path.Length == 0 ? "Nothing to export." : "Exported to " + path;
            ReloadReplayList();
            Refresh();
        }

        private void ExportDump()
        {
            string path = tools.ExportStateDump();
            _status = path.Length == 0 ? "No match to dump." : "State written to " + path;
            Refresh();
        }

        private void CopyDump()
        {
            if (tools.Session != null && tools.Session.IsReady)
            {
                GUIUtility.systemCopyBuffer = StateDump.Readable(tools.Session.State);
                _status = "State dump copied to the clipboard.";
            }

            Refresh();
        }

        private void VerifyCurrent()
        {
            ReplayVerificationResult result = tools.VerifyCurrentReplay();
            _status = result.Describe();
            Refresh();
        }

        private void VerifySelected()
        {
            if (_selectedReplay.Length == 0)
            {
                _status = "Select a replay first.";
                Refresh();
                return;
            }

            try
            {
                _status = tools.Verify(ReplayFiles.Load(_selectedReplay)).Describe();
            }
            catch (System.Exception error)
            {
                _status = "That replay could not be read: " + error.Message;
            }

            Refresh();
        }

        private void PlaySelected()
        {
            if (_selectedReplay.Length == 0)
            {
                _status = "Select a replay first.";
                Refresh();
                return;
            }

            try
            {
                ReplayRecord record = ReplayFiles.Load(_selectedReplay);
                _status = "Replaying " + record.CommandCount + " commands.";
                tools.PlayReplay(record);
            }
            catch (System.Exception error)
            {
                _status = "That replay could not be read: " + error.Message;
            }

            Refresh();
        }

        private void ReloadReplayList()
        {
            if (_replayList == null)
            {
                return;
            }

            for (int index = _replayList.childCount - 1; index >= 0; index--)
            {
                Destroy(_replayList.GetChild(index).gameObject);
            }

            _replayPaths.Clear();
            _replayPaths.AddRange(ReplayFiles.List());

            for (int index = 0; index < _replayPaths.Count && index < 8; index++)
            {
                string path = _replayPaths[index];
                string name = System.IO.Path.GetFileName(path);

                Button(_replayList, name, () =>
                {
                    _selectedReplay = path;
                    _status = "Selected " + System.IO.Path.GetFileName(path);
                    Refresh();
                });
            }

            if (_replayPaths.Count == 0)
            {
                Label(_replayList, "No replays in " + ReplayFiles.Folder);
            }
        }

        private void SetSpeed(float speed, bool instant)
        {
            if (tools.Timing != null)
            {
                tools.Timing.SetPlayback(speed, instant);
            }

            _status = instant ? "Presentation is instant." : "Presentation speed is " + speed + "x.";
            Refresh();
        }

        // ------------------------------------------------------------------
        //  Construction
        // ------------------------------------------------------------------

        private void Build()
        {
            _root = new GameObject("DebugOverlayCanvas");
            _root.transform.SetParent(transform, false);

            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the match HUD, which uses the default order.
            canvas.sortingOrder = 100;

            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _root.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            RectTransform backdrop = Panel(_root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Color(0.04f, 0.04f, 0.06f, 0.92f));

            RectTransform left = Column(backdrop, 0.00f, 0.30f, "STATE");
            _stateText = Scrollable(left);

            RectTransform middle = Column(backdrop, 0.30f, 0.58f, "COMMANDS");
            _commandText = Scrollable(middle);

            RectTransform right = Column(backdrop, 0.58f, 0.80f, "EVENTS");
            _eventText = Scrollable(right);

            RectTransform actions = Column(backdrop, 0.80f, 1.00f, "TOOLS");
            BuildActions(actions);

            RectTransform statusBar = Panel(backdrop, new Vector2(0f, 0f), new Vector2(1f, 0.05f),
                new Color(0.10f, 0.10f, 0.14f, 1f));

            _statusText = Label(statusBar, _status);
            _statusText.alignment = TextAlignmentOptions.Left;
        }

        private void BuildActions(RectTransform parent)
        {
            Scroller(parent, out RectTransform content);

            // Anchored to the top and grown downward by its fitter, so the list
            // of buttons is as tall as it needs to be rather than stretched.
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Label(content, "MATCH");
            Button(content, "Restart Match", () => { tools.RestartMatch(); _status = "Fresh match."; Refresh(); });

            Label(content, "SCENARIOS");

            foreach (DebugScenario scenario in DebugScenarios.All)
            {
                string id = scenario.Id;

                Button(content, id, () =>
                {
                    _status = tools.LoadScenario(id)
                        ? "Loaded scenario " + id
                        : "Scenario " + id + " could not be loaded.";

                    Refresh();
                });
            }

            Label(content, "REPLAY");
            Button(content, "Export Replay", ExportReplay);
            Button(content, "Verify Current Replay", VerifyCurrent);
            Button(content, "Reload Replay List", () => { ReloadReplayList(); Refresh(); });
            Button(content, "Play Selected", PlaySelected);
            Button(content, "Verify Selected", VerifySelected);

            _replayList = SubList(content);

            Label(content, "STATE");
            Button(content, "Copy State Dump", CopyDump);
            Button(content, "Export State Dump", ExportDump);

            Label(content, "SPEED");
            Button(content, "1x", () => SetSpeed(1f, false));
            Button(content, "2x", () => SetSpeed(2f, false));
            Button(content, "4x", () => SetSpeed(4f, false));
            Button(content, "Instant", () => SetSpeed(1f, true));
        }

        private static RectTransform Panel(Transform parent, Vector2 min, Vector2 max, Color colour)
        {
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.AddComponent<Image>().color = colour;
            return rect;
        }

        private RectTransform Column(RectTransform parent, float from, float to, string title)
        {
            RectTransform column = Panel(parent, new Vector2(from, 0.05f), new Vector2(to, 1f),
                new Color(0.07f, 0.07f, 0.10f, 1f));

            RectTransform header = Panel(column, new Vector2(0f, 0.96f), new Vector2(1f, 1f),
                new Color(0.16f, 0.16f, 0.22f, 1f));

            Label(header, title).fontStyle = FontStyles.Bold;
            return column;
        }

        private TextMeshProUGUI Scrollable(RectTransform column)
        {
            Scroller(column, out RectTransform content);

            TextMeshProUGUI text = Label(content, string.Empty);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(6f, 6f);
            text.rectTransform.offsetMax = new Vector2(-6f, -6f);

            return text;
        }

        private static RectTransform Scroller(RectTransform parent, out RectTransform content)
        {
            RectTransform view = Panel(parent, new Vector2(0f, 0f), new Vector2(1f, 0.96f),
                new Color(0f, 0f, 0f, 0f));

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(view, false);

            content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            return view;
        }

        private static RectTransform SubList(RectTransform parent)
        {
            GameObject list = new GameObject("ReplayList", typeof(RectTransform));
            list.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)list.transform;
            rect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;

            ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement element = list.AddComponent<LayoutElement>();
            element.minHeight = 20f;

            return rect;
        }

        private static TextMeshProUGUI Label(Transform parent, string value)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = FontSize;
            text.color = new Color(0.86f, 0.88f, 0.92f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.text = value;

            LayoutElement element = textObject.AddComponent<LayoutElement>();
            element.minHeight = 18f;
            element.preferredHeight = 18f;

            return text;
        }

        private static void Button(Transform parent, string caption, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject("Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.22f, 0.24f, 0.32f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.minHeight = 24f;
            element.preferredHeight = 24f;

            TextMeshProUGUI label = Label(buttonObject.transform, caption);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4f, 0f);
            label.rectTransform.offsetMax = new Vector2(-4f, 0f);
        }
    }

}
