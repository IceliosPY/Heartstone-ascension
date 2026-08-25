using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Builds a card out of nothing and shows what it would look like.
    ///
    /// A development tool, not a card creator for players and not the beginning
    /// of a deck builder. It exists to answer one question quickly — what does
    /// this combination compose to, and what is still missing — without loading
    /// a scene or dealing a hand.
    ///
    /// The important part is what it does not contain: any drawing. It builds a
    /// <see cref="CardVisualDescriptor"/> and asks the same
    /// <see cref="CardVisualFactory"/> the game asks, gets the same
    /// <see cref="CardVisualPlan"/>, and hands it to the same
    /// <see cref="CardVisualPainter"/> on a hidden object. A preview is
    /// therefore not an approximation of what the game will draw. It is the
    /// same composition, rendered by the same code, and the two cannot drift
    /// apart because there is only one of them.
    /// </summary>
    public sealed class CardVisualPreviewWindow : EditorWindow
    {
        private CardVisualFactory _factory;

        private CardType _type = CardType.Minion;
        private CardClass _class = CardClass.Neutral;
        private Rarity _rarity = Rarity.Common;
        private Tribe _tribe = Tribe.None;
        private Sprite _artwork;
        private string _name = "Test Soldier";
        private string _rules = "";
        private int _cost = 2;
        private int _attack = 2;
        private int _health = 3;
        private bool _faceDown;

        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private GameObject _stage;
        private CardVisualPainter _painter;
        private Camera _camera;
        private RenderTexture _target;

        private Vector2 _scroll;

        [MenuItem("Tools/Conquest of Hearthstone/Card Visual Preview")]
        public static void Open()
        {
            CardVisualPreviewWindow window = GetWindow<CardVisualPreviewWindow>("Card Visual");
            window.minSize = new Vector2(560f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_factory == null)
            {
                _factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);
            }
        }

        private void OnDisable() => TearDownStage();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawControls();
                DrawPreview();
            }
        }

        // ------------------------------------------------------------------
        //  The card being described
        // ------------------------------------------------------------------

        private void DrawControls()
        {
            using (EditorGUILayout.ScrollViewScope scroll =
                new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Width(280f)))
            {
                _scroll = scroll.scrollPosition;

                _factory = (CardVisualFactory)EditorGUILayout.ObjectField(
                    "Factory", _factory, typeof(CardVisualFactory), false);

                EditorGUILayout.Space();

                _type = (CardType)EditorGUILayout.EnumPopup("Type", _type);
                _class = (CardClass)EditorGUILayout.EnumPopup("Class", _class);
                _rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", _rarity);
                _tribe = (Tribe)EditorGUILayout.EnumPopup("Tribe", _tribe);

                EditorGUILayout.Space();

                _artwork = (Sprite)EditorGUILayout.ObjectField("Artwork", _artwork, typeof(Sprite), false);
                _name = EditorGUILayout.TextField("Name", _name);

                EditorGUILayout.LabelField("Rules Text");
                _rules = EditorGUILayout.TextArea(_rules, GUILayout.Height(48f));

                EditorGUILayout.Space();

                _cost = EditorGUILayout.IntField("Mana", _cost);
                _attack = EditorGUILayout.IntField("Attack", _attack);
                _health = EditorGUILayout.IntField("Health", _health);

                EditorGUILayout.Space();
                _faceDown = EditorGUILayout.Toggle("Face down", _faceDown);

                EditorGUILayout.Space();
                DrawReport();
            }
        }

        private CardVisualDescriptor Describe() =>
            new CardVisualDescriptor(
                _type,
                _class,
                _rarity,
                _tribe,
                _artwork != null || _factory == null || _factory.Library == null
                    ? _artwork
                    : _factory.Library.ArtworkFor(default),
                _name,
                _rules,
                _cost,
                _attack,
                _health,
                showsCost: true,
                showsStatistics: _type == CardType.Minion || _type == CardType.Weapon,
                faceDown: _faceDown);

        // ------------------------------------------------------------------
        //  What came of it
        // ------------------------------------------------------------------

        private void DrawReport()
        {
            if (_factory == null)
            {
                EditorGUILayout.HelpBox(
                    "No factory. Run Conquest of Hearthstone → Rebuild Card Visuals.",
                    MessageType.Warning);
                return;
            }

            CardVisualDescriptor card = Describe();
            _factory.Compose(card, _plan);

            EditorGUILayout.LabelField(
                _plan.Layers.Count + " layer(s)", EditorStyles.miniBoldLabel);

            if (_plan.IsComplete)
            {
                EditorGUILayout.HelpBox("Composed completely.", MessageType.Info);
            }
            else
            {
                List<string> lines = new List<string>();

                for (int index = 0; index < _plan.Gaps.Count; index++)
                {
                    lines.Add(_plan.Gaps[index].Describe());
                }

                EditorGUILayout.HelpBox(
                    "Missing:\n" + string.Join("\n", lines), MessageType.Warning);
            }

            EditorGUILayout.LabelField(_plan.Describe(), EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------
        //  Drawing it, with the game's own painter
        // ------------------------------------------------------------------

        private void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(
                240f, 4000f, 320f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type != EventType.Repaint || _factory == null)
            {
                return;
            }

            EnsureStage();

            _factory.Compose(Describe(), _plan);
            _painter.Apply(_plan);

            int width = Mathf.Max(64, Mathf.RoundToInt(area.height * (CardCanvas.Width / CardCanvas.Height)));
            int height = Mathf.Max(64, Mathf.RoundToInt(area.height));

            if (_target == null || _target.width != width || _target.height != height)
            {
                if (_target != null)
                {
                    _target.Release();
                    DestroyImmediate(_target);
                }

                _target = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave };
            }

            _camera.targetTexture = _target;
            _camera.Render();
            _camera.targetTexture = null;

            Rect card = new Rect(
                area.x + (area.width - width) * 0.5f, area.y, width, height);

            GUI.DrawTexture(card, _target, ScaleMode.ScaleToFit, false);
        }

        /// <summary>
        /// A hidden object with the game's painter on it, and a camera pointed
        /// at it. Nothing about how a card is composed lives here; this is a
        /// place to stand and a lens to look through.
        /// </summary>
        private void EnsureStage()
        {
            if (_stage != null)
            {
                return;
            }

            _stage = EditorUtility.CreateGameObjectWithHideFlags(
                "Card Visual Preview", HideFlags.HideAndDontSave);

            GameObject card = new GameObject("Card") { hideFlags = HideFlags.HideAndDontSave };
            card.transform.SetParent(_stage.transform, false);
            _painter = card.AddComponent<CardVisualPainter>();

            GameObject eye = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            eye.transform.SetParent(_stage.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -3f);

            _camera = eye.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = CardCanvas.CardHeight * 0.52f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            _camera.enabled = false;

            // Somewhere the scene is not, so a preview never renders the game.
            _stage.transform.position = new Vector3(10000f, 10000f, 10000f);
            _camera.cullingMask = ~0;
        }

        private void TearDownStage()
        {
            if (_target != null)
            {
                _target.Release();
                DestroyImmediate(_target);
                _target = null;
            }

            if (_stage != null)
            {
                DestroyImmediate(_stage);
                _stage = null;
                _painter = null;
                _camera = null;
            }
        }
    }
}
