using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGamePregnancyPlusBridge
{
    public sealed partial class Plugin
    {
        private const string MotionStrengthStrong = "strong";
        private const string MotionStrengthWeak = "weak";
        private const string MotionStrengthUnknown = "unknown";

        private ConfigEntry<bool> _cfgBellyEnabled;
        private ConfigEntry<bool> _cfgBellyCaptureEnabled;
        private ConfigEntry<KeyboardShortcut> _cfgBellyCaptureMaxKey;
        private ConfigEntry<KeyboardShortcut> _cfgBellyCaptureMinKey;
        private ConfigEntry<int> _cfgBellyPresetSlot;
        private ConfigEntry<float> _cfgBellyMinInflationSize;
        private ConfigEntry<float> _cfgBellyMaxInflationSize;
        private ConfigEntry<bool> _cfgBellyAutoReturnMin;
        private ConfigEntry<float> _cfgBellyForwardMinPhase;
        private ConfigEntry<float> _cfgBellyMinHoldWidth;
        private ConfigEntry<float> _cfgBellyMaxPhase;
        private ConfigEntry<float> _cfgBellyReturnMinPhase;
        private ConfigEntry<float> _cfgBellyTimelineDisplayOffset;
        private ConfigEntry<string> _cfgBellyEaseUp;
        private ConfigEntry<string> _cfgBellyEaseDown;
        private ConfigEntry<bool> _cfgBellyProfileEnabled;
        private ConfigEntry<bool> _cfgBellyEditMode;
        private ConfigEntry<bool> _cfgBellyAutoLoadProfile;
        private ConfigEntry<bool> _cfgBellyTimelineEditor;
        private ConfigEntry<bool> _cfgBellySaveProfileNow;
        private ConfigEntry<bool> _cfgBellyLoadProfileNow;
        private ConfigEntry<string> _cfgBellyContext;
        private ConfigEntry<string> _cfgBellyMotionStrength;
        private ConfigEntry<bool> _cfgBellyDistanceMode;
        private ConfigEntry<float> _cfgBellyDistanceCutPercent;
        private ConfigEntry<float> _cfgBellyDistanceMinMeters;
        private ConfigEntry<float> _cfgBellyDistanceMaxMeters;
        private ConfigEntry<float> _cfgBellyDistanceSmoothing;
        private ConfigEntry<int> _cfgBellyDistanceAnalyzeTurns;
        private ConfigEntry<bool> _cfgBellyDistanceAnalyzeNow;

        private BellyBokoStore _bellyStore;

        private bool _hasRuntimeInflationSizeOverride;
        private float _runtimeInflationSizeOverride;
        private string _bellyCurrentAnimationKey;
        private bool _bellyEditorSyncGuard;
        private bool _bellyTriggerGuard;
        private int _bellyTimelineDragHandle = -1;
        private bool _bellyTimelineDragDirty;
        private float _bellyCurrentPhase;
        private bool _hasBellyCurrentPhase;
        private float _bellyDistanceSmoothed;
        private bool _hasBellyDistanceSmoothed;
        private bool _bellyDistanceAnalyzeActive;
        private int _bellyDistanceAnalyzeTargetTurns;
        private int _bellyDistanceAnalyzeCompletedTurns;
        private float _bellyDistanceAnalyzeMin;
        private float _bellyDistanceAnalyzeMax;
        private float _bellyDistanceAnalyzeLastPhase;
        private bool _hasBellyDistanceAnalyzeLastPhase;
        private string _bellyDistanceAnalyzeKey;
        private bool _bellyDistanceAnalyzeTriggerGuard;
        private HSceneProc _bellyHSceneProc;
        private float _nextBellyHScanTime;
        private float _nextBellyDiagLogTime;
        private string _lastBellyDiagGate = string.Empty;
        private ChaControl _bellyMaleCha;
        private ChaControl _bellyFemaleCha;
        private Transform _bellyMaleDistanceRef;
        private Transform _bellyFemaleDistanceRef;
        private readonly Dictionary<Type, FieldInfo> _bellyLstFemaleFieldCache = new Dictionary<Type, FieldInfo>();
        private readonly Dictionary<Type, MemberInfo> _bellyFlagsMemberCache = new Dictionary<Type, MemberInfo>();
        private readonly Dictionary<Type, MemberInfo> _bellyNowAnimMemberCache = new Dictionary<Type, MemberInfo>();
        private static readonly string[] BellyEaseOptions = { "linear", "easeIn", "easeOut", "smoothStep", "smootherStep" };
        private const float BellyTimelineMinGap = 0.01f;

        private static ConfigurationManager.ConfigurationManagerAttributes BellyUiOrder(int order)
        {
            return new ConfigurationManager.ConfigurationManagerAttributes
            {
                Order = order
            };
        }

        private static ConfigurationManager.ConfigurationManagerAttributes BellyUiOrderReadOnly(int order)
        {
            return new ConfigurationManager.ConfigurationManagerAttributes
            {
                Order = order,
                ReadOnly = true,
                IsAdvanced = true
            };
        }

        private void InitializeBellyBokoSystem(string pluginDir)
        {
            string storePath = System.IO.Path.Combine(pluginDir, "MainGamePregnancyPlusBridgeBellyProfiles.json");
            _bellyStore = new BellyBokoStore(storePath, LogInfo, LogWarn);

            _cfgBellyEnabled = Config.Bind(
                "10.BellyBoko",
                "Enabled",
                true,
                new ConfigDescription("Enable automatic belly reaction by animation phase", null, BellyUiOrder(999)));
            _cfgBellyCaptureEnabled = Config.Bind(
                "10.BellyBoko",
                "CaptureEnabled",
                true,
                new ConfigDescription("Enable/Disable phase capture keys", null, BellyUiOrder(984)));
            _cfgBellyCaptureMaxKey = Config.Bind(
                "10.BellyBoko",
                "CaptureMaxKey",
                new KeyboardShortcut(KeyCode.LeftControl),
                new ConfigDescription("Capture MAX point key (None disables)", null, BellyUiOrder(983)));
            _cfgBellyCaptureMinKey = Config.Bind(
                "10.BellyBoko",
                "CaptureMinKey",
                new KeyboardShortcut(KeyCode.RightControl),
                new ConfigDescription("Capture MIN point key (None disables)", null, BellyUiOrder(982)));
            _cfgBellyPresetSlot = Config.Bind(
                "10.BellyBoko",
                "PresetSlotForMax",
                1,
                new ConfigDescription("Preset slot used as max InflationSize source", new AcceptableValueRange<int>(1, 20), BellyUiOrder(985)));
            _cfgBellyMinInflationSize = Config.Bind(
                "10.BellyBoko",
                "MinInflationSize",
                0f,
                new ConfigDescription("Belly minimum InflationSize (slider + text)", new AcceptableValueRange<float>(0f, 40f), BellyUiOrder(987)));
            _cfgBellyMaxInflationSize = Config.Bind(
                "10.BellyBoko",
                "MaxInflationSize",
                5f,
                new ConfigDescription("Belly maximum InflationSize (slider + text)", new AcceptableValueRange<float>(0f, 40f), BellyUiOrder(986)));
            _cfgBellyDistanceMode = Config.Bind(
                "10.BellyBoko",
                "DistanceMode",
                false,
                new ConfigDescription("Distance mode (male/female groin distance based)", null, BellyUiOrder(978)));
            _cfgBellyDistanceCutPercent = Config.Bind(
                "10.BellyBoko",
                "DistanceCutPercent",
                0.5f,
                new ConfigDescription("Distance: ratio where Max->Min drop ends (0..1)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(977)));
            _cfgBellyDistanceMinMeters = Config.Bind(
                "10.BellyBoko",
                "DistanceMinMeters",
                0.04f,
                new ConfigDescription("Distance: min range point (meters)", new AcceptableValueRange<float>(0f, 2f), BellyUiOrder(976)));
            _cfgBellyDistanceMaxMeters = Config.Bind(
                "10.BellyBoko",
                "DistanceMaxMeters",
                0.24f,
                new ConfigDescription("Distance: max range point (meters)", new AcceptableValueRange<float>(0f, 2f), BellyUiOrder(975)));
            _cfgBellyDistanceSmoothing = Config.Bind(
                "10.BellyBoko",
                "DistanceSmoothing",
                0.35f,
                new ConfigDescription("Distance smoothing (0=no smoothing, 1=very smooth)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(974)));
            _cfgBellyDistanceAnalyzeTurns = Config.Bind(
                "10.BellyBoko",
                "DistanceAnalyzeTurns",
                2,
                new ConfigDescription("Distance analysis turns", new AcceptableValueRange<int>(1, 12), BellyUiOrder(973)));
            _cfgBellyDistanceAnalyzeNow = Config.Bind(
                "10.BellyBoko",
                "DistanceAnalyzeNow",
                false,
                new ConfigDescription(
                    "Analyze current posture distance (button)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 972,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellyDistanceAnalyzeButton
                    }));
            _cfgBellyAutoReturnMin = Config.Bind(
                "10.BellyBoko",
                "AutoReturnMin",
                true,
                new ConfigDescription("Auto-calc return-min phase from forward-min and max", null, BellyUiOrder(981)));
            _cfgBellyForwardMinPhase = Config.Bind(
                "10.BellyBoko",
                "ForwardMinPhase",
                0.15f,
                new ConfigDescription("Forward MIN phase (0..1)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(994)));
            _cfgBellyMinHoldWidth = Config.Bind(
                "10.BellyBoko",
                "MinHoldWidth",
                0f,
                new ConfigDescription("Minimum hold phase width (0..1)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(993)));
            _cfgBellyMaxPhase = Config.Bind(
                "10.BellyBoko",
                "MaxPhase",
                0.35f,
                new ConfigDescription("MAX phase (0..1)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(992)));
            _cfgBellyReturnMinPhase = Config.Bind(
                "10.BellyBoko",
                "ReturnMinPhase",
                0.55f,
                new ConfigDescription("Return MIN phase (0..1)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(991)));
            _cfgBellyTimelineDisplayOffset = Config.Bind(
                "10.BellyBoko",
                "TimelineDisplayOffset",
                0f,
                new ConfigDescription("Seekbar display offset (0..1, display only)", new AcceptableValueRange<float>(0f, 1f), BellyUiOrder(990)));
            _cfgBellyEaseUp = Config.Bind(
                "10.BellyBoko",
                "EaseUp",
                "easeOut",
                new ConfigDescription("Curve for ForwardMin->Max", new AcceptableValueList<string>(BellyEaseOptions), BellyUiOrder(989)));
            _cfgBellyEaseDown = Config.Bind(
                "10.BellyBoko",
                "EaseDown",
                "easeIn",
                new ConfigDescription("Curve for Max->ReturnMin", new AcceptableValueList<string>(BellyEaseOptions), BellyUiOrder(988)));
            _cfgBellyProfileEnabled = Config.Bind(
                "10.BellyBoko",
                "ProfileEnabled",
                true,
                new ConfigDescription("Enable current animation belly profile", null, BellyUiOrder(996)));
            _cfgBellyEditMode = Config.Bind(
                "10.BellyBoko",
                "EditMode",
                true,
                new ConfigDescription(
                    "Apply source mode: editor sliders (ON) / saved profile (OFF)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 998,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellyApplyModeToggleButton
                    }));
            _cfgBellyAutoLoadProfile = Config.Bind(
                "10.BellyBoko",
                "AutoLoadProfileOnContextChange",
                true,
                new ConfigDescription(
                    "Auto-load profile when animation context changes (button)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 997,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellyAutoLoadToggleButton
                    }));
            _cfgBellyTimelineEditor = Config.Bind(
                "10.BellyBoko",
                "TimelineEditor",
                false,
                new ConfigDescription(
                    "Phase seekbar editor (playhead + markers)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 995,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellyTimelineEditor
                    }));

            _cfgBellySaveProfileNow = Config.Bind(
                "10.BellyBoko",
                "SaveProfileNow",
                false,
                new ConfigDescription(
                    "Save current phase settings (button)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 980,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellySaveProfileButton
                    }));
            _cfgBellyLoadProfileNow = Config.Bind(
                "10.BellyBoko",
                "LoadProfileNow",
                false,
                new ConfigDescription(
                    "Load settings for current animation key (button)",
                    null,
                    new ConfigurationManager.ConfigurationManagerAttributes
                    {
                        Order = 979,
                        HideDefaultButton = true,
                        CustomDrawer = DrawBellyLoadProfileButton
                    }));

            _cfgBellyContext = Config.Bind(
                "10.BellyBoko",
                "CurrentContext",
                "(no-context)",
                new ConfigDescription(
                    "Current matching key: posture/strength/anim",
                    null,
                    BellyUiOrderReadOnly(970)));
            _cfgBellyMotionStrength = Config.Bind(
                "10.BellyBoko",
                "CurrentStrength",
                MotionStrengthUnknown,
                new ConfigDescription(
                    "Current strong/weak classification",
                    null,
                    BellyUiOrderReadOnly(969)));
            _cfgBellySaveProfileNow.SettingChanged += OnBellySaveProfileRequested;
            _cfgBellyLoadProfileNow.SettingChanged += OnBellyLoadProfileRequested;
            _cfgBellyForwardMinPhase.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyMinHoldWidth.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyMaxPhase.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyReturnMinPhase.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyEaseUp.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyEaseDown.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyProfileEnabled.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyPresetSlot.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyMinInflationSize.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyMaxInflationSize.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceMode.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceCutPercent.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceMinMeters.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceMaxMeters.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceSmoothing.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceAnalyzeTurns.SettingChanged += OnBellyEditorValueChanged;
            _cfgBellyDistanceAnalyzeNow.SettingChanged += OnBellyDistanceAnalyzeRequested;
            _cfgBellyAutoReturnMin.SettingChanged += OnBellyEditorValueChanged;

            _cfgBellyPresetSlot.Value = Mathf.Clamp(_cfgBellyPresetSlot.Value, 1, 20);
            _cfgBellyMinInflationSize.Value = Mathf.Clamp(_cfgBellyMinInflationSize.Value, 0f, 40f);
            _cfgBellyMaxInflationSize.Value = Mathf.Clamp(_cfgBellyMaxInflationSize.Value, 0f, 40f);
            _cfgBellyDistanceCutPercent.Value = Mathf.Clamp01(_cfgBellyDistanceCutPercent.Value);
            _cfgBellyDistanceMinMeters.Value = Mathf.Clamp(_cfgBellyDistanceMinMeters.Value, 0f, 2f);
            _cfgBellyDistanceMaxMeters.Value = Mathf.Clamp(_cfgBellyDistanceMaxMeters.Value, 0f, 2f);
            _cfgBellyDistanceSmoothing.Value = Mathf.Clamp01(_cfgBellyDistanceSmoothing.Value);
            _cfgBellyDistanceAnalyzeTurns.Value = Mathf.Clamp(_cfgBellyDistanceAnalyzeTurns.Value, 1, 12);
            _cfgBellyMinHoldWidth.Value = Mathf.Clamp01(_cfgBellyMinHoldWidth.Value);
            _cfgBellyTimelineDisplayOffset.Value = NormalizePhase01(_cfgBellyTimelineDisplayOffset.Value);
            _cfgBellyEaseUp.Value = NormalizeEaseName(_cfgBellyEaseUp.Value, "easeOut");
            _cfgBellyEaseDown.Value = NormalizeEaseName(_cfgBellyEaseDown.Value, "easeIn");
        }

        private void DrawBellySaveProfileButton(ConfigEntryBase entryBase)
        {
            if (GUILayout.Button("SAVE PROFILE", GUILayout.MinWidth(120f)))
                SaveBellyProfileFromEditor(forcePopup: true);
        }

        private void DrawBellyApplyModeToggleButton(ConfigEntryBase entryBase)
        {
            bool editMode = _cfgBellyEditMode == null || _cfgBellyEditMode.Value;
            string label = editMode ? "MODE: EDIT" : "MODE: PLAY";
            if (!GUILayout.Button(label, GUILayout.MinWidth(130f)))
                return;

            _cfgBellyEditMode.Value = !editMode;
            _dirty = true;
            ShowPresetPopup(_cfgBellyEditMode.Value ? "腹ボコモード: 編集" : "腹ボコモード: 再生", false);
        }

        private void DrawBellyAutoLoadToggleButton(ConfigEntryBase entryBase)
        {
            string label = _cfgBellyAutoLoadProfile != null && _cfgBellyAutoLoadProfile.Value
                ? "AUTO LOAD: ON"
                : "AUTO LOAD: OFF";

            if (!GUILayout.Button(label, GUILayout.MinWidth(140f)))
                return;

            _cfgBellyAutoLoadProfile.Value = !(_cfgBellyAutoLoadProfile != null && _cfgBellyAutoLoadProfile.Value);
            _dirty = true;
            ShowPresetPopup(
                _cfgBellyAutoLoadProfile.Value ? "自動プロファイル読込: 有効" : "自動プロファイル読込: 無効",
                false);
        }

        private void DrawBellyLoadProfileButton(ConfigEntryBase entryBase)
        {
            if (GUILayout.Button("LOAD PROFILE", GUILayout.MinWidth(120f)))
                LoadBellyProfileToEditor(forcePopup: true);
        }

        private void DrawBellyDistanceAnalyzeButton(ConfigEntryBase entryBase)
        {
            string label = _bellyDistanceAnalyzeActive
                ? "ANALYZING..."
                : "ANALYZE DISTANCE";
            if (!GUILayout.Button(label, GUILayout.MinWidth(150f)))
                return;

            RequestBellyDistanceAnalyzeNow();
        }

        private void DrawBellyTimelineEditor(ConfigEntryBase entryBase)
        {
            float forwardMin = NormalizePhase01(_cfgBellyForwardMinPhase.Value);
            float minHoldWidth = Mathf.Clamp01(_cfgBellyMinHoldWidth.Value);
            float maxPhase = NormalizePhase01(_cfgBellyMaxPhase.Value);
            float returnMinPhase = NormalizePhase01(_cfgBellyReturnMinPhase.Value);
            float displayOffset = NormalizePhase01(_cfgBellyTimelineDisplayOffset.Value);

            CoerceBellyTimelineValues(ref forwardMin, ref minHoldWidth, ref maxPhase, ref returnMinPhase);

            if (!Mathf.Approximately(forwardMin, _cfgBellyForwardMinPhase.Value)
                || !Mathf.Approximately(minHoldWidth, _cfgBellyMinHoldWidth.Value)
                || !Mathf.Approximately(maxPhase, _cfgBellyMaxPhase.Value)
                || !Mathf.Approximately(returnMinPhase, _cfgBellyReturnMinPhase.Value))
            {
                ApplyBellyTimelineValues(forwardMin, minHoldWidth, maxPhase, returnMinPhase, saveProfile: false);
            }

            float dMax = PhaseDistanceForward(forwardMin, maxPhase);
            float dHold = Mathf.Clamp(minHoldWidth, 0f, Mathf.Max(0f, dMax - BellyTimelineMinGap));
            float holdEnd = NormalizePhase01(forwardMin + dHold);
            float playPhase = _hasBellyCurrentPhase ? NormalizePhase01(_bellyCurrentPhase) : 0f;
            Rect rootRect = GUILayoutUtility.GetRect(1f, 10000f, 118f, 118f, GUILayout.ExpandWidth(true));
            float innerW = Mathf.Max(120f, rootRect.width - 12f);
            float left = rootRect.x + 6f;

            float buttonW = Mathf.Min(112f, (innerW - 8f) * 0.5f);
            Rect centerBtnRect = new Rect(left, rootRect.y + 2f, buttonW, 20f);
            Rect resetBtnRect = new Rect(left + buttonW + 8f, rootRect.y + 2f, buttonW, 20f);
            Rect offsetLabelRect = new Rect(left, rootRect.y + 25f, 65f, 18f);
            Rect offsetSliderRect = new Rect(left + 68f, rootRect.y + 27f, Mathf.Max(40f, innerW - 124f), 14f);
            Rect offsetValueRect = new Rect(left + innerW - 52f, rootRect.y + 25f, 52f, 18f);

            if (GUI.Button(centerBtnRect, "最大を中央へ"))
            {
                displayOffset = NormalizePhase01(0.5f - maxPhase);
                _cfgBellyTimelineDisplayOffset.Value = displayOffset;
                ShowPresetPopup("位相表示を最大中心へ移動", false);
            }
            if (GUI.Button(resetBtnRect, "表示リセット"))
            {
                displayOffset = 0f;
                _cfgBellyTimelineDisplayOffset.Value = 0f;
                ShowPresetPopup("位相表示をリセット", false);
            }

            GUI.Label(offsetLabelRect, "表示ずらし");
            float newDisplayOffset = GUI.HorizontalSlider(offsetSliderRect, displayOffset, 0f, 1f);
            GUI.Label(offsetValueRect, displayOffset.ToString("0.000"));
            if (!Mathf.Approximately(newDisplayOffset, displayOffset))
            {
                displayOffset = NormalizePhase01(newDisplayOffset);
                _cfgBellyTimelineDisplayOffset.Value = displayOffset;
            }

            float displayForwardMin = NormalizePhase01(forwardMin + displayOffset);
            float displayHoldEnd = NormalizePhase01(holdEnd + displayOffset);
            float displayMaxPhase = NormalizePhase01(maxPhase + displayOffset);
            float displayReturnMin = NormalizePhase01(returnMinPhase + displayOffset);
            float displayPlayPhase = NormalizePhase01(playPhase + displayOffset);

            Rect titleRect = new Rect(left, rootRect.y + 45f, innerW, 18f);
            Rect barRect = new Rect(left + 4f, rootRect.y + 67f, Mathf.Max(80f, innerW - 8f), 16f);
            Rect textRect = new Rect(left, rootRect.y + 87f, innerW, 30f);

            GUI.Label(titleRect, "位相シークバー（再生ヘッド追従 / 行き最小・待機終端・最大・帰り最小）");
            DrawBellyTimelineBar(barRect, displayForwardMin, displayHoldEnd, displayMaxPhase, displayReturnMin, displayPlayPhase);

            GUI.Label(
                textRect,
                "行き最小 " + forwardMin.ToString("0.000")
                + " / 待機 " + minHoldWidth.ToString("0.000")
                + " / 最大 " + maxPhase.ToString("0.000")
                + " / 帰り最小 " + returnMinPhase.ToString("0.000")
                + " / 再生 " + playPhase.ToString("0.000"));

            Event e = Event.current;
            if (e == null)
                return;

            if (e.type == EventType.MouseDown && e.button == 0 && barRect.Contains(e.mousePosition))
            {
                _bellyTimelineDragHandle = FindNearestTimelineHandle(barRect, e.mousePosition.x, displayForwardMin, displayHoldEnd, displayMaxPhase, displayReturnMin);
                _bellyTimelineDragDirty = false;
                if (_bellyTimelineDragHandle >= 0)
                    e.Use();
            }

            if (_bellyTimelineDragHandle >= 0 && e.type == EventType.MouseDrag)
            {
                float p = Mathf.Clamp01((e.mousePosition.x - barRect.x) / Mathf.Max(1f, barRect.width));
                float realP = NormalizePhase01(p - displayOffset);
                float newForwardMin = forwardMin;
                float newHoldEnd = holdEnd;
                float newMaxPhase = maxPhase;
                float newReturnMin = returnMinPhase;

                switch (_bellyTimelineDragHandle)
                {
                    case 0:
                        newForwardMin = realP;
                        break;
                    case 1:
                        newHoldEnd = realP;
                        break;
                    case 2:
                        newMaxPhase = realP;
                        break;
                    case 3:
                        newReturnMin = realP;
                        break;
                }

                float newHoldWidth = PhaseDistanceForward(newForwardMin, newHoldEnd);
                CoerceBellyTimelineValues(ref newForwardMin, ref newHoldWidth, ref newMaxPhase, ref newReturnMin);
                ApplyBellyTimelineValues(newForwardMin, newHoldWidth, newMaxPhase, newReturnMin, saveProfile: false);
                _bellyTimelineDragDirty = true;
                _dirty = true;
                e.Use();
            }

            if (_bellyTimelineDragHandle >= 0 && e.type == EventType.MouseUp)
            {
                int releasedHandle = _bellyTimelineDragHandle;
                _bellyTimelineDragHandle = -1;
                if (releasedHandle >= 0 && _bellyTimelineDragDirty)
                {
                    _bellyTimelineDragDirty = false;
                    if (_cfgBellyEditMode == null || _cfgBellyEditMode.Value)
                        SaveBellyProfileFromEditor(forcePopup: false);
                }
                e.Use();
            }
        }

        private void DrawBellyTimelineBar(Rect barRect, float forwardMin, float holdEnd, float maxPhase, float returnMinPhase, float playPhase)
        {
            Color prev = GUI.color;

            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            DrawTimelineSegment(barRect, forwardMin, holdEnd, new Color(0.25f, 0.55f, 0.95f, 0.9f));
            DrawTimelineSegment(barRect, holdEnd, maxPhase, new Color(0.95f, 0.62f, 0.22f, 0.9f));
            DrawTimelineSegment(barRect, maxPhase, returnMinPhase, new Color(0.35f, 0.85f, 0.45f, 0.9f));

            DrawTimelineMarker(barRect, forwardMin, new Color(0.25f, 0.75f, 1f, 1f), 3f);
            DrawTimelineMarker(barRect, holdEnd, new Color(0.3f, 0.6f, 1f, 1f), 2f);
            DrawTimelineMarker(barRect, maxPhase, new Color(1f, 0.78f, 0.25f, 1f), 3f);
            DrawTimelineMarker(barRect, returnMinPhase, new Color(0.45f, 1f, 0.55f, 1f), 3f);
            DrawTimelineMarker(barRect, playPhase, new Color(1f, 0.2f, 0.2f, 1f), 2f);

            GUI.color = prev;
        }

        private static void DrawTimelineSegment(Rect barRect, float fromPhase, float toPhase, Color color)
        {
            float a = NormalizePhase01(fromPhase);
            float b = NormalizePhase01(toPhase);

            if (Mathf.Abs(a - b) <= 1e-6f)
                return;

            if (b >= a)
            {
                DrawTimelineSegmentLinear(barRect, a, b, color);
                return;
            }

            DrawTimelineSegmentLinear(barRect, a, 1f, color);
            DrawTimelineSegmentLinear(barRect, 0f, b, color);
        }

        private static void DrawTimelineSegmentLinear(Rect barRect, float fromPhase, float toPhase, Color color)
        {
            float x0 = barRect.x + Mathf.Clamp01(fromPhase) * barRect.width;
            float x1 = barRect.x + Mathf.Clamp01(toPhase) * barRect.width;
            if (x1 <= x0)
                return;

            Rect seg = new Rect(x0, barRect.y, Mathf.Max(1f, x1 - x0), barRect.height);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(seg, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawTimelineMarker(Rect barRect, float phase, Color color, float width)
        {
            float x = barRect.x + Mathf.Clamp01(phase) * barRect.width - (width * 0.5f);
            Rect mark = new Rect(x, barRect.y - 3f, Mathf.Max(1f, width), barRect.height + 6f);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(mark, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static int FindNearestTimelineHandle(Rect barRect, float mouseX, float forwardMin, float holdEnd, float maxPhase, float returnMinPhase)
        {
            float[] phases = { forwardMin, holdEnd, maxPhase, returnMinPhase };
            int best = -1;
            float bestDist = 14f;
            for (int i = 0; i < phases.Length; i++)
            {
                float x = barRect.x + Mathf.Clamp01(phases[i]) * barRect.width;
                float d = Mathf.Abs(mouseX - x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        private void ApplyBellyTimelineValues(float forwardMin, float minHoldWidth, float maxPhase, float returnMinPhase, bool saveProfile)
        {
            _bellyEditorSyncGuard = true;
            try
            {
                _cfgBellyForwardMinPhase.Value = NormalizePhase01(forwardMin);
                _cfgBellyMinHoldWidth.Value = Mathf.Clamp01(minHoldWidth);
                _cfgBellyMaxPhase.Value = NormalizePhase01(maxPhase);
                _cfgBellyReturnMinPhase.Value = NormalizePhase01(returnMinPhase);
            }
            finally
            {
                _bellyEditorSyncGuard = false;
            }

            if (saveProfile && (_cfgBellyEditMode == null || _cfgBellyEditMode.Value))
                SaveBellyProfileFromEditor(forcePopup: false);
        }

        private static void CoerceBellyTimelineValues(ref float forwardMin, ref float minHoldWidth, ref float maxPhase, ref float returnMinPhase)
        {
            forwardMin = Mathf.Clamp01(forwardMin);
            float maxDist = PhaseDistanceForward(forwardMin, maxPhase);
            maxDist = Mathf.Clamp(maxDist, BellyTimelineMinGap * 2f, 1f - (BellyTimelineMinGap * 2f));

            float returnDist = PhaseDistanceForward(forwardMin, returnMinPhase);
            returnDist = Mathf.Clamp(returnDist, maxDist + BellyTimelineMinGap, 1f - BellyTimelineMinGap);

            float holdDist = Mathf.Clamp(minHoldWidth, 0f, Mathf.Max(0f, maxDist - BellyTimelineMinGap));

            minHoldWidth = holdDist;
            maxPhase = NormalizePhase01(forwardMin + maxDist);
            returnMinPhase = NormalizePhase01(forwardMin + returnDist);
        }

        private void OnBellySaveProfileRequested(object sender, EventArgs e)
        {
            if (_bellyTriggerGuard || !_cfgBellySaveProfileNow.Value)
                return;

            try
            {
                SaveBellyProfileFromEditor(forcePopup: true);
            }
            finally
            {
                ResetBellyTrigger(_cfgBellySaveProfileNow);
            }
        }

        private void OnBellyLoadProfileRequested(object sender, EventArgs e)
        {
            if (_bellyTriggerGuard || !_cfgBellyLoadProfileNow.Value)
                return;

            try
            {
                LoadBellyProfileToEditor(forcePopup: true);
            }
            finally
            {
                ResetBellyTrigger(_cfgBellyLoadProfileNow);
            }
        }

        private void OnBellyDistanceAnalyzeRequested(object sender, EventArgs e)
        {
            if (_bellyDistanceAnalyzeTriggerGuard || _cfgBellyDistanceAnalyzeNow == null || !_cfgBellyDistanceAnalyzeNow.Value)
                return;

            try
            {
                RequestBellyDistanceAnalyzeNow();
            }
            finally
            {
                ResetBellyDistanceAnalyzeTrigger();
            }
        }

        private void ResetBellyTrigger(ConfigEntry<bool> trigger)
        {
            _bellyTriggerGuard = true;
            try
            {
                trigger.Value = false;
            }
            finally
            {
                _bellyTriggerGuard = false;
            }
        }

        private void ResetBellyDistanceAnalyzeTrigger()
        {
            if (_cfgBellyDistanceAnalyzeNow == null)
                return;

            _bellyDistanceAnalyzeTriggerGuard = true;
            try
            {
                _cfgBellyDistanceAnalyzeNow.Value = false;
            }
            finally
            {
                _bellyDistanceAnalyzeTriggerGuard = false;
            }
        }

        private void RequestBellyDistanceAnalyzeNow()
        {
            if (!TryGetBellyContext(out BellyContext context))
            {
                ShowPresetPopup("距離分析失敗: Hシーン文脈を取得できません", true);
                return;
            }

            int turns = Mathf.Clamp(_cfgBellyDistanceAnalyzeTurns != null ? _cfgBellyDistanceAnalyzeTurns.Value : 2, 1, 12);
            _bellyDistanceAnalyzeActive = true;
            _bellyDistanceAnalyzeTargetTurns = turns;
            _bellyDistanceAnalyzeCompletedTurns = 0;
            _bellyDistanceAnalyzeMin = float.MaxValue;
            _bellyDistanceAnalyzeMax = 0f;
            _bellyDistanceAnalyzeLastPhase = context.Phase;
            _hasBellyDistanceAnalyzeLastPhase = true;
            _bellyDistanceAnalyzeKey = context.AnimationKey ?? string.Empty;
            _hasBellyDistanceSmoothed = false;

            ShowPresetPopup("距離分析開始: " + turns + "ターン", false);
            LogInfo("belly distance analysis start key=" + (context.AnimationKey ?? string.Empty) + " turns=" + turns);
        }

        private void OnBellyEditorValueChanged(object sender, EventArgs e)
        {
            if (_bellyEditorSyncGuard)
                return;

            _bellyEditorSyncGuard = true;
            try
            {
                _cfgBellyEaseUp.Value = NormalizeEaseName(_cfgBellyEaseUp.Value, "easeOut");
                _cfgBellyEaseDown.Value = NormalizeEaseName(_cfgBellyEaseDown.Value, "easeIn");
                _cfgBellyDistanceCutPercent.Value = Mathf.Clamp01(_cfgBellyDistanceCutPercent.Value);
                _cfgBellyDistanceMinMeters.Value = Mathf.Clamp(_cfgBellyDistanceMinMeters.Value, 0f, 2f);
                _cfgBellyDistanceMaxMeters.Value = Mathf.Clamp(_cfgBellyDistanceMaxMeters.Value, 0f, 2f);
                _cfgBellyDistanceSmoothing.Value = Mathf.Clamp01(_cfgBellyDistanceSmoothing.Value);
                _cfgBellyDistanceAnalyzeTurns.Value = Mathf.Clamp(_cfgBellyDistanceAnalyzeTurns.Value, 1, 12);
            }
            finally
            {
                _bellyEditorSyncGuard = false;
            }

            _dirty = true;
            if (_cfgBellyEditMode == null || _cfgBellyEditMode.Value)
                SaveBellyProfileFromEditor(forcePopup: false);
        }

        private bool UpdateBellyBokoRuntime()
        {
            _hasRuntimeInflationSizeOverride = false;

            if (!_cfgBellyEnabled.Value)
            {
                LogBellyGate("disabled");
                return false;
            }

            if (!TryGetBellyContext(out BellyContext context))
            {
                LogBellyGate("no-context");
                return false;
            }

            _cfgBellyContext.Value = context.DisplayText;
            _cfgBellyMotionStrength.Value = context.MotionStrength;
            _bellyCurrentPhase = context.Phase;
            _hasBellyCurrentPhase = true;

            if (!IsStrongOrWeakMotionStrength(context.MotionStrength))
            {
                LogBellyGate("unsupported-strength", context);
                return false;
            }

            if (!string.Equals(_bellyCurrentAnimationKey ?? string.Empty, context.AnimationKey ?? string.Empty, StringComparison.Ordinal))
            {
                LogInfo("belly context changed key=" + context.AnimationKey);
                _bellyCurrentAnimationKey = context.AnimationKey;
                _hasBellyDistanceSmoothed = false;
                if (_bellyDistanceAnalyzeActive && !string.Equals(_bellyDistanceAnalyzeKey ?? string.Empty, context.AnimationKey ?? string.Empty, StringComparison.Ordinal))
                {
                    _bellyDistanceAnalyzeActive = false;
                    ShowPresetPopup("距離分析中断: 体位が切り替わりました", true);
                    LogInfo("belly distance analysis aborted reason=context-changed");
                }
                if (_cfgBellyAutoLoadProfile == null || _cfgBellyAutoLoadProfile.Value)
                {
                    bool hasAnyForPosture = _bellyStore != null
                        && _bellyStore.HasAnyForPostureStrength(context.PostureId, context.PostureMode, context.MotionStrength);
                    if (hasAnyForPosture)
                    {
                        LoadBellyProfileToEditor(forcePopup: false);
                    }
                    else
                    {
                        LogInfo("belly auto-load skipped reason=posture-not-configured key=" + context.ShortKeyText);
                    }
                }
                else
                {
                    LogInfo("belly auto-load skipped reason=disabled");
                }
            }

            HandleBellyCaptureKeys(context);

            if (!_cfgBellyProfileEnabled.Value)
            {
                LogBellyGate("profile-disabled", context);
                return false;
            }

            bool editModeApply = _cfgBellyEditMode == null || _cfgBellyEditMode.Value;
            bool autoLoadProfile = _cfgBellyAutoLoadProfile == null || _cfgBellyAutoLoadProfile.Value;
            bool hasConfiguredForPosture = _bellyStore != null
                && _bellyStore.HasAnyForPostureStrength(context.PostureId, context.PostureMode, context.MotionStrength);

            if (!editModeApply && autoLoadProfile && !hasConfiguredForPosture)
                return ApplyBellyFlat(context, "play-posture-unconfigured-flat");

            float forwardMinPhase;
            float minHoldWidth;
            float maxPhase;
            float returnMinPhase;
            string easeUp;
            string easeDown;
            float minInflationSize;
            float maxInflationSize;
            bool distanceMode;
            float distanceCutPercent;
            float distanceMinMeters;
            float distanceMaxMeters;
            float distanceSmoothing;
            BellyBokoProfile activeProfile = null;

            if (!editModeApply && autoLoadProfile)
            {
                if (!_bellyStore.TryGet(context.AnimationKey, out BellyBokoProfile profile) || profile == null)
                {
                    return ApplyBellyFlat(context, "play-profile-missing-flat");
                }

                if (!profile.Enabled)
                {
                    return ApplyBellyFlat(context, "play-profile-disabled-flat");
                }

                activeProfile = profile;
                forwardMinPhase = NormalizePhase01(profile.ForwardMinPhase);
                minHoldWidth = Mathf.Clamp01(profile.MinHoldWidth);
                maxPhase = NormalizePhase01(profile.MaxPhase);
                returnMinPhase = NormalizePhase01(profile.ReturnMinPhase);
                easeUp = NormalizeEaseName(profile.EaseUp, "easeOut");
                easeDown = NormalizeEaseName(profile.EaseDown, "easeIn");
                minInflationSize = Mathf.Clamp(profile.MinInflationSize, 0f, 40f);
                maxInflationSize = Mathf.Clamp(profile.MaxInflationSize, 0f, 40f);
                distanceMode = profile.DistanceMode;
                distanceCutPercent = Mathf.Clamp01(profile.DistanceCutPercent);
                distanceMinMeters = Mathf.Clamp(profile.DistanceMinMeters, 0f, 2f);
                distanceMaxMeters = Mathf.Clamp(profile.DistanceMaxMeters, 0f, 2f);
                distanceSmoothing = Mathf.Clamp01(profile.DistanceSmoothing);
            }
            else
            {
                activeProfile = new BellyBokoProfile
                {
                    Enabled = true,
                    EaseUp = NormalizeEaseName(_cfgBellyEaseUp.Value, "easeOut"),
                    EaseDown = NormalizeEaseName(_cfgBellyEaseDown.Value, "easeIn")
                };

                forwardMinPhase = NormalizePhase01(_cfgBellyForwardMinPhase.Value);
                minHoldWidth = Mathf.Clamp01(_cfgBellyMinHoldWidth.Value);
                maxPhase = NormalizePhase01(_cfgBellyMaxPhase.Value);
                returnMinPhase = NormalizePhase01(_cfgBellyReturnMinPhase.Value);
                easeUp = NormalizeEaseName(_cfgBellyEaseUp.Value, "easeOut");
                easeDown = NormalizeEaseName(_cfgBellyEaseDown.Value, "easeIn");
                minInflationSize = Mathf.Clamp(_cfgBellyMinInflationSize.Value, 0f, 40f);
                maxInflationSize = Mathf.Clamp(_cfgBellyMaxInflationSize.Value, 0f, 40f);
                distanceMode = _cfgBellyDistanceMode != null && _cfgBellyDistanceMode.Value;
                distanceCutPercent = Mathf.Clamp01(_cfgBellyDistanceCutPercent != null ? _cfgBellyDistanceCutPercent.Value : 0.5f);
                distanceMinMeters = Mathf.Clamp(_cfgBellyDistanceMinMeters != null ? _cfgBellyDistanceMinMeters.Value : 0.04f, 0f, 2f);
                distanceMaxMeters = Mathf.Clamp(_cfgBellyDistanceMaxMeters != null ? _cfgBellyDistanceMaxMeters.Value : 0.24f, 0f, 2f);
                distanceSmoothing = Mathf.Clamp01(_cfgBellyDistanceSmoothing != null ? _cfgBellyDistanceSmoothing.Value : 0.35f);

                if (!editModeApply)
                    LogBellyGate("play-local-values", context);
            }

            float normalizedWeight;
            if (distanceMode)
            {
                if (!context.HasDistance)
                {
                    _runtimeInflationSizeOverride = minInflationSize;
                    _hasRuntimeInflationSizeOverride = true;
                    LogBellyGate("distance-ref-missing", context);
                    return true;
                }

                float currentDistance = Mathf.Max(0f, context.Distance);
                if (_hasBellyDistanceSmoothed)
                    _bellyDistanceSmoothed = Mathf.Lerp(currentDistance, _bellyDistanceSmoothed, distanceSmoothing);
                else
                    _bellyDistanceSmoothed = currentDistance;
                _hasBellyDistanceSmoothed = true;

                float evalDistance = _bellyDistanceSmoothed;
                EnsureDistanceRange(ref distanceMinMeters, ref distanceMaxMeters);
                normalizedWeight = EvaluateDistanceWeight(evalDistance, distanceMinMeters, distanceMaxMeters, distanceCutPercent, easeDown);
                UpdateDistanceAnalysis(context, currentDistance, evalDistance);
            }
            else
            {
                if (_bellyDistanceAnalyzeActive)
                {
                    _bellyDistanceAnalyzeActive = false;
                    LogInfo("belly distance analysis aborted reason=mode-phase");
                }

                CoerceBellyTimelineValues(ref forwardMinPhase, ref minHoldWidth, ref maxPhase, ref returnMinPhase);

                normalizedWeight = EvaluateBellyWeight(
                    context.Phase,
                    forwardMinPhase,
                    minHoldWidth,
                    maxPhase,
                    returnMinPhase,
                    easeUp,
                    easeDown);
            }

            float targetSize = Mathf.Lerp(minInflationSize, maxInflationSize, normalizedWeight);
            _runtimeInflationSizeOverride = Mathf.Clamp(targetSize, 0f, 40f);
            _hasRuntimeInflationSizeOverride = true;

            activeProfile.ForwardMinPhase = forwardMinPhase;
            activeProfile.MinHoldWidth = minHoldWidth;
            activeProfile.MaxPhase = maxPhase;
            activeProfile.ReturnMinPhase = returnMinPhase;
            activeProfile.MinInflationSize = minInflationSize;
            activeProfile.MaxInflationSize = maxInflationSize;
            activeProfile.Enabled = true;
            activeProfile.EaseUp = easeUp;
            activeProfile.EaseDown = easeDown;
            activeProfile.DistanceMode = distanceMode;
            activeProfile.DistanceCutPercent = distanceCutPercent;
            activeProfile.DistanceMinMeters = distanceMinMeters;
            activeProfile.DistanceMaxMeters = distanceMaxMeters;
            activeProfile.DistanceSmoothing = distanceSmoothing;

            LogBellyGate(Mathf.Abs(maxInflationSize - minInflationSize) <= 0.0001f ? "applied-flat-range" : "applied", context);
            LogBellyApplySample(context, activeProfile, normalizedWeight, _runtimeInflationSizeOverride);
            return true;
        }

        private void HandleBellyCaptureKeys(BellyContext context)
        {
            if (!_cfgBellyCaptureEnabled.Value)
                return;
            if (!(_cfgBellyEditMode == null || _cfgBellyEditMode.Value))
                return;
            if (_cfgBellyDistanceMode != null && _cfgBellyDistanceMode.Value)
                return;

            KeyboardShortcut maxShortcut = _cfgBellyCaptureMaxKey.Value;
            if (IsCaptureShortcutAssigned(maxShortcut) && maxShortcut.IsDown())
            {
                _cfgBellyMaxPhase.Value = NormalizePhase01(context.Phase);
                if (_cfgBellyAutoReturnMin.Value)
                {
                    float span = PhaseDistanceForward(_cfgBellyForwardMinPhase.Value, _cfgBellyMaxPhase.Value);
                    _cfgBellyReturnMinPhase.Value = NormalizePhase01(_cfgBellyMaxPhase.Value + span);
                }
                SaveBellyProfileFromEditor(forcePopup: true);
                ShowPresetPopup("最大位相を記録: 位相=" + _cfgBellyMaxPhase.Value.ToString("0.000"), false);
            }

            KeyboardShortcut minShortcut = _cfgBellyCaptureMinKey.Value;
            if (IsCaptureShortcutAssigned(minShortcut) && minShortcut.IsDown())
            {
                _cfgBellyForwardMinPhase.Value = NormalizePhase01(context.Phase);
                if (_cfgBellyAutoReturnMin.Value)
                {
                    float span = PhaseDistanceForward(_cfgBellyForwardMinPhase.Value, _cfgBellyMaxPhase.Value);
                    _cfgBellyReturnMinPhase.Value = NormalizePhase01(_cfgBellyMaxPhase.Value + span);
                }
                SaveBellyProfileFromEditor(forcePopup: true);
                ShowPresetPopup("最小位相を記録: 位相=" + _cfgBellyForwardMinPhase.Value.ToString("0.000"), false);
            }
        }

        private void SaveBellyProfileFromEditor(bool forcePopup)
        {
            if (!TryGetBellyContext(out BellyContext context))
            {
                if (forcePopup)
                    ShowPresetPopup("腹ボコ保存失敗: Hシーン文脈を取得できません", true);
                return;
            }

            int slot = Mathf.Clamp(_cfgBellyPresetSlot.Value, 1, 20);
            float minInflation = Mathf.Clamp(_cfgBellyMinInflationSize.Value, 0f, 40f);
            float maxInflation = Mathf.Clamp(_cfgBellyMaxInflationSize.Value, 0f, 40f);

            var profile = new BellyBokoProfile
            {
                AnimationKey = context.AnimationKey,
                PostureId = context.PostureId,
                PostureMode = context.PostureMode,
                PostureName = context.PostureName,
                MotionStrength = context.MotionStrength,
                AnimatorStateHash = context.AnimatorStateHash,
                Enabled = _cfgBellyProfileEnabled.Value,
                PresetSlot = slot,
                ForwardMinPhase = NormalizePhase01(_cfgBellyForwardMinPhase.Value),
                MinHoldWidth = Mathf.Clamp01(_cfgBellyMinHoldWidth.Value),
                MaxPhase = NormalizePhase01(_cfgBellyMaxPhase.Value),
                ReturnMinPhase = NormalizePhase01(_cfgBellyReturnMinPhase.Value),
                MinInflationSize = minInflation,
                MaxInflationSize = Mathf.Clamp(maxInflation, 0f, 40f),
                EaseUp = NormalizeEaseName(_cfgBellyEaseUp.Value, "easeOut"),
                EaseDown = NormalizeEaseName(_cfgBellyEaseDown.Value, "easeIn"),
                DistanceMode = _cfgBellyDistanceMode != null && _cfgBellyDistanceMode.Value,
                DistanceCutPercent = Mathf.Clamp01(_cfgBellyDistanceCutPercent != null ? _cfgBellyDistanceCutPercent.Value : 0.5f),
                DistanceMinMeters = Mathf.Clamp(_cfgBellyDistanceMinMeters != null ? _cfgBellyDistanceMinMeters.Value : 0.04f, 0f, 2f),
                DistanceMaxMeters = Mathf.Clamp(_cfgBellyDistanceMaxMeters != null ? _cfgBellyDistanceMaxMeters.Value : 0.24f, 0f, 2f),
                DistanceSmoothing = Mathf.Clamp01(_cfgBellyDistanceSmoothing != null ? _cfgBellyDistanceSmoothing.Value : 0.35f)
            };

            _bellyStore.Upsert(profile);
            _bellyStore.Save();
            _bellyCurrentAnimationKey = context.AnimationKey;

            if (forcePopup)
                ShowPresetPopup("腹ボコプロファイル保存: " + context.ShortKeyText, false);

            LogInfo("belly profile saved key=" + context.AnimationKey
                + " min=" + profile.ForwardMinPhase.ToString("0.000")
                + " hold=" + profile.MinHoldWidth.ToString("0.000")
                + " max=" + profile.MaxPhase.ToString("0.000")
                + " ret=" + profile.ReturnMinPhase.ToString("0.000")
                + " slot=" + profile.PresetSlot
                + " size=" + profile.MinInflationSize.ToString("0.###") + "->" + profile.MaxInflationSize.ToString("0.###")
                + " mode=" + (profile.DistanceMode ? "distance" : "phase")
                + " distRange=" + profile.DistanceMinMeters.ToString("0.000") + "->" + profile.DistanceMaxMeters.ToString("0.000")
                + " cut=" + profile.DistanceCutPercent.ToString("0.000"));
        }

        private void LoadBellyProfileToEditor(bool forcePopup)
        {
            if (!TryGetBellyContext(out BellyContext context))
            {
                if (forcePopup)
                    ShowPresetPopup("腹ボコ読込失敗: Hシーン文脈を取得できません", true);
                return;
            }

            if (!_bellyStore.TryGet(context.AnimationKey, out BellyBokoProfile profile) || profile == null)
            {
                if (forcePopup)
                    ShowPresetPopup("腹ボコ読込失敗: 現在キーのプロファイルがありません", true);
                return;
            }

            _bellyEditorSyncGuard = true;
            try
            {
                _cfgBellyProfileEnabled.Value = profile.Enabled;
                _cfgBellyPresetSlot.Value = Mathf.Clamp(profile.PresetSlot, 1, 20);
                _cfgBellyForwardMinPhase.Value = NormalizePhase01(profile.ForwardMinPhase);
                _cfgBellyMinHoldWidth.Value = Mathf.Clamp01(profile.MinHoldWidth);
                _cfgBellyMaxPhase.Value = NormalizePhase01(profile.MaxPhase);
                _cfgBellyReturnMinPhase.Value = NormalizePhase01(profile.ReturnMinPhase);
                _cfgBellyEaseUp.Value = NormalizeEaseName(profile.EaseUp, "easeOut");
                _cfgBellyEaseDown.Value = NormalizeEaseName(profile.EaseDown, "easeIn");
                _cfgBellyMinInflationSize.Value = Mathf.Clamp(profile.MinInflationSize, 0f, 40f);
                _cfgBellyMaxInflationSize.Value = Mathf.Clamp(profile.MaxInflationSize, 0f, 40f);
                _cfgBellyDistanceMode.Value = profile.DistanceMode;
                _cfgBellyDistanceCutPercent.Value = Mathf.Clamp01(profile.DistanceCutPercent);
                _cfgBellyDistanceMinMeters.Value = Mathf.Clamp(profile.DistanceMinMeters, 0f, 2f);
                _cfgBellyDistanceMaxMeters.Value = Mathf.Clamp(profile.DistanceMaxMeters, 0f, 2f);
                _cfgBellyDistanceSmoothing.Value = Mathf.Clamp01(profile.DistanceSmoothing);
            }
            finally
            {
                _bellyEditorSyncGuard = false;
            }
            _hasBellyDistanceSmoothed = false;

            if (forcePopup)
                ShowPresetPopup("腹ボコプロファイル読込: " + context.ShortKeyText, false);

            LogInfo("belly profile loaded key=" + context.AnimationKey);
        }

        private float GetEffectiveInflationSize()
        {
            return _hasRuntimeInflationSizeOverride
                ? _runtimeInflationSizeOverride
                : Mathf.Clamp(_cfgBellyMinInflationSize != null ? _cfgBellyMinInflationSize.Value : 0f, 0f, 40f);
        }

        private bool TryGetBellyContext(out BellyContext context)
        {
            context = default(BellyContext);

            if (Time.unscaledTime >= _nextBellyHScanTime)
            {
                _nextBellyHScanTime = Time.unscaledTime + 1f;
                if (_bellyHSceneProc == null)
                    _bellyHSceneProc = FindObjectOfType<HSceneProc>();
                else if (!_bellyHSceneProc)
                    _bellyHSceneProc = null;
            }

            if (_bellyHSceneProc == null)
                return false;

            ChaControl female = ResolveMainFemaleForBelly(_bellyHSceneProc);
            if (female == null)
                return false;

            AnimatorStateInfo stateInfo;
            try
            {
                stateInfo = female.getAnimatorStateInfo(0);
            }
            catch
            {
                return false;
            }

            object nowAnimInfo = GetNowAnimationInfoForBelly(_bellyHSceneProc);
            int postureId = GetIntMemberValueByName(nowAnimInfo, "id", int.MinValue);
            int postureMode = GetIntMemberValueByName(nowAnimInfo, "mode", int.MinValue);
            string postureName = GetStringMemberValueByName(nowAnimInfo, "nameAnimation");

            string motionStrength = ClassifyMotionStrength(stateInfo);
            int stateHash = stateInfo.fullPathHash;
            float phase = NormalizePhase01(stateInfo.normalizedTime);
            bool hasDistance = TryGetBellyDistance(_bellyHSceneProc, female, out float distanceMeters);
            string key = BuildAnimationKey(postureId, postureMode, postureName, motionStrength, stateHash);
            string shortKey = BuildShortKeyText(postureId, postureMode, postureName, motionStrength);

            context = new BellyContext
            {
                PostureId = postureId,
                PostureMode = postureMode,
                PostureName = postureName ?? string.Empty,
                MotionStrength = motionStrength,
                AnimatorStateHash = stateHash,
                Phase = phase,
                Distance = distanceMeters,
                HasDistance = hasDistance,
                AnimationKey = key,
                ShortKeyText = shortKey,
                DisplayText = hasDistance
                    ? shortKey + " hash=" + stateHash + " dist=" + distanceMeters.ToString("0.000")
                    : shortKey + " hash=" + stateHash + " dist=(none)"
            };
            return true;
        }

        private ChaControl ResolveMainFemaleForBelly(HSceneProc proc)
        {
            if (proc == null)
                return null;

            Type t = proc.GetType();
            if (!_bellyLstFemaleFieldCache.TryGetValue(t, out FieldInfo fi))
            {
                fi = t.GetField("lstFemale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _bellyLstFemaleFieldCache[t] = fi;
            }

            if (fi != null)
            {
                try
                {
                    var list = fi.GetValue(proc) as System.Collections.IList;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] is ChaControl cha && cha != null)
                                return cha;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            ChaControl[] all = FindObjectsOfType<ChaControl>();
            for (int i = 0; i < all.Length; i++)
            {
                ChaControl cha = all[i];
                if (cha != null && cha.sex == 1)
                    return cha;
            }

            return null;
        }

        private object GetNowAnimationInfoForBelly(HSceneProc proc)
        {
            if (proc == null)
                return null;

            object flags = GetFlagsForBelly(proc);
            if (flags == null)
                return null;

            Type ft = flags.GetType();
            if (!_bellyNowAnimMemberCache.TryGetValue(ft, out MemberInfo member))
            {
                FieldInfo fi = ft.GetField("nowAnimationInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                    member = fi;
                else
                    member = ft.GetProperty("nowAnimationInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _bellyNowAnimMemberCache[ft] = member;
            }

            return ReadMemberValue(flags, member);
        }

        private object GetFlagsForBelly(HSceneProc proc)
        {
            Type t = proc.GetType();
            if (!_bellyFlagsMemberCache.TryGetValue(t, out MemberInfo member))
            {
                FieldInfo fi = t.GetField("flags", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                    member = fi;
                else
                    member = t.GetProperty("flags", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _bellyFlagsMemberCache[t] = member;
            }

            return ReadMemberValue(proc, member);
        }

        private static object ReadMemberValue(object owner, MemberInfo member)
        {
            if (owner == null || member == null)
                return null;

            try
            {
                if (member is FieldInfo fi)
                    return fi.GetValue(owner);
                if (member is PropertyInfo pi)
                    return pi.GetValue(owner, null);
            }
            catch
            {
                // ignored
            }
            return null;
        }

        private static int GetIntMemberValueByName(object owner, string memberName, int fallback)
        {
            object value = GetMemberValueByName(owner, memberName);
            if (value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static string GetStringMemberValueByName(object owner, string memberName)
        {
            object value = GetMemberValueByName(owner, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static object GetMemberValueByName(object owner, string memberName)
        {
            if (owner == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = owner.GetType();
            FieldInfo fi = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
            {
                try { return fi.GetValue(owner); } catch { }
            }

            PropertyInfo pi = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null)
            {
                try { return pi.GetValue(owner, null); } catch { }
            }

            return null;
        }

        private bool TryGetBellyDistance(HSceneProc proc, ChaControl female, out float distanceMeters)
        {
            distanceMeters = 0f;
            if (proc == null || female == null)
                return false;

            ChaControl male = ResolveMaleForBelly(proc);
            if (male == null)
                return false;

            if (_bellyFemaleCha != female || _bellyFemaleDistanceRef == null)
            {
                _bellyFemaleCha = female;
                _bellyFemaleDistanceRef = ResolveFemaleDistanceReference(female);
            }

            if (_bellyMaleCha != male || _bellyMaleDistanceRef == null)
            {
                _bellyMaleCha = male;
                _bellyMaleDistanceRef = ResolveMaleDistanceReference(male);
            }

            if (_bellyFemaleDistanceRef == null || _bellyMaleDistanceRef == null)
                return false;

            distanceMeters = Vector3.Distance(_bellyMaleDistanceRef.position, _bellyFemaleDistanceRef.position);
            return true;
        }

        private static ChaControl ResolveMaleForBelly(HSceneProc proc)
        {
            if (proc == null)
                return null;

            object maleObj = GetMemberValueByName(proc, "male");
            if (maleObj is ChaControl maleCha && maleCha != null)
                return maleCha;

            ChaControl[] all = FindObjectsOfType<ChaControl>();
            for (int i = 0; i < all.Length; i++)
            {
                ChaControl cha = all[i];
                if (cha != null && cha.sex == 0)
                    return cha;
            }
            return null;
        }

        private static Transform ResolveFemaleDistanceReference(ChaControl female)
        {
            if (female == null)
                return null;

            Transform root = female.objBodyBone != null ? female.objBodyBone.transform : female.transform;
            return FindFirstTransformByNames(root,
                "k_f_kokan_00",
                "a_n_kokan",
                "cf_j_hips",
                "cf_j_root");
        }

        private static Transform ResolveMaleDistanceReference(ChaControl male)
        {
            if (male == null)
                return null;

            Transform root = male.objBodyBone != null ? male.objBodyBone.transform : male.transform;
            return FindFirstTransformByNames(root,
                "a_n_kokan",
                "cf_j_hips",
                "cf_j_waist01",
                "cm_j_waist01",
                "cf_j_root");
        }

        private static Transform FindFirstTransformByNames(Transform root, params string[] names)
        {
            if (root == null || names == null || names.Length == 0)
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                string target = names[i];
                for (int j = 0; j < all.Length; j++)
                {
                    Transform t = all[j];
                    if (t != null && string.Equals(t.name, target, StringComparison.Ordinal))
                        return t;
                }
            }

            return null;
        }

        private static string BuildAnimationKey(int postureId, int postureMode, string postureName, string motionStrength, int animatorStateHash)
        {
            return postureId + "|" + postureMode + "|" + (postureName ?? string.Empty) + "|" + motionStrength + "|" + animatorStateHash;
        }

        private static string BuildShortKeyText(int postureId, int postureMode, string postureName, string motionStrength)
        {
            return "id=" + postureId + " mode=" + postureMode + " name=" + (postureName ?? string.Empty) + " strength=" + motionStrength;
        }

        private static string ClassifyMotionStrength(AnimatorStateInfo stateInfo)
        {
            if (IsStrongMotionState(stateInfo))
                return MotionStrengthStrong;
            if (IsWeakMotionState(stateInfo))
                return MotionStrengthWeak;
            return MotionStrengthUnknown;
        }

        private static bool IsStrongOrWeakMotionStrength(string strength)
        {
            return string.Equals(strength ?? string.Empty, MotionStrengthStrong, StringComparison.Ordinal)
                || string.Equals(strength ?? string.Empty, MotionStrengthWeak, StringComparison.Ordinal);
        }

        private static bool IsStrongMotionState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName("SLoop")
                || stateInfo.IsName("A_SLoop")
                || stateInfo.IsName("SS_IN_Loop")
                || stateInfo.IsName("SF_IN_Loop")
                || stateInfo.IsName("sameS")
                || stateInfo.IsName("orgS");
        }

        private static bool IsWeakMotionState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName("WLoop")
                || stateInfo.IsName("A_WLoop")
                || stateInfo.IsName("WS_IN_Loop")
                || stateInfo.IsName("WF_IN_Loop")
                || stateInfo.IsName("sameW")
                || stateInfo.IsName("orgW");
        }

        
        private static bool IsCaptureShortcutAssigned(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None;
        }

        private bool ApplyBellyFlat(BellyContext context, string gate)
        {
            _runtimeInflationSizeOverride = 0f;
            _hasRuntimeInflationSizeOverride = true;
            LogBellyGate(gate, context);
            return true;
        }

        private void LogBellyGate(string gate)
        {
            string state = gate ?? string.Empty;
            if (string.Equals(_lastBellyDiagGate, state, StringComparison.Ordinal))
                return;

            _lastBellyDiagGate = state;
            LogInfo("belly runtime gate=" + state);
        }

        private void LogBellyGate(string gate, BellyContext context)
        {
            string state = (gate ?? string.Empty) + "|" + (context.AnimationKey ?? string.Empty);
            if (string.Equals(_lastBellyDiagGate, state, StringComparison.Ordinal))
                return;

            _lastBellyDiagGate = state;
            LogInfo("belly runtime gate=" + (gate ?? string.Empty)
                + " key=" + context.ShortKeyText
                + " hash=" + context.AnimatorStateHash);
        }

        private void LogBellyApplySample(BellyContext context, BellyBokoProfile profile, float weight, float targetSize)
        {
            if (!_cfgVerboseLog.Value)
                return;
            if (Time.unscaledTime < _nextBellyDiagLogTime)
                return;

            _nextBellyDiagLogTime = Time.unscaledTime + 0.5f;

            LogVerbose("belly runtime apply"
                + " key=" + context.ShortKeyText
                + " phase=" + context.Phase.ToString("0.000")
                + " minPhase=" + profile.ForwardMinPhase.ToString("0.000")
                + " minHold=" + profile.MinHoldWidth.ToString("0.000")
                + " maxPhase=" + profile.MaxPhase.ToString("0.000")
                + " retPhase=" + profile.ReturnMinPhase.ToString("0.000")
                + " dist=" + (context.HasDistance ? context.Distance.ToString("0.000") : "(none)")
                + " weight=" + weight.ToString("0.000")
                + " minSize=" + profile.MinInflationSize.ToString("0.###")
                + " maxSize=" + profile.MaxInflationSize.ToString("0.###")
                + " target=" + targetSize.ToString("0.###"));
        }

        private static void EnsureDistanceRange(ref float minMeters, ref float maxMeters)
        {
            minMeters = Mathf.Clamp(minMeters, 0f, 2f);
            maxMeters = Mathf.Clamp(maxMeters, 0f, 2f);
            if (maxMeters < minMeters + 0.0001f)
                maxMeters = minMeters + 0.0001f;
        }

        private static float EvaluateDistanceWeight(float distanceMeters, float minMeters, float maxMeters, float cutPercent, string easeName)
        {
            EnsureDistanceRange(ref minMeters, ref maxMeters);
            float d = Mathf.Max(0f, distanceMeters);
            float p = Mathf.Clamp01(cutPercent);
            float cut = minMeters + (maxMeters - minMeters) * p;
            if (cut < minMeters + 0.0001f)
                cut = minMeters + 0.0001f;

            if (d <= minMeters)
                return 1f;
            if (d >= cut)
                return 0f;

            float t = Mathf.InverseLerp(minMeters, cut, d);
            return 1f - EaseByName(t, easeName);
        }

        private void UpdateDistanceAnalysis(BellyContext context, float rawDistance, float evalDistance)
        {
            if (!_bellyDistanceAnalyzeActive)
                return;

            if (!string.Equals(_bellyDistanceAnalyzeKey ?? string.Empty, context.AnimationKey ?? string.Empty, StringComparison.Ordinal))
                return;

            float d = Mathf.Max(0f, evalDistance);
            if (d < _bellyDistanceAnalyzeMin)
                _bellyDistanceAnalyzeMin = d;
            if (d > _bellyDistanceAnalyzeMax)
                _bellyDistanceAnalyzeMax = d;

            if (_hasBellyDistanceAnalyzeLastPhase)
            {
                if (context.Phase + 0.5f < _bellyDistanceAnalyzeLastPhase)
                    _bellyDistanceAnalyzeCompletedTurns++;
            }

            _bellyDistanceAnalyzeLastPhase = context.Phase;
            _hasBellyDistanceAnalyzeLastPhase = true;

            if (_bellyDistanceAnalyzeCompletedTurns < _bellyDistanceAnalyzeTargetTurns)
                return;

            _bellyDistanceAnalyzeActive = false;
            float learnedMin = Mathf.Clamp(_bellyDistanceAnalyzeMin, 0f, 2f);
            float learnedMax = Mathf.Clamp(_bellyDistanceAnalyzeMax, 0f, 2f);
            EnsureDistanceRange(ref learnedMin, ref learnedMax);

            _cfgBellyDistanceMinMeters.Value = learnedMin;
            _cfgBellyDistanceMaxMeters.Value = learnedMax;

            ShowPresetPopup(
                "距離分析完了: min=" + learnedMin.ToString("0.000")
                + " max=" + learnedMax.ToString("0.000")
                + " (" + _bellyDistanceAnalyzeTargetTurns + "ターン)",
                false);
            LogInfo("belly distance analysis done"
                + " key=" + (context.AnimationKey ?? string.Empty)
                + " turns=" + _bellyDistanceAnalyzeTargetTurns
                + " min=" + learnedMin.ToString("0.000")
                + " max=" + learnedMax.ToString("0.000")
                + " raw=" + rawDistance.ToString("0.000"));

            bool editModeApply = _cfgBellyEditMode == null || _cfgBellyEditMode.Value;
            if (editModeApply)
                SaveBellyProfileFromEditor(forcePopup: false);
        }

        private static float EvaluateBellyWeight(float phase, float forwardMin, float minHoldWidth, float max, float returnMin, string easeUp, string easeDown)
        {
            phase = NormalizePhase01(phase);
            forwardMin = NormalizePhase01(forwardMin);
            minHoldWidth = Mathf.Clamp01(minHoldWidth);
            max = NormalizePhase01(max);
            returnMin = NormalizePhase01(returnMin);

            float upDistance = PhaseDistanceForward(forwardMin, max);
            float clampedHold = Mathf.Clamp(minHoldWidth, 0f, Mathf.Max(0f, upDistance - 0.0001f));
            float riseStart = NormalizePhase01(forwardMin + clampedHold);

            bool onMinHold = IsPhaseWithinForward(phase, forwardMin, riseStart);
            if (onMinHold)
                return 0f;

            // At exact max phase, prefer the down-branch so weight=1 starts from the peak.
            bool onDown = IsPhaseWithinForward(phase, max, returnMin);
            if (onDown)
            {
                float t = InverseLerpPhaseForward(max, returnMin, phase);
                return 1f - EaseByName(t, easeDown);
            }

            bool onUp = IsPhaseWithinForward(phase, riseStart, max);
            if (onUp)
            {
                float t = InverseLerpPhaseForward(riseStart, max, phase);
                return EaseByName(t, easeUp);
            }

            return 0f;
        }

        private static string NormalizeEaseName(string easing, string fallback)
        {
            string key = (easing ?? string.Empty).Trim().ToLowerInvariant();
            if (key == "linear")
                return "linear";
            if (key == "easein" || key == "in")
                return "easeIn";
            if (key == "easeout" || key == "out")
                return "easeOut";
            if (key == "smoothstep" || key == "smooth")
                return "smoothStep";
            if (key == "smootherstep" || key == "smoother")
                return "smootherStep";
            return fallback;
        }

        private static float EaseByName(float t, string easing)
        {
            t = Mathf.Clamp01(t);
            string key = NormalizeEaseName(easing, "linear").ToLowerInvariant();
            if (key == "easein")
                return t * t;
            if (key == "easeout")
                return 1f - (1f - t) * (1f - t);
            if (key == "smoothstep")
                return t * t * (3f - 2f * t);
            if (key == "smootherstep")
                return t * t * t * (t * (6f * t - 15f) + 10f);
            return t;
        }

        private static float NormalizePhase01(float v)
        {
            return Mathf.Repeat(v, 1f);
        }

        private static float PhaseDistanceForward(float from, float to)
        {
            float d = NormalizePhase01(to) - NormalizePhase01(from);
            if (d < 0f)
                d += 1f;
            return d;
        }

        private static bool IsPhaseWithinForward(float phase, float from, float to)
        {
            float len = PhaseDistanceForward(from, to);
            float pos = PhaseDistanceForward(from, phase);
            return pos <= len;
        }

        private static float InverseLerpPhaseForward(float from, float to, float phase)
        {
            float len = PhaseDistanceForward(from, to);
            if (len <= 1e-6f)
                return 0f;
            float pos = PhaseDistanceForward(from, phase);
            return Mathf.Clamp01(pos / len);
        }

        private struct BellyContext
        {
            public int PostureId;
            public int PostureMode;
            public string PostureName;
            public string MotionStrength;
            public int AnimatorStateHash;
            public float Phase;
            public float Distance;
            public bool HasDistance;
            public string AnimationKey;
            public string ShortKeyText;
            public string DisplayText;
        }
    }
}
