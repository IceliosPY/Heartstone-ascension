using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Data;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Authors what a card looks like.
    ///
    /// The window knows almost nothing about cards. It asks
    /// <see cref="CardVisualSchema"/> what can be edited, asks the recipe which
    /// layers a card has, and asks <see cref="CardVisualPropertyField"/> to draw
    /// whatever comes back. There is no list of layer names in this file and no
    /// branch on card type, which is the property the whole thing exists for: a
    /// visual element added to the data six months from now appears here without
    /// anybody opening this file.
    ///
    /// Two scopes, and the difference between them is the difference between a
    /// change to a kind of card and a change to one card:
    ///
    ///   Type profile - the recipe. Every card the layer's conditions admit.
    ///   Card         - one sparse row per property that card does differently.
    ///
    /// And three ways of looking at the result, which are presentations of one
    /// composition rather than three styles: flat and large for editing, and the
    /// two the game actually draws, run through the game's own fan and the real
    /// prefab's hover.
    /// </summary>
    public sealed class CardVisualEditorWindow : EditorWindow
    {
        private enum Scope
        {
            TypeProfile = 0,
            Card = 1
        }

        private enum Look
        {
            General = 0,
            HandRest = 1,
            HandHover = 2
        }

        private CardVisualFactory _factory;

        // --- what is being shown -------------------------------------------
        private CardType _type = CardType.Minion;
        private CardClass _class = CardClass.Neutral;
        private Rarity _rarity = Rarity.Common;
        private CardDefinitionAsset _card;
        private string _name = "Test Soldier";
        private string _rules = "Battlecry: Deal 2 damage to an enemy character.";

        // --- how it is being shown ------------------------------------------
        private Look _look = Look.General;
        private bool _dimmed;
        private HandPresentation.Place _place = HandPresentation.Place.Centre;
        private int _handSize = 5;

        // --- what is being edited -------------------------------------------
        private Scope _scope = Scope.TypeProfile;
        // The stable id of the layer being edited, never its label: selecting a
        // layer must survive somebody renaming it in the same session.
        private string _layerId = string.Empty;

        private Vector2 _layerScroll;
        private Vector2 _propertyScroll;

        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private GameObject _stage;
        private CardVisualPainter _painter;
        private CardView _view;
        private Camera _camera;
        private RenderTexture _target;

        private readonly AdvancedDropdownState _pickerState = new AdvancedDropdownState();

        /// <summary>Why the preview cannot be drawn at all, or null.</summary>
        private string _cannotDraw;

        /// <summary>What is wrong with the authored data, or null until asked.</summary>
        private List<string> _problems;

        [MenuItem("Tools/Conquest of Hearthstone/Card Visual Editor")]
        public static void Open()
        {
            CardVisualEditorWindow window = GetWindow<CardVisualEditorWindow>("Card Visual Editor");
            window.minSize = new Vector2(1080f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            _factory ??= AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                CardVisualSetup.FactoryAssetPath);

            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            TearDown();
        }

        // ------------------------------------------------------------------
        //  The card being looked at
        // ------------------------------------------------------------------

        private CardVisualLibraryAsset Library => _factory == null ? null : _factory.Library;

        private CardVisualRecipeAsset Recipe =>
            _factory == null ? null : _factory.RecipeFor(CardVisualStyle.Default);

        /// <summary>
        /// What the composer is asked to draw.
        ///
        /// A real card when one is chosen — its own data, its own adjustments —
        /// and otherwise a made up one from the fields above. The made up card
        /// is preview content and belongs to nobody: editing it changes what is
        /// on screen and never touches a card definition.
        /// </summary>
        private CardVisualDescriptor Describe()
        {
            if (_card != null)
            {
                return CardVisualSelection.Describe(_card, Library);
            }

            return new CardVisualDescriptor(
                _type, _class, _rarity, Tribe.None,
                Library != null ? Library.ArtworkFor(default) : null,
                _name, _rules, 3, 2, 2,
                showsCost: true,
                showsStatistics: _type == CardType.Minion || _type == CardType.Weapon);
        }

        /// <summary>Every layer this card actually draws, in the order it draws them.</summary>
        private List<CardVisualLayerDefinition> LayersOf(in CardVisualDescriptor card)
        {
            List<CardVisualLayerDefinition> found = new List<CardVisualLayerDefinition>();

            if (Recipe == null)
            {
                return found;
            }

            for (int index = 0; index < Recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = Recipe.Layers[index];

                if (layer != null && layer.AppliesTo(card))
                {
                    found.Add(layer);
                }
            }

            found.Sort((left, right) => left.sortingOrder.CompareTo(right.sortingOrder));
            return found;
        }

        // ------------------------------------------------------------------
        //  Drawing the window
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (_factory == null)
            {
                EditorGUILayout.HelpBox(
                    "No card visual factory at " + CardVisualSetup.FactoryAssetPath + ".",
                    MessageType.Error);

                _factory = (CardVisualFactory)EditorGUILayout.ObjectField(
                    "Factory", _factory, typeof(CardVisualFactory), false);

                return;
            }

            DrawTopBar();
            DrawProblems();

            CardVisualDescriptor card = Describe();
            List<CardVisualLayerDefinition> layers = LayersOf(card);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawScopeAndLayers(layers);
                DrawPreview(card);
                DrawProperties(card, layers);
            }
        }

        /// <summary>
        /// Anything wrong with the authored data, above the tool that authored
        /// it.
        ///
        /// Re-checked only when something has been saved, because validating
        /// the whole library on every repaint would make the window cost more
        /// than the game. The point is that an adjustment pointing at nothing
        /// is visible here rather than discovered a month later as a card that
        /// would not take polish.
        /// </summary>
        private void DrawProblems()
        {
            if (_problems == null)
            {
                _problems = new List<string>();
                CardVisualDataValidator.Validate(_factory, _problems);
            }

            if (_problems.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                _problems.Count + " problem(s) with the authored card visual data:" +
                Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", _problems),
                MessageType.Warning);
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _look = (Look)EditorGUILayout.EnumPopup(
                    _look, EditorStyles.toolbarPopup, GUILayout.Width(110f));

                if (_look != Look.General)
                {
                    _place = (HandPresentation.Place)EditorGUILayout.EnumPopup(
                        _place, EditorStyles.toolbarPopup, GUILayout.Width(80f));

                    _handSize = EditorGUILayout.IntSlider(_handSize, 1, 10, GUILayout.Width(140f));
                }

                _dimmed = GUILayout.Toggle(
                    _dimmed, "Unplayable", EditorStyles.toolbarButton, GUILayout.Width(90f));

                GUILayout.FlexibleSpace();

                if (_card == null)
                {
                    _type = (CardType)EditorGUILayout.EnumPopup(
                        _type, EditorStyles.toolbarPopup, GUILayout.Width(90f));
                }

                DrawCardPicker();
            }
        }

        /// <summary>
        /// The one control that chooses what the editor is looking at: a made
        /// up preview, or a real card by name.
        ///
        /// A single button that always does the same thing when clicked - opens
        /// a searchable list of every real card, with an entry at the top to
        /// go back to the made up preview. There used to be two controls here,
        /// a button that only ever cleared the selection and a separate search
        /// field below that actually picked one; the button's own label lied
        /// about what it did whenever no card was already chosen, which is
        /// indistinguishable from doing nothing. One control, one job.
        /// </summary>
        private void DrawCardPicker()
        {
            GUIContent label = new GUIContent(_card == null ? "Pick a card..." : _card.DisplayName);
            Rect rect = GUILayoutUtility.GetRect(
                label, EditorStyles.toolbarDropDown, GUILayout.Width(170f));

            if (GUI.Button(rect, label, EditorStyles.toolbarDropDown))
            {
                new CardPickerDropdown(_pickerState, CardRoster.All(), PickCard).Show(rect);
            }

            if (GUILayout.Button(
                new GUIContent("↻", "Rescan the project for cards (only needed after adding one)."),
                EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                CardRoster.Invalidate();
            }
        }

        /// <summary>
        /// What the dropdown calls back with: the chosen card, or null for the
        /// made up preview. Never told which card it replaces - that is read
        /// straight from <see cref="_card"/> everywhere else, so nothing here
        /// can leave a stale reference behind.
        /// </summary>
        private void PickCard(CardDefinitionAsset picked)
        {
            _card = picked;
            Repaint();
        }

        // ------------------------------------------------------------------
        //  Left: scope, layers
        // ------------------------------------------------------------------

        private void DrawScopeAndLayers(List<CardVisualLayerDefinition> layers)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(270f)))
            {
                DrawScope();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

                using (EditorGUILayout.ScrollViewScope scroll =
                    new EditorGUILayout.ScrollViewScope(_layerScroll))
                {
                    _layerScroll = scroll.scrollPosition;

                    for (int index = 0; index < layers.Count; index++)
                    {
                        CardVisualLayerDefinition layer = layers[index];

                        bool selected = string.Equals(layer.LayerId, _layerId, System.StringComparison.Ordinal);
                        int adjusted = AdjustmentsTo(layer.LayerId);

                        string label = layer.name + (adjusted > 0 ? "   (" + adjusted + ")" : string.Empty);

                        if (GUILayout.Toggle(selected, label, EditorStyles.miniButton) && !selected)
                        {
                            _layerId = layer.LayerId;
                        }
                    }

                    if (layers.Count == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "This card draws nothing. Its conditions admit no layer.",
                            MessageType.Info);
                    }
                }
            }
        }

        private void DrawScope()
        {
            EditorGUILayout.LabelField("Editing", EditorStyles.boldLabel);

            Scope wanted = (Scope)GUILayout.Toolbar((int)_scope, new[] { "Type profile", "This card" });

            if (wanted == Scope.Card && _card == null)
            {
                EditorGUILayout.HelpBox(
                    "Pick a real card above to give it adjustments of its own.", MessageType.Info);

                wanted = Scope.TypeProfile;
            }

            _scope = wanted;

            // Loud, because the whole risk of a tool like this is believing one
            // scope is the other and quietly retuning every card of a kind.
            Color was = GUI.backgroundColor;

            GUI.backgroundColor = _scope == Scope.Card
                ? new Color(1f, 0.85f, 0.4f)
                : new Color(0.5f, 0.8f, 1f);

            EditorGUILayout.HelpBox(
                _scope == Scope.Card
                    ? "Changes affect " + (_card == null ? "this card" : _card.DisplayName) +
                      " alone, as sparse adjustments over its profile."
                    : "Changes affect the recipe: every card whose conditions select these layers.",
                MessageType.None);

            GUI.backgroundColor = was;
        }

        // ------------------------------------------------------------------
        //  Right: the selected layer's properties
        // ------------------------------------------------------------------

        private int AdjustmentsTo(string layer)
        {
            CardVisualOverrides overrides = OverridesOfTheCard();

            if (overrides == null)
            {
                return 0;
            }

            int count = 0;

            for (int index = 0; index < overrides.Properties.Count; index++)
            {
                if (overrides.Properties[index] != null &&
                    string.Equals(overrides.Properties[index].layer, layer, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private CardVisualOverrides OverridesOfTheCard() =>
            _card == null || Library == null ? null : Library.OverridesFor(_card.Id);

        private void DrawProperties(in CardVisualDescriptor card, List<CardVisualLayerDefinition> layers)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(340f)))
            {
                CardVisualLayerDefinition layer = layers.Find(
                    candidate => string.Equals(candidate.LayerId, _layerId, System.StringComparison.Ordinal));

                if (layer == null)
                {
                    EditorGUILayout.HelpBox("Pick a layer on the left.", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField(layer.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Shown when", layer.Describe(), EditorStyles.miniLabel);

                using (EditorGUILayout.ScrollViewScope scroll =
                    new EditorGUILayout.ScrollViewScope(_propertyScroll))
                {
                    _propertyScroll = scroll.scrollPosition;

                    DrawGroup(layer, CardVisualPropertyOwner.Layer, layer);

                    CardTextStyleDefinition style = Recipe.TextStyleFor(layer);

                    if (style != null)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField(
                            "Style: " + style.name +
                            (_scope == Scope.TypeProfile ? "   (shared by every layer set in it)" : ""),
                            EditorStyles.boldLabel);

                        DrawGroup(layer, CardVisualPropertyOwner.Style, style);
                    }

                    DrawResetButtons(layer);
                }
            }
        }

        /// <summary>
        /// Every property of one owner, generated from the schema.
        ///
        /// The loop is the point: it does not know what it is drawing, only what
        /// type each thing is and where its value came from.
        /// </summary>
        private void DrawGroup(
            CardVisualLayerDefinition layer, CardVisualPropertyOwner owner, object authored)
        {
            string group = null;

            foreach (CardVisualProperty property in CardVisualSchema.For(owner))
            {
                if (!string.Equals(property.Group, group, System.StringComparison.Ordinal))
                {
                    group = property.Group;

                    if (!string.IsNullOrEmpty(group))
                    {
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                    }
                }

                DrawProperty(layer, property, authored);
            }
        }

        private void DrawProperty(
            CardVisualLayerDefinition layer, CardVisualProperty property, object authored)
        {
            CardVisualOverrides overrides = OverridesOfTheCard();

            CardVisualResolved resolved = CardVisualInheritance.Resolve(
                property, authored, layer.LayerId, Recipe == null ? string.Empty : Recipe.name, overrides);

            bool overridden = resolved.Source == CardVisualSource.CardOverride;
            bool canOverride = _scope == Scope.Card && property.SupportsCardOverride && _card != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (canOverride)
                {
                    bool wanted = EditorGUILayout.Toggle(overridden, GUILayout.Width(16f));

                    if (wanted != overridden)
                    {
                        if (wanted)
                        {
                            // Starts from what it already was, so switching an
                            // adjustment on never moves anything by itself.
                            Record("Adjust " + property.DisplayName);
                            Establish().Set(
                                layer.LayerId, property, CardVisualValue.Of(resolved.Value));
                        }
                        else
                        {
                            Record("Reset " + property.DisplayName);
                            Establish().Clear(layer.LayerId, property);
                        }

                        Save();
                        return;
                    }
                }
                else
                {
                    GUILayout.Space(canOverride ? 16f : 0f);
                }

                // What the contract allows, rather than what the panel happens
                // to be showing. A structural property is real profile
                // authoring and stays editable there; an identity or an
                // unsupported one is never editable anywhere, because changing
                // it either breaks what points at it or does nothing at all.
                bool editable = _scope == Scope.TypeProfile
                    ? property.SupportsProfileEdit
                    : overridden;

                using (new EditorGUI.DisabledScope(!editable))
                {
                    EditorGUI.BeginChangeCheck();

                    object changed = CardVisualPropertyField.Draw(property, resolved.Value);

                    if (EditorGUI.EndChangeCheck() && !Equals(changed, resolved.Value))
                    {
                        if (_scope == Scope.Card)
                        {
                            Record("Adjust " + property.DisplayName);
                            Establish().Set(layer.LayerId, property, CardVisualValue.Of(changed));
                        }
                        else
                        {
                            Record("Edit " + property.DisplayName);
                            property.Write(authored, changed);
                            EditorUtility.SetDirty(Recipe);
                        }

                        Save();
                    }
                }
            }

            // Where the value came from. Compact, and never absent: on a large
            // roster "why is it this" is the question that costs the most time.
            //
            // And, for anything a card may not differ on, why not. A control
            // that is greyed out with no reason given is indistinguishable from
            // one that is broken.
            string provenance = overridden ? "This card" : resolved.Describe();

            EditorGUILayout.LabelField(
                " ",
                property.Authorability == CardVisualAuthorability.PerCard
                    ? provenance
                    : provenance + "   -   " + Why(property),
                overridden ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
        }

        /// <summary>Why a property is not freely editable, in as few words as fit.</summary>
        private static string Why(CardVisualProperty property)
        {
            if (!string.IsNullOrEmpty(property.Note))
            {
                return property.Note;
            }

            switch (property.Authorability)
            {
                case CardVisualAuthorability.ProfileOnly:
                    return "profile only";

                case CardVisualAuthorability.Structural:
                    return "structural: settled before a card's own adjustments";

                case CardVisualAuthorability.Unsupported:
                    return "authored but read by nothing";

                case CardVisualAuthorability.Identity:
                    return "identity: other data points at this";

                default:
                    return string.Empty;
            }
        }

        private void DrawResetButtons(CardVisualLayerDefinition layer)
        {
            if (_scope != Scope.Card || _card == null)
            {
                return;
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                CardVisualOverrides overrides = OverridesOfTheCard();

                using (new EditorGUI.DisabledScope(AdjustmentsTo(layer.LayerId) == 0))
                {
                    if (GUILayout.Button("Reset layer"))
                    {
                        Record("Reset " + layer.name);
                        Establish().ClearLayer(layer.LayerId);
                        Save();
                    }
                }

                using (new EditorGUI.DisabledScope(overrides == null || overrides.IsEmpty))
                {
                    if (GUILayout.Button("Reset card") &&
                        EditorUtility.DisplayDialog(
                            "Reset every adjustment?",
                            _card.DisplayName + " will go back to composing exactly as its " +
                            "profile says. This can be undone.",
                            "Reset", "Keep"))
                    {
                        Record("Reset " + _card.DisplayName);
                        Establish().Clear();
                        Save();
                    }
                }
            }
        }

        private CardVisualOverrides Establish() => CardVisualSelection.Adjustments(_card, Library);

        private void Record(string what) =>
            Undo.RecordObject(_scope == Scope.Card ? (UnityEngine.Object)Library : Recipe, what);

        private void Save()
        {
            EditorUtility.SetDirty(_scope == Scope.Card ? (UnityEngine.Object)Library : Recipe);

            // Whatever was just written may have introduced a problem, and
            // this is the moment somebody can still connect the two.
            _problems = null;

            Repaint();
        }

        // ------------------------------------------------------------------
        //  Centre: the card itself
        // ------------------------------------------------------------------

        private void DrawPreview(in CardVisualDescriptor card)
        {
            if (_cannotDraw != null)
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.HelpBox(_cannotDraw, MessageType.Error);

                    if (GUILayout.Button("Try again"))
                    {
                        _cannotDraw = null;
                        TearDown();
                    }
                }

                return;
            }

            Rect area = GUILayoutUtility.GetRect(
                260f, 4000f, 320f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            int width = Mathf.Max(64, Mathf.RoundToInt(area.width));
            int height = Mathf.Max(64, Mathf.RoundToInt(area.height));

            // A preview that cannot use the real prefab is not a preview of
            // anything, so it says so where the picture would have been rather
            // than drawing a plausible card in the wrong fonts.
            try
            {
                Stage(card);
            }
            catch (MissingCardPrefabException missing)
            {
                _cannotDraw = missing.Message;
                return;
            }

            _cannotDraw = null;

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

            GUI.DrawTexture(area, _target, ScaleMode.ScaleToFit, false);
        }

        /// <summary>
        /// Composes the card and puts it in front of the camera, ready to be
        /// rendered.
        ///
        /// Shared by the window's own viewport and by the captures, so a still
        /// written to disk is the same picture the window shows rather than a
        /// second implementation that agrees with it today.
        /// </summary>
        private void Stage(in CardVisualDescriptor card)
        {
            EnsureStage();

            _factory.Compose(card, _plan);
            _painter.Apply(_plan);
            _painter.SetDimmed(_dimmed);

            Frame();
        }

        /// <summary>
        /// Writes what the window would be showing to a file.
        ///
        /// Through a real instance of the window, with the window's own stage
        /// and the window's own framing. A capture taken any other way proves
        /// something about the capture code.
        /// </summary>
        internal static void Capture(
            in CardVisualDescriptor card,
            string path,
            int width = 420,
            bool inHand = false,
            bool hovered = false,
            bool dimmed = false,
            HandPresentation.Place place = HandPresentation.Place.Centre,
            int handSize = 5)
        {
            CardVisualEditorWindow window = CreateInstance<CardVisualEditorWindow>();

            try
            {
                window._factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                    CardVisualSetup.FactoryAssetPath);

                if (window._factory == null)
                {
                    Debug.LogError("No card visual factory to capture with.");
                    return;
                }

                window._look = !inHand
                    ? Look.General
                    : hovered ? Look.HandHover : Look.HandRest;

                window._dimmed = dimmed;
                window._place = place;
                window._handSize = handSize;

                // The artwork a card with none of its own would really be
                // given, so a capture is not emptier than the window.
                window.Stage(
                    card.HasArtwork || window.Library == null
                        ? card
                        : card.With(window.Library.ArtworkFor(default)));

                int height = inHand
                    ? Mathf.RoundToInt(width * 0.62f)
                    : Mathf.RoundToInt(width * (CardCanvas.Height / CardCanvas.Width));

                RenderTexture target = new RenderTexture(width, height, 24)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                window._camera.targetTexture = target;

                // Twice: the first render of a batch session can land before
                // the shaders are ready and produce a black frame that looks
                // exactly like a composition bug.
                window._camera.Render();
                window._camera.Render();

                window._camera.targetTexture = null;

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;

                Texture2D picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();

                RenderTexture.active = previous;

                System.IO.File.WriteAllBytes(path, picture.EncodeToPNG());

                DestroyImmediate(picture);
                target.Release();
                DestroyImmediate(target);
            }
            finally
            {
                window.TearDown();
                DestroyImmediate(window);
            }
        }

        /// <summary>
        /// Puts the card where the chosen way of looking at it puts it.
        ///
        /// General is flat and square to the camera, which is what makes it the
        /// one to edit in. The other two are the game's: the pose comes from
        /// <see cref="HandFanLayout"/> and the hover from the prefab, so what is
        /// on screen is what a player would see rather than an impression of it.
        /// </summary>
        private void Frame()
        {
            bool inHand = _look != Look.General;

            _hand.gameObject.SetActive(inHand);

            if (!inHand)
            {
                _view.transform.SetParent(_stage.transform, false);
                _view.transform.localPosition = Vector3.zero;
                _view.transform.localRotation = Quaternion.identity;
                _view.transform.localScale = Vector3.one;

                _camera.orthographic = true;
                _camera.orthographicSize = CardCanvas.CardHeight * 0.55f;
                _camera.transform.localPosition = new Vector3(0f, 0f, -3f);
                _camera.transform.localRotation = Quaternion.identity;

                return;
            }

            _view.transform.SetParent(_hand, false);

            int index = HandPresentation.IndexOf(_place, _handSize);

            // The match camera's own pose, so the card is foreshortened by
            // exactly the angle a player sees it at rather than an angle that
            // happens to look similar.
            _camera.orthographic = false;
            _camera.transform.localPosition = HandPresentation.EyePosition;
            _camera.transform.localRotation = Quaternion.Euler(HandPresentation.EyePitch, 0f, 0f);

            // The magnification is fixed from where the card rests, before any
            // hover, so the two hand looks are drawn at one scale and the hover
            // is visibly a magnification instead of being normalised away by a
            // camera that chases it.
            HandPresentation.Pose(_view, index, _handSize, false);
            Magnify(_view.transform.position);

            if (_look == Look.HandHover)
            {
                HandPresentation.Pose(_view, index, _handSize, true);
            }

            // Panned afterwards, because a hovered card rises far enough to
            // leave a frame built around its resting place. Panning is the one
            // move that changes nothing about how big anything is.
            Centre(_view.transform.position);
        }

        /// <summary>
        /// Magnifies the game's picture without becoming a different picture.
        ///
        /// Two moves, both of which a photographer would call a crop: narrow the
        /// field of view, and slide the camera across its own plane until the
        /// card is centred. Neither changes where the camera is looking from, so
        /// the perspective, the tilt and the foreshortening are the ones on
        /// screen in a match — just large enough to judge a typeface by.
        ///
        /// Moving the camera *toward* the card would have been easier and would
        /// have quietly flattened it.
        /// </summary>
        private void Magnify(Vector3 card)
        {
            float along = Vector3.Dot(
                card - _camera.transform.position, _camera.transform.forward);

            if (along <= 0.01f)
            {
                return;
            }

            float tall = CardCanvas.CardHeight * HandPresentation.Settings().Scale * 1.7f;

            _camera.fieldOfView =
                Mathf.Clamp(2f * Mathf.Atan2(tall * 0.5f, along) * Mathf.Rad2Deg, 1f, 120f);
        }

        /// <summary>Slides the camera across its own plane, never along it.</summary>
        private void Centre(Vector3 card)
        {
            Vector3 eye = _camera.transform.position;
            Vector3 forward = _camera.transform.forward;

            _camera.transform.position +=
                card - (eye + forward * Vector3.Dot(card - eye, forward));
        }

        private Transform _hand;

        private void EnsureStage()
        {
            if (_stage != null)
            {
                return;
            }

            _stage = EditorUtility.CreateGameObjectWithHideFlags(
                "Card Visual Editor", HideFlags.HideAndDontSave);

            _stage.transform.position = new Vector3(12000f, 12000f, 12000f);

            _hand = HandPresentation.Anchor(_stage.transform);

            _painter = CardPreviewCard.Make(_stage.transform, out GameObject card);
            _view = card.GetComponent<CardView>();

            if (_view == null)
            {
                _view = card.AddComponent<CardView>();
            }

            GameObject eye = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            eye.transform.SetParent(_stage.transform, false);

            _camera = eye.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.11f, 0.11f, 0.13f);
            _camera.enabled = false;
            _camera.cullingMask = ~0;
        }

        private void TearDown()
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
            }
        }
    }
}
