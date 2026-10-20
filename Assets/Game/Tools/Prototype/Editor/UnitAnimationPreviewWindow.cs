using System;
using System.Linq;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.Prototype
{
    /// <summary>只读预览制作完成的素材动作。产品选择素材；此窗口不再编辑或保存产品 JSON。</summary>
    public sealed class UnitAnimationPreviewWindow : EditorWindow
    {
        [SerializeField] private UnitAnimationSet _resources;
        private UnitAnimationPlayer _player;
        private UnitAction _action;
        private Image _image;
        private Label _status;
        private SliderInt _scrubber;
        private bool _playing;
        private double _start;

        [MenuItem("Game/Animation Preview")]
        public static void Open()
        {
            GetWindow<UnitAnimationPreviewWindow>("Animation Preview");
        }

        public void CreateGUI()
        {
            minSize = new Vector2(420, 500);
            if (_resources == null)
                _resources = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(AlienAnimationImport.SetPath);
            DrawResource();
            rootVisualElement.schedule.Execute(Tick).Every(33);
        }

        private void DrawResource()
        {
            _playing = false;
            _player = null;
            var root = rootVisualElement;
            root.Clear();
            var resource = new ObjectField("Animation resource")
            {
                objectType = typeof(UnitAnimationSet), value = _resources, allowSceneObjects = false
            };
            resource.RegisterValueChangedCallback(evt =>
            {
                _resources = (UnitAnimationSet)evt.newValue;
                DrawResource();
            });
            root.Add(resource);
            root.Add(new HelpBox("这里预览素材中已制作好的标准动作。产品只需在 JSON 选择资源名，无需设置帧范围或帧率。", HelpBoxMessageType.Info));
            try
            {
                _player = new UnitAnimationPlayer(_resources);
            }
            catch (Exception exception)
            {
                root.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error));
                return;
            }

            var actions = Enum.GetValues(typeof(UnitAction)).Cast<UnitAction>()
                .Where(action => _player.Find(action) != null).Select(action => action.ToString()).ToList();
            _action = UnitAction.Idle;
            var actionPicker = new PopupField<string>("Standard action", actions, 0);
            actionPicker.RegisterValueChangedCallback(evt =>
            {
                _action = (UnitAction)Enum.Parse(typeof(UnitAction), evt.newValue);
                ShowAction();
            });
            root.Add(actionPicker);
            root.Add(new Button(() =>
            {
                _playing = !_playing;
                _start = EditorApplication.timeSinceStartup;
            }) { text = "Play / Pause (repeat preview)" });
            _scrubber = new SliderInt("Source frame", 1, _resources.Frames.Length) { showInputField = true };
            _scrubber.RegisterValueChangedCallback(evt =>
            {
                _playing = false;
                _image.sprite = _resources.Frames[evt.newValue - 1];
            });
            root.Add(_scrubber);
            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.style.flexGrow = 1;
            _image.style.minHeight = 200;
            _image.style.backgroundColor = new Color(0.35f, 0.38f, 0.4f);
            root.Add(_image);
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_status);
            root.Add(new Button(() => Selection.activeObject = _resources) { text = "Select source asset in Inspector (art authoring)" });
            ShowAction();
        }

        private void ShowAction()
        {
            _playing = false;
            var sequence = _resources.GetSequence(_action);
            _scrubber.SetValueWithoutNotify(sequence.FirstFrame);
            _image.sprite = _player.Require(_action).Frame(0);
            _status.text = $"{_action}: {sequence.FirstFrame}–{sequence.LastFrame}, {sequence.Fps} FPS, loop: {sequence.Loop}";
        }

        private void Tick()
        {
            if (!_playing || _player == null)
                return;
            var clip = _player.Require(_action);
            double age = (EditorApplication.timeSinceStartup - _start) % clip.Duration;
            _image.sprite = clip.Frame(age);
            _scrubber.SetValueWithoutNotify(_resources.GetSequence(_action).FirstFrame + (int)(age * clip.Fps));
        }
    }
}
