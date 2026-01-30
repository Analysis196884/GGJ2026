using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Core;

namespace MasqueradeArk.UI
{
    /// <summary>
    /// UI 管理器 - 负责更新所有 UI 显示，与逻辑层解耦
    /// </summary>
    [GlobalClass]
    public partial class UIManager : Control
    {
        // UI 节点引用
        private Label? _dayLabel;
        private Label? _suppliesLabel;
        private Label? _defenseLabel;
        private RichTextLabel? _eventLog;
        private VBoxContainer? _survivorCards;
        private Button? _nextDayButton;
        private Button? _meetingButton;
        private HBoxContainer? _choicesContainer;
        private LineEdit? _playerInput;

        // 信号
        [Signal]
        public delegate void NextDayPressedEventHandler();

        [Signal]
        public delegate void MeetingPressedEventHandler();

        [Signal]
        public delegate void ChoiceSelectedEventHandler(int choiceIndex);

        [Signal]
        public delegate void PlayerInputSubmittedEventHandler(string input);

        private bool _isDebugMode = false;

        public override void _Ready()
        {
            SetupUI();
            _isDebugMode = OS.IsDebugBuild();
        }

        /// <summary>
        /// 设置 UI 结构
        /// </summary>
        private void SetupUI()
        {
            // 创建主容器
            var mainContainer = new HBoxContainer();
            mainContainer.AnchorLeft = 0;
            mainContainer.AnchorTop = 0;
            mainContainer.AnchorRight = 1;
            mainContainer.AnchorBottom = 1;
            AddChild(mainContainer);

            // ===== 左侧面板（侧边栏）=====
            var sidebar = new VBoxContainer();
            sidebar.CustomMinimumSize = new Vector2(250, 0);
            mainContainer.AddChild(sidebar);

            _dayLabel = new Label { Text = "Day 1" };
            _dayLabel.AddThemeFontSizeOverride("font_size", 24);
            sidebar.AddChild(_dayLabel);

            _suppliesLabel = new Label { Text = "Supplies: 50" };
            sidebar.AddChild(_suppliesLabel);

            _defenseLabel = new Label { Text = "Defense: 50" };
            sidebar.AddChild(_defenseLabel);

            sidebar.AddChild(new HSeparator());

            var survivorLabel = new Label { Text = "幸存者列表" };
            sidebar.AddChild(survivorLabel);

            _survivorCards = new VBoxContainer();
            _survivorCards.CustomMinimumSize = new Vector2(250, 400);
            var scrollContainer = new ScrollContainer();
            scrollContainer.AddChild(_survivorCards);
            sidebar.AddChild(scrollContainer);

            // ===== 右侧主区域 =====
            var mainArea = new VBoxContainer();
            mainContainer.AddChild(mainArea);
            mainArea.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // 事件日志
            _eventLog = new RichTextLabel();
            _eventLog.CustomMinimumSize = new Vector2(0, 300);
            _eventLog.BbcodeEnabled = true;
            _eventLog.ScrollActive = true;
            mainArea.AddChild(_eventLog);

            mainArea.AddChild(new HSeparator());

            // 行动区
            var actionArea = new VBoxContainer();
            mainArea.AddChild(actionArea);

            _nextDayButton = new Button { Text = "推进到下一天" };
            _nextDayButton.Pressed += OnNextDayPressed;
            actionArea.AddChild(_nextDayButton);

            _meetingButton = new Button { Text = "召开会议" };
            _meetingButton.Pressed += OnMeetingPressed;
            actionArea.AddChild(_meetingButton);

            // 玩家输入框
            var inputLabel = new Label { Text = "输入你的命令：" };
            actionArea.AddChild(inputLabel);

            _playerInput = new LineEdit();
            _playerInput.PlaceholderText = "输入命令...";
            _playerInput.TextSubmitted += OnPlayerInputSubmitted;
            actionArea.AddChild(_playerInput);

            actionArea.AddChild(new HSeparator());

            // 选择按钮区
            _choicesContainer = new HBoxContainer();
            actionArea.AddChild(_choicesContainer);
        }

        /// <summary>
        /// 更新整个 UI 显示
        /// </summary>
        public void UpdateUI(GameState state)
        {
            UpdateStatus(state);
            UpdateSurvivorCards(state);
        }

        /// <summary>
        /// 更新状态标签
        /// </summary>
        private void UpdateStatus(GameState state)
        {
            _dayLabel.Text = $"Day {state.Day}";
            _suppliesLabel.Text = $"Supplies: {state.Supplies}";
            _defenseLabel.Text = $"Defense: {state.Defense}";
        }

        /// <summary>
        /// 更新幸存者卡片
        /// </summary>
        private void UpdateSurvivorCards(GameState state)
        {
            // 清空旧卡片
            foreach (var child in _survivorCards.GetChildren())
            {
                child.QueueFree();
            }

            // 创建新卡片
            foreach (var survivor in state.Survivors)
            {
                var card = CreateSurvivorCard(survivor);
                _survivorCards.AddChild(card);
            }
        }

        /// <summary>
        /// 创建单个幸存者卡片
        /// </summary>
        private PanelContainer CreateSurvivorCard(Survivor survivor)
        {
            var panel = new PanelContainer();
            var vbox = new VBoxContainer();
            panel.AddChild(vbox);

            // 名字和角色
            var nameLabel = new Label();
            nameLabel.Text = $"{survivor.SurvivorName} ({survivor.Role})";
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(nameLabel);

            if (_isDebugMode && survivor.Secrets.Length > 0)
            {
                var secretsLabel = new Label();
                secretsLabel.Text = $"[秘密] {string.Join(", ", survivor.Secrets)}";
                secretsLabel.AddThemeColorOverride("font_color", Colors.Red);
                vbox.AddChild(secretsLabel);
            }

            // HP 条
            vbox.AddChild(CreateProgressBar("HP", survivor.Hp, 100, Colors.Red));

            // Stress 条
            vbox.AddChild(CreateProgressBar("Stress", survivor.Stress, 100, Colors.Orange));

            // Suspicion 条
            vbox.AddChild(CreateProgressBar("Suspicion", survivor.Suspicion, 100, Colors.Yellow));

            // Hunger 条
            vbox.AddChild(CreateProgressBar("Hunger", survivor.Hunger, 100, Colors.Brown));

            return panel;
        }

        /// <summary>
        /// 创建进度条
        /// </summary>
        private HBoxContainer CreateProgressBar(string label, int value, int max, Color color)
        {
            var hbox = new HBoxContainer();

            var labelNode = new Label { Text = label + ":" };
            labelNode.CustomMinimumSize = new Vector2(80, 0);
            hbox.AddChild(labelNode);

            var progressBar = new ProgressBar();
            progressBar.MinValue = 0;
            progressBar.MaxValue = max;
            progressBar.Value = value;
            progressBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            progressBar.AddThemeColorOverride("fill", color);
            hbox.AddChild(progressBar);

            var valueLabel = new Label { Text = $"{value}/{max}" };
            valueLabel.CustomMinimumSize = new Vector2(50, 0);
            hbox.AddChild(valueLabel);

            return hbox;
        }

        /// <summary>
        /// 追加日志条目
        /// </summary>
        public void AppendLog(string text)
        {
            if (_eventLog != null)
            {
                _eventLog.AppendText(text + "\n");
                // 自动滚动到底部
                _eventLog.GetVScrollBar().Value = _eventLog.GetVScrollBar().MaxValue;
            }
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void ClearLog()
        {
            _eventLog.Clear();
        }

        /// <summary>
        /// 显示选择按钮
        /// </summary>
        public void ShowChoices(string[] choices)
        {
            // 清空旧按钮
            foreach (var child in _choicesContainer.GetChildren())
            {
                child.QueueFree();
            }

            if (choices == null || choices.Length == 0)
                return;

            for (int i = 0; i < choices.Length; i++)
            {
                int choiceIndex = i; // 闭包捕获
                var button = new Button { Text = choices[i] };
                button.Pressed += () => OnChoiceSelected(choiceIndex);
                _choicesContainer.AddChild(button);
            }
        }

        /// <summary>
        /// 隐藏选择按钮
        /// </summary>
        public void HideChoices()
        {
            foreach (var child in _choicesContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        /// <summary>
        /// 启用/禁用行动按钮
        /// </summary>
        public void SetActionButtonsEnabled(bool enabled)
        {
            _nextDayButton.Disabled = !enabled;
            _meetingButton.Disabled = !enabled;
        }

        // ===== 事件处理 =====

        private void OnNextDayPressed()
        {
            EmitSignal(SignalName.NextDayPressed);
        }

        private void OnMeetingPressed()
        {
            EmitSignal(SignalName.MeetingPressed);
        }

        private void OnChoiceSelected(int choiceIndex)
        {
            EmitSignal(SignalName.ChoiceSelected, choiceIndex);
            HideChoices();
        }

        private void OnPlayerInputSubmitted(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                EmitSignal(SignalName.PlayerInputSubmitted, input);
                _playerInput.Clear();
            }
        }

        /// <summary>
        /// 切换调试模式
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            _isDebugMode = enabled;
        }
    }
}
