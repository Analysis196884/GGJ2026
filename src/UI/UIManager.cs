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
		// UI 节点引用（从场景获取）
		private Label? _dayLabel;
		private Label? _suppliesLabel;
		private Label? _defenseLabel;
		private RichTextLabel? _eventLog;
		private VBoxContainer? _survivorCardsContainer;
		private Button? _nextDayButton;
		private Button? _meetingButton;
		private Button? _autoModeButton;
		private Button? _locationActionButton;
		private Button? _taskManagementButton;
		private Button? _exportLogButton;
		private HBoxContainer? _choicesContainer;
		private LineEdit? _playerInput;
		private InteractionDialog? _interactionDialog;

		// 信号
		[Signal]
		public delegate void NextDayPressedEventHandler();

		[Signal]
		public delegate void MeetingPressedEventHandler();

		[Signal]
		public delegate void AutoModePressedEventHandler();

		[Signal]
		public delegate void ChoiceSelectedEventHandler(int choiceIndex);

		[Signal]
		public delegate void PlayerInputSubmittedEventHandler(string input);
		
		[Signal]
		public delegate void ExportLogPressedEventHandler();
		
		[Signal]
		public delegate void LocationActionPressedEventHandler(string locationName, string actionType);
		
		[Signal]
		public delegate void TaskActionPressedEventHandler(string taskId, string survivorName);
		
		[Signal]
		public delegate void SurvivorCardClickedEventHandler(Survivor survivor);

		private bool _isDebugMode = false;

		public override void _Ready()
		{
			GD.Print("[UIManager] 初始化开始");
			
			// 从场景树获取所有节点引用
			GetNodeReferences();
			
			// 连接按钮信号
			ConnectButtonSignals();
			
			// 初始化交互对话框
			InitializeInteractionDialog();
			
			_isDebugMode = OS.IsDebugBuild();
			GD.Print("[UIManager] 初始化完成");
		}

		/// <summary>
		/// 从场景树获取所有节点引用
		/// </summary>
		private void GetNodeReferences()
		{
			try
			{
				var hboxContainer = GetNode<HBoxContainer>("HBoxContainer");
				var sidebar = hboxContainer.GetNode<VBoxContainer>("Sidebar");
				var mainArea = hboxContainer.GetNode<VBoxContainer>("MainArea");
	
				// 从新的UI结构获取节点引用
				_dayLabel = sidebar.GetNode<PanelContainer>("StatusPanel")
					.GetNode<VBoxContainer>("StatusVBox").GetNode<Label>("DayLabel");
				_suppliesLabel = sidebar.GetNode<PanelContainer>("StatusPanel")
					.GetNode<VBoxContainer>("StatusVBox").GetNode<Label>("SuppliesLabel");
				_defenseLabel = sidebar.GetNode<PanelContainer>("StatusPanel")
					.GetNode<VBoxContainer>("StatusVBox").GetNode<Label>("DefenseLabel");
				
				_survivorCardsContainer = sidebar.GetNode<PanelContainer>("SurvivorPanel")
					.GetNode<VBoxContainer>("SurvivorVBox").GetNode<ScrollContainer>("ScrollContainer")
					.GetNode<VBoxContainer>("SurvivorCardsContainer");
	
				_eventLog = mainArea.GetNode<PanelContainer>("EventLogPanel")
					.GetNode<VBoxContainer>("EventLogVBox").GetNode<RichTextLabel>("EventLog");
				if (_eventLog != null)
				{
					// 扩大事件日志窗口：设置最小尺寸并允许横/纵向扩展以占满可用空间
					_eventLog.CustomMinimumSize = new Vector2(800, 400);
					_eventLog.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					_eventLog.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
				}
	
				var actionArea = mainArea.GetNode<PanelContainer>("ActionPanel")
					.GetNode<VBoxContainer>("ActionVBox").GetNode<VBoxContainer>("ActionArea");
				
				_nextDayButton = actionArea.GetNode<HBoxContainer>("ButtonsHBox").GetNode<Button>("NextDayButton");
				_meetingButton = actionArea.GetNode<HBoxContainer>("ButtonsHBox").GetNode<Button>("MeetingButton");
				_autoModeButton = actionArea.GetNode<HBoxContainer>("ButtonsHBox").GetNode<Button>("AutoModeButton");
				
				// 获取新增的按钮
				var actionButtonsHBox = actionArea.GetNode<HBoxContainer>("ActionButtonsHBox");
				_locationActionButton = actionButtonsHBox.GetNode<Button>("LocationActionButton");
				_taskManagementButton = actionButtonsHBox.GetNode<Button>("TaskManagementButton");
				_exportLogButton = actionButtonsHBox.GetNode<Button>("ExportLogButton");
				
				_playerInput = actionArea.GetNode<LineEdit>("PlayerInput");
				_choicesContainer = actionArea.GetNode<HBoxContainer>("ChoicesContainer");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[UIManager] 获取节点引用失败：{ex.Message}");
			}
		}

		/// <summary>
		/// 连接按钮信号
		/// </summary>
		private void ConnectButtonSignals()
		{
			if (_nextDayButton != null)
			{
				_nextDayButton.Pressed += OnNextDayPressed;
			}

			if (_meetingButton != null)
			{
				_meetingButton.Pressed += OnMeetingPressed;
			}

			if (_autoModeButton != null)
			{
				_autoModeButton.Pressed += OnAutoModePressed;
			}

			if (_locationActionButton != null)
			{
				_locationActionButton.Pressed += OnLocationActionPressed;
			}

			if (_taskManagementButton != null)
			{
				_taskManagementButton.Pressed += OnTaskManagementPressed;
			}

			if (_exportLogButton != null)
			{
				_exportLogButton.Pressed += OnExportLogPressed;
			}

			if (_playerInput != null)
			{
				_playerInput.TextSubmitted += OnPlayerInputSubmitted;
			}
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
		/// 显示场所状态信息
		/// </summary>
		public void ShowLocationStatus(GameState state)
		{
			var locationInfo = "=== 场所状态 ===\n";
			foreach (var location in state.Locations)
			{
				locationInfo += $"{location.Name}: {(location.CanUse() ? "可用" : "不可用")} ";
				locationInfo += $"(损坏度: {location.DamageLevel}%, 效率: {location.GetEfficiency():P})\n";
			}
			AppendLog(locationInfo);
		}
		
		/// <summary>
		/// 显示可用的场所行动选项
		/// </summary>
		public void ShowLocationActions(string[] actions)
		{
			if (actions == null || actions.Length == 0)
			{
				AppendLog("当前没有可用的场所行动。");
				return;
			}
			
			ShowChoices(actions);
		}
		
		/// <summary>
		/// 显示可用任务列表
		/// </summary>
		public void ShowAvailableTasks(List<MasqueradeArk.Manager.TaskManager.Task> tasks)
		{
			if (tasks == null || tasks.Count == 0)
			{
				AppendLog("当前没有可用任务。");
				return;
			}
			
			var taskInfo = "=== 可用任务 ===\n";
			var taskChoices = new List<string>();
			
			for (int i = 0; i < tasks.Count; i++)
			{
				var task = tasks[i];
				taskInfo += $"{i + 1}. {task.Name}\n";
				taskInfo += $"   描述: {task.Description}\n";
				taskInfo += $"   持续时间: {task.Duration} 天\n";
				
				// 显示任务要求
				if (task.Requirements != null && task.Requirements.Count > 0)
				{
					taskInfo += "   要求: ";
					foreach (var req in task.Requirements)
					{
						taskInfo += $"{req.Key}:{req.Value} ";
					}
					taskInfo += "\n";
				}
				
				taskInfo += "\n";
				taskChoices.Add($"选择任务: {task.Name}");
			}
			
			// AppendLog(taskInfo);
			ShowChoices(taskChoices.ToArray());
		}
		
		/// <summary>
		/// 显示幸存者选择界面用于任务分配
		/// </summary>
		public void ShowSurvivorChoiceForTask(GameState state, string taskName)
		{
			var choices = new List<string>();
			foreach (var survivor in state.Survivors)
			{
				if (survivor.Hp > 0)
				{
					choices.Add($"{survivor.SurvivorName}({survivor.Role})");
				}
			}
			
			if (choices.Count > 0)
			{
				ShowChoices(choices.ToArray());
			}
			else
			{
				AppendLog("没有可用的幸存者执行任务。");
			}
		}
		
		/// <summary>
		/// 显示当前进行中的任务
		/// </summary>
		public void ShowActiveTasks(List<MasqueradeArk.Manager.TaskManager.Task> activeTasks)
		{
			if (activeTasks == null || activeTasks.Count == 0)
			{
				AppendLog("当前没有进行中的任务。");
				return;
			}
			
			var taskInfo = "=== 进行中的任务 ===\n";
			foreach (var task in activeTasks)
			{
				taskInfo += $"• {task.Name} - 执行者: {task.AssignedSurvivor}\n";
				taskInfo += $"  进度: {task.Progress}/{task.Duration} 天\n";
			}
			
			AppendLog(taskInfo);
		}

		/// <summary>
		/// 更新状态标签
		/// </summary>
		private void UpdateStatus(GameState state)
		{
			if (_dayLabel != null)
				_dayLabel.Text = $"Day {state.Day}";
			if (_suppliesLabel != null)
				_suppliesLabel.Text = $"Supplies: {state.Supplies}";
			if (_defenseLabel != null)
				_defenseLabel.Text = $"Defense: {state.Defense}";
		}

		/// <summary>
		/// 更新幸存者卡片
		/// </summary>
		private void UpdateSurvivorCards(GameState state)
		{
			if (_survivorCardsContainer == null)
				return;

			// 清空旧卡片
			foreach (var child in _survivorCardsContainer.GetChildren())
			{
				child.QueueFree();
			}

			// 创建新卡片
			foreach (var survivor in state.Survivors)
			{
				var card = CreateSurvivorCard(survivor);
				_survivorCardsContainer.AddChild(card);
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

			// Trust 条
			vbox.AddChild(CreateProgressBar("Trust", survivor.Trust, 100, Colors.Green));

			// Hunger 条
			vbox.AddChild(CreateProgressBar("Hunger", survivor.Hunger, 100, Colors.Brown));

			// 添加交互按钮（覆盖整个卡片）
			var interactButton = new Button();
			interactButton.Name = "InteractButton";
			interactButton.Text = "对话";
			interactButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			interactButton.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			interactButton.Flat = true; // 透明背景
			interactButton.Pressed += () =>
			{
				GD.Print($"[UIManager] 幸存者卡片按钮被点击: {survivor.SurvivorName}");
				// 发出幸存者卡片点击信号
				EmitSignal(SignalName.SurvivorCardClicked, survivor);
			};
			vbox.AddChild(interactButton);

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
			if (_eventLog != null)
				_eventLog.Clear();
		}

		/// <summary>
		/// 显示选择按钮
		/// </summary>
		public void ShowChoices(string[] choices)
		{
			if (_choicesContainer == null)
				return;

			// 确保容器可见并扩展，以便按钮能显示
			_choicesContainer.Visible = true;
			_choicesContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_choicesContainer.CustomMinimumSize = new Vector2(200, 0);

			// 清空旧按钮
			foreach (var child in _choicesContainer.GetChildren())
			{
				child.QueueFree();
			}

			if (choices == null || choices.Length == 0)
			{
				GD.Print("[UIManager] ShowChoices: no choices provided");
				return;
			}

			for (int i = 0; i < choices.Length; i++)
			{
				int choiceIndex = i; // 闭包捕获
				var button = new Button { Text = choices[i] };
				button.Pressed += () => OnChoiceSelected(choiceIndex);
				_choicesContainer.CallDeferred("add_child", button);
			}
		}

		/// <summary>
		/// 隐藏选择按钮
		/// </summary>
		public void HideChoices()
		{
			if (_choicesContainer == null)
				return;

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
			if (_nextDayButton != null)
				_nextDayButton.Disabled = !enabled;
			if (_meetingButton != null)
				_meetingButton.Disabled = !enabled;
		}

		// ===== 事件处理 =====

		private void OnNextDayPressed()
		{
			GD.Print("[UIManager] NextDayButton 被按下");
			EmitSignal(SignalName.NextDayPressed);
		}

		private void OnMeetingPressed()
		{
			GD.Print("[UIManager] MeetingButton 被按下");
			EmitSignal(SignalName.MeetingPressed);
		}

		private void OnAutoModePressed()
		{
			GD.Print("[UIManager] AutoModeButton 被按下");
			EmitSignal(SignalName.AutoModePressed);
		}

		private void OnChoiceSelected(int choiceIndex)
		{
			// 不立即隐藏选择按钮；由 GameManager 在处理完选择后调用 HideChoices()
			EmitSignal(SignalName.ChoiceSelected, choiceIndex);
		}

		private void OnPlayerInputSubmitted(string input)
		{
			GD.Print($"[UIManager] 玩家输入：{input}");
			if (!string.IsNullOrEmpty(input))
			{
				EmitSignal(SignalName.PlayerInputSubmitted, input);
				if (_playerInput != null)
					_playerInput.Clear();
			}
		}

		private void OnLocationActionPressed()
		{
			GD.Print("[UIManager] LocationActionButton 被按下");
			EmitSignal(SignalName.LocationActionPressed, "", "");
		}

		private void OnTaskManagementPressed()
		{
			GD.Print("[UIManager] TaskManagementButton 被按下");
			EmitSignal(SignalName.TaskActionPressed, "", "");
		}

		private void OnExportLogPressed()
		{
			GD.Print("[UIManager] ExportLogButton 被按下");
			EmitSignal(SignalName.ExportLogPressed);
		}

		/// <summary>
		/// 切换调试模式
		/// </summary>
		public void SetDebugMode(bool enabled)
		{
			_isDebugMode = enabled;
		}

		/// <summary>
		/// 初始化交互对话框
		/// </summary>
		private void InitializeInteractionDialog()
		{
			// 加载场景
			var scene = GD.Load<PackedScene>("res://scenes/InteractionDialog.tscn");
			if (scene != null)
			{
				_interactionDialog = scene.Instantiate<InteractionDialog>();
				AddChild(_interactionDialog);
				// 连接信号
				_interactionDialog.InteractionCompleted += OnInteractionCompleted;
				GD.Print("[UIManager] 交互对话框已加载");
			}
			else
			{
				GD.PrintErr("[UIManager] 无法加载交互对话框场景，创建默认实例");
				_interactionDialog = new InteractionDialog();
				AddChild(_interactionDialog);
				_interactionDialog.InteractionCompleted += OnInteractionCompleted;
			}
			// 默认隐藏
			_interactionDialog.Visible = false;
		}

		/// <summary>
		/// 显示与指定 NPC 的交互对话框
		/// </summary>
		public void ShowInteractionDialog(Survivor npc)
		{
			GD.Print($"[UIManager] 显示交互对话框给 {npc.SurvivorName}");
			if (_interactionDialog != null)
			{
				_interactionDialog.ShowDialog(npc);
			}
			else
			{
				GD.PrintErr("[UIManager] 交互对话框为空");
			}
		}

		/// <summary>
		/// 处理交互完成事件
		/// </summary>
		private void OnInteractionCompleted(NarrativeActionResponse response)
		{
			// 将 NPC 回应文本记录到日志
			if (!string.IsNullOrEmpty(response.NarrativeText))
			{
				AppendLog($"> {response.NarrativeText}");
			}
			// 根据成功状态提供视觉反馈（TODO: 播放音效/特效）
			if (response.IsSuccess)
			{
				AppendLog("[成功]");
			}
			else
			{
				AppendLog("[失败]");
			}
			// 更新 UI 以反映数值变化
			// （因为 NarrativeEngine 已经应用了变化，只需刷新卡片）
			// 注意：UIManager.UpdateUI 将在下次游戏循环时被调用
		}
	}
}
