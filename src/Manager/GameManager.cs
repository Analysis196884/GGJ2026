using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using MasqueradeArk.Core;
using MasqueradeArk.Engine;
using MasqueradeArk.UI;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Manager
{
	/// <summary>
	/// 游戏主管理器 - 协调所有系统的核心类
	/// 处理流程：NextDay -> Simulation -> Narrative -> UI
	/// </summary>
	[GlobalClass]
	public partial class GameManager : Node
	{
		// 核心组件
		private GameState? _gameState;
		private SimulationEngine? _simulationEngine;
		private NarrativeEngine? _narrativeEngine;
		private UIManager? _uiManager;
		
		// 新增管理器
		private LocationManager? _locationManager;
		private TaskManager? _taskManager;
		private LogExporter? _logExporter;
		private LLMClient? _llmClient;

		// 游戏状态
		private bool _isGameOver = false;
		private bool _isProcessing = false;
		
		// 任务选择状态
		private List<TaskManager.Task>? _pendingTasks = null;
		private TaskManager.Task? _selectedTask = null;
		private string? _pendingLocationAction = null;
		private string _currentUIMode = ""; // "task_select", "survivor_select", "location_action", "location_survivor_select"
		
		// 时间机制
		private Timer? _gameTimer;
		private bool _isAutoMode = false;
		private bool _isPaused = false; // 游戏暂停标志，用于暂停事件系统
		private float _dayDuration = 10.0f; // 10秒为一天

		// 背景音乐
		private AudioStreamPlayer? _bgmPlayer;

		// 信号
		[Signal]
		public delegate void GameStateUpdatedEventHandler();

		[Signal]
		public delegate void GameOverEventHandler(bool victory);

		public override void _Ready()
		{
			Initialize();
		}

		/// <summary>
		/// 初始化游戏
		/// </summary>
		private void Initialize()
		{
			GD.Print("=== 初始化游戏管理器 ===");

			// 创建或获取组件
			_gameState = new GameState();
			_gameState.Initialize();

			_simulationEngine = new SimulationEngine();
			AddChild(_simulationEngine);

			// 创建 LLM 客户端（默认启用但模拟模式）
			_llmClient = new LLMClient();
			_llmClient.Name = "LLMClient"; // 设置节点名称，便于 NarrativeEngine 查找
			_llmClient.Enabled = true; // 启用 LLM
			_llmClient.Simulate = true; // 默认使用模拟模式（避免真实 API 调用）
			AddChild(_llmClient);

			_narrativeEngine = new NarrativeEngine();
			AddChild(_narrativeEngine);
			// 显式设置 LLMClient 给 NarrativeEngine
			_narrativeEngine.SetLLMClient(_llmClient);

			// 初始化新的管理器
			_locationManager = new LocationManager();
			AddChild(_locationManager);

			_taskManager = new TaskManager();
			AddChild(_taskManager);

			_logExporter = new LogExporter();
			AddChild(_logExporter);

			// 尝试从场景树获取 UIManager
			_uiManager = GetNode<UIManager>("UIManager");
			if (_uiManager == null)
			{
				GD.PrintErr("UIManager 未找到，将创建新实例");
				_uiManager = new UIManager();
				AddChild(_uiManager);
			}

			// 初始化时间机制
			InitializeTimer();

			// 连接 UI 信号
			ConnectUISignals();
			
			// 设置日志回调 - 确保所有日志都同步到 UI
			SetupLogCallbacks();

			// 为幸存者分配随机秘密
			_simulationEngine.AssignRandomSecretsToAll(_gameState);

			// 初始化 UI
			_uiManager.UpdateUI(_gameState);
			
			// 同步 GameState 的历史日志到 UI
			SyncEventLogToUI();
			
			// 初始化背景音乐
			InitializeBGM();
			
			this.Log("欢迎来到 Masquerade Ark");
		}

		/// <summary>
		/// 初始化背景音乐
		/// </summary>
		private void InitializeBGM()
		{
			_bgmPlayer = new AudioStreamPlayer();
			AddChild(_bgmPlayer);
			var bgmStream = GD.Load<AudioStream>("res://resources/Snow in the Walls (1.50x).mp3");
			if (bgmStream != null)
			{
				_bgmPlayer.Stream = bgmStream;
				_bgmPlayer.Autoplay = true;
				_bgmPlayer.VolumeDb = -10; // 调整音量
				_bgmPlayer.Play();
				GD.Print("[GameManager] BGM 已加载并播放");
			}
			else
			{
				GD.PrintErr("[GameManager] 无法加载 BGM 音频文件");
			}
		}

		/// <summary>
		/// 同步 GameState 的事件日志到 UIManager
		/// </summary>
		private void SyncEventLogToUI()
		{
			if (_gameState == null || _uiManager == null)
				return;
			
			var logs = _gameState.GetEventLog();
			foreach (var log in logs)
			{
				_uiManager.AppendLog(log);
			}
		}
		
		/// <summary>
		/// 设置各个管理器的日志回调
		/// </summary>
		private void SetupLogCallbacks()
		{
			// 统一的日志方法：同时更新 GameState 和 UIManager
			Action<string> logAction = (message) =>
			{
				if (_gameState != null)
					_gameState.AppendLog(message);
				if (_uiManager != null)
					_uiManager.AppendLog(message);
			};
			
			// 设置所有管理器的日志回调
			if (_taskManager != null)
				_taskManager.LogCallback = logAction;
			if (_locationManager != null)
				_locationManager.LogCallback = logAction;
			if (_simulationEngine != null)
				_simulationEngine.LogCallback = logAction;
		}

		/// <summary>
		/// 统一的日志方法
		/// </summary>
		public void Log(string message)
		{
			if (_gameState != null)
				_gameState.AppendLog(message);
			if (_uiManager != null)
				_uiManager.AppendLog(message);
		}

		/// <summary>
		/// 初始化游戏计时器
		/// </summary>
		private void InitializeTimer()
		{
			_gameTimer = new Timer();
			_gameTimer.WaitTime = _dayDuration;
			_gameTimer.OneShot = false;
			_gameTimer.Timeout += OnTimerTimeout;
			AddChild(_gameTimer);
		}

		/// <summary>
		/// 计时器超时事件 - 自动推进天数
		/// </summary>
		private void OnTimerTimeout()
		{
			if (_isAutoMode && !_isGameOver && !_isProcessing && !_isPaused)
			{
				GD.Print("[GameManager] 自动模式：推进到下一天");
				OnNextDayPressed();
			}
		}

		/// <summary>
		/// 切换自动/手动模式
		/// </summary>
		public void ToggleAutoMode()
		{
			_isAutoMode = !_isAutoMode;
			if (_gameTimer != null)
			{
				if (_isAutoMode)
				{
					_gameTimer.Start();
					this.Log("已开启自动模式 - 每10秒推进一天");
				}
				else
				{
					_gameTimer.Stop();
					this.Log("已关闭自动模式 - 手动控制");
				}
			}
			GD.Print($"[GameManager] 自动模式: {(_isAutoMode ? "开启" : "关闭")}");
		}

		/// <summary>
		/// 暂停游戏事件系统（用于对话等场景）
		/// </summary>
		public void PauseGame()
		{
			if (_isPaused) return;
			_isPaused = true;
			// 暂停计时器
			if (_gameTimer != null && _gameTimer.TimeLeft > 0)
			{
				_gameTimer.Stop();
			}
			// 禁用行动按钮
			if (_uiManager != null)
			{
				_uiManager.SetActionButtonsEnabled(false);
			}
			GD.Print("[GameManager] 游戏已暂停（对话中）");
		}

		/// <summary>
		/// 恢复游戏事件系统
		/// </summary>
		public void ResumeGame()
		{
			if (!_isPaused) return;
			_isPaused = false;
			// 如果自动模式开启，重新启动计时器
			if (_isAutoMode && _gameTimer != null && !_isGameOver)
			{
				_gameTimer.Start();
			}
			// 恢复行动按钮（除非游戏结束）
			if (_uiManager != null)
			{
				_uiManager.SetActionButtonsEnabled(!_isGameOver);
			}
			GD.Print("[GameManager] 游戏已恢复");
		}

		/// <summary>
		/// 连接 UI 信号
		/// </summary>
		private void ConnectUISignals()
		{
			if (_uiManager == null)
			{
				GD.PrintErr("[GameManager] UIManager 为 null，无法连接信号");
				return;
			}
			
			try
			{
				// 使用 Godot 的 Connect 方法连接信号
				_uiManager.Connect(
					UIManager.SignalName.NextDayPressed,
					new Callable(this, MethodName.OnNextDayPressed)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.MeetingPressed,
					new Callable(this, MethodName.OnMeetingPressed)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.AutoModePressed,
					new Callable(this, MethodName.OnAutoModePressed)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.ChoiceSelected,
					new Callable(this, MethodName.OnChoiceSelected)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.PlayerInputSubmitted,
					new Callable(this, MethodName.OnPlayerInputSubmitted)
				);
				
				// 连接新的信号
				_uiManager.Connect(
					UIManager.SignalName.ExportLogPressed,
					new Callable(this, MethodName.OnExportLogPressed)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.LocationActionPressed,
					new Callable(this, MethodName.OnLocationActionPressed)
				);
				
				_uiManager.Connect(
					UIManager.SignalName.TaskActionPressed,
					new Callable(this, MethodName.OnTaskActionPressed)
				);
				
				// 连接幸存者卡片点击信号
				_uiManager.Connect(
					UIManager.SignalName.SurvivorCardClicked,
					new Callable(this, MethodName.OnSurvivorCardClicked)
				);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[GameManager] 信号连接失败：{ex.Message}");
			}
		}

		/// <summary>
		/// 推进一天的核心流程
		/// </summary>
		private void OnNextDayPressed()
		{
			if (_isGameOver || _isProcessing || _isPaused)
				return;

			_isProcessing = true;
			_uiManager.SetActionButtonsEnabled(false);

			GD.Print($"\n=== 推进到第 {_gameState.Day} 天 ===");

			// Step 1: 处理任务进度
			if (_taskManager != null)
			{
				var taskEvents = _taskManager.ProcessActiveTasks(_gameState);
				foreach (var taskEvent in taskEvents)
				{
					this.Log(taskEvent.Description);
				}
			}

			// Step 2: 模拟引擎计算
			var events = _simulationEngine.AdvanceDay(_gameState);

			// Step 3: 处理每个事件并生成叙事
			foreach (var evt in events)
			{
				GD.Print($"事件：{evt}");

				// 将事件添加到日志
				this.Log(evt.Description);

				// 生成叙事文本
				_narrativeEngine.GenerateEventNarrative(evt, _gameState, (narrative) =>
				{
					if (!string.IsNullOrEmpty(narrative.NarrativeText) && narrative.NarrativeText != evt.Description)
					{
						this.Log($"[i]{narrative.NarrativeText}[/i]");
					}

					// 如果有选择，显示给玩家
					if (narrative.Choices.Length > 0)
					{
						_uiManager.ShowChoices(narrative.Choices);
						_isProcessing = false; // 等待玩家选择
						return;
					}

					// 无选择，继续到Step 3
					// Step 3: 生成日间摘要
					var summary = _narrativeEngine.GenerateDaySummary(_gameState);
					_uiManager.AppendLog($"[i]{summary}[/i]");

					// Step 4: 更新 UI
					_uiManager.UpdateUI(_gameState);
				});
			}
			EmitSignal(SignalName.GameStateUpdated);

			// Step 5: 检查游戏结束条件
			CheckGameOver();

			_isProcessing = false;
			_uiManager.SetActionButtonsEnabled(!_isGameOver);
		}

		/// <summary>
		/// 召开会议（讨论与投票）
		/// </summary>
		private void OnMeetingPressed()
		{
			if (_isGameOver || _isProcessing || _isPaused)
				return;

			GD.Print("召开会议...");
			this.Log("\n[会议开始]");
			this.Log("团队聚集一起。气氛很紧张。");
			this.Log("每个人都有眼神接触，互相猜疑。");

			// 显示投票选项（需要自定义具体实现）
			var choices = GetVotingChoices();
			_uiManager.ShowChoices(choices);
		}

		/// <summary>
		/// 切换自动模式
		/// </summary>
		private void OnAutoModePressed()
		{
			ToggleAutoMode();
		}

		/// <summary>
		/// 获取投票选项
		/// </summary>
		private string[] GetVotingChoices()
		{
			var aliveSurvivors = new List<string>();
			foreach (var survivor in _gameState.Survivors)
			{
				if (survivor.Hp > 0)
				{
					aliveSurvivors.Add(survivor.SurvivorName);
				}
			}

			var choices = new List<string>(aliveSurvivors) { "取消投票" };
			return choices.ToArray();
		}

		/// <summary>
		/// 玩家选择处理
		/// </summary>
		private void OnChoiceSelected(int choiceIndex)
		{
			GD.Print($"玩家选择了选项 {choiceIndex}");
			
			// 处理任务选择
			if (_currentUIMode == "task_select" && _pendingTasks != null)
			{
				if (choiceIndex < _pendingTasks.Count)
				{
					_selectedTask = _pendingTasks[choiceIndex];
					_currentUIMode = "survivor_select";
					_uiManager.ShowSurvivorChoiceForTask(_gameState, _selectedTask.Name);
					return;
				}
			}
			// 处理幸存者选择
			else if (_currentUIMode == "survivor_select" && _selectedTask != null)
			{
				var aliveSurvivors = new List<Survivor>();
				foreach (var s in _gameState.Survivors)
				{
					if (s.Hp > 0) aliveSurvivors.Add(s);
				}
				
				if (choiceIndex < aliveSurvivors.Count)
				{
					var selectedSurvivor = aliveSurvivors[choiceIndex];
					bool success = _taskManager.AssignTask(ref _gameState, _selectedTask.Id, selectedSurvivor.SurvivorName);
					
					if (success)
					{
						this.Log($"任务 {_selectedTask.Name} 已分配给 {selectedSurvivor.SurvivorName}");
					}
					else
					{
						this.Log($"无法分配任务给 {selectedSurvivor.SurvivorName}");
					}
				}
				
				// 重置状态
				_currentUIMode = "";
				_selectedTask = null;
				_pendingTasks = null;
				_uiManager.HideChoices();
				return;
			}
			// 处理场所行动选择
			else if (_currentUIMode == "location_action" && _locationManager != null)
			{
				var actions = _locationManager.GetAvailableLocationActions(_gameState);
				if (choiceIndex < actions.Count)
				{
					var selectedAction = actions[choiceIndex];
					_currentUIMode = "location_survivor_select";
					_pendingLocationAction = selectedAction;
					
					// 显示幸存者选择
					var survivorChoices = new List<string>();
					foreach (var survivor in _gameState.Survivors)
					{
						if (survivor.Hp > 0)
						{
							survivorChoices.Add($"{survivor.SurvivorName} ({survivor.Role})");
						}
					}
					
					if (survivorChoices.Count > 0)
					{
						_uiManager.ShowChoices(survivorChoices.ToArray());
					}
					else
					{
						this.Log("没有可用的幸存者执行场所行动。");
						_currentUIMode = "";
					}
				}
				return;
			}
			// 处理场所行动的幸存者选择
			else if (_currentUIMode == "location_survivor_select" && _pendingLocationAction != null)
			{
				var aliveSurvivors = new List<Survivor>();
				foreach (var s in _gameState.Survivors)
				{
					if (s.Hp > 0) aliveSurvivors.Add(s);
				}
				
				if (choiceIndex < aliveSurvivors.Count)
				{
					var selectedSurvivor = aliveSurvivors[choiceIndex];
					bool success = _locationManager.ExecuteLocationAction(ref _gameState, selectedSurvivor, _pendingLocationAction);
					
					if (success)
					{
						this.Log($"{selectedSurvivor.SurvivorName} 成功执行了 {_pendingLocationAction}");
					}
					else
					{
						this.Log($"{selectedSurvivor.SurvivorName} 无法执行 {_pendingLocationAction}");
					}
					
					_uiManager.UpdateUI(_gameState);
				}
				
				// 重置状态
				_currentUIMode = "";
				_pendingLocationAction = null;
				_uiManager.HideChoices();
				return;
			}
			
			// 如果当前处于投票状态，处理投票结果
			var choices = GetVotingChoices();
			if (choiceIndex < choices.Length)
			{
				string selectedChoice = choices[choiceIndex];
				
				if (selectedChoice == "取消投票")
				{
					_uiManager.AppendLog("会议结束。没有人被驱逐。");
				}
				else
				{
					// 执行驱逐
					_simulationEngine.ExileSurvivor(_gameState, selectedChoice);
					_uiManager.AppendLog($"经过激烈的辩论，{selectedChoice} 被团队投票驱逐了。");
					_uiManager.AppendLog("气氛变得更加紧张...");
					
					// 增加所有剩余幸存者的压力
					foreach (var survivor in _gameState.Survivors)
					{
						if (survivor.Hp > 0 && survivor.SurvivorName != selectedChoice)
						{
							survivor.Stress += 15;
						}
					}
					
					// 更新UI
					_uiManager.UpdateUI(_gameState);
					
					// 检查游戏结束条件
					CheckGameOver();
				}
			}
			else
			{
				_uiManager.AppendLog("你的决定已记录。");
			}

			_isProcessing = false;
			_uiManager.SetActionButtonsEnabled(!_isGameOver);
			_uiManager.HideChoices();
		}

		/// <summary>
		/// 玩家输入处理
		/// </summary>
		private void OnPlayerInputSubmitted(string input)
		{
			GD.Print($"玩家输入：{input}");
			_uiManager.AppendLog($"> {input}");

			// 只有以"/"开头的文本会被识别为命令
			if (input.StartsWith("/"))
			{
				string command = input.Substring(1); // 去掉"/"
				ProcessCommand(command);
			}
		}

		/// <summary>
		/// 处理控制台命令（调试）
		/// </summary>
		private void ProcessCommand(string command)
		{
			var parts = command.Split(" ");
			if (parts.Length == 0)
				return;

			var cmd = parts[0].ToLower();

			switch (cmd)
			{
				case "help":
					_uiManager.AppendLog("可用命令：");
					_uiManager.AppendLog("  help - 显示帮助");
					_uiManager.AppendLog("  status - 显示当前状态");
					_uiManager.AppendLog("  secrets - 显示所有秘密（调试）");
					_uiManager.AppendLog("  damage <name> - 伤害指定幸存者");
					_uiManager.AppendLog("  infect <name> - 感染指定幸存者");
					_uiManager.AppendLog("  locations - 显示所有场所状态");
					_uiManager.AppendLog("  tasks - 显示当前任务");
					_uiManager.AppendLog("  stress <name> <amount> - 修改指定幸存者的压力值");
					_uiManager.AppendLog("  trust <name> <amount> - 修改指定幸存者的信任值");
					_uiManager.AppendLog("  supplies <amount> - 修改物资数量");
					_uiManager.AppendLog("  trigger_event - 手动触发随机事件");
					break;

				case "status":
					_uiManager.AppendLog(_gameState.ToString());
					break;

				case "secrets":
					if (OS.IsDebugBuild())
					{
						foreach (var survivor in _gameState.Survivors)
						{
							if (survivor.Secrets.Length > 0)
							{
								_uiManager.AppendLog($"{survivor.SurvivorName}: {string.Join(", ", survivor.Secrets)}");
							}
						}
					}
					else
					{
						_uiManager.AppendLog("此命令仅在调试模式下可用");
					}
					break;

				case "damage":
					if (parts.Length > 1)
					{
						var survivor = _gameState.GetSurvivor(parts[1]);
						if (survivor != null)
						{
							survivor.Hp -= 20;
							_uiManager.AppendLog($"{survivor.SurvivorName} 受到伤害");
							_uiManager.UpdateUI(_gameState);
						}
					}
					break;

				case "infect":
					if (parts.Length > 1)
					{
						var survivor = _gameState.GetSurvivor(parts[1]);
						if (survivor != null)
						{
							survivor.AddSecret("Infected");
							_uiManager.AppendLog($"{survivor.SurvivorName} 被感染了");
							_uiManager.UpdateUI(_gameState);
						}
					}
					break;

				case "locations":
					_uiManager.ShowLocationStatus(_gameState);
					break;

				case "tasks":
					if (_taskManager != null)
					{
						var activeTasks = _taskManager.GetActiveTasks();
						_uiManager.ShowActiveTasks(activeTasks);
						
						var availableTasks = _taskManager.GenerateAvailableTasks(_gameState);
						// if (availableTasks.Count > 0)
						// {
						// 	_uiManager.AppendLog("=== 可用任务 ===");
						// 	foreach (var task in availableTasks)
						// 	{
						// 		_uiManager.AppendLog($"• {task.Name}: {task.Description}");
						// 	}
						// }
					}
					break;

				case "stress":
					if (parts.Length > 2)
					{
						var survivor = _gameState.GetSurvivor(parts[1]);
						if (survivor != null && int.TryParse(parts[2], out int newstress))
						{
							survivor.Stress = newstress;
							survivor.ClampValues();
							_uiManager.AppendLog($"{survivor.SurvivorName} 的压力值改为了 {survivor.Stress}");
							_uiManager.UpdateUI(_gameState);
						}
					}
					break;

				case "trust":
					if (parts.Length > 2)
					{
						var survivor = _gameState.GetSurvivor(parts[1]);
						if (survivor != null && int.TryParse(parts[2], out int newtrust))
						{
							survivor.Trust = newtrust;
							survivor.ClampValues();
							_uiManager.AppendLog($"{survivor.SurvivorName} 的信任值改为了 {survivor.Trust}");
							_uiManager.UpdateUI(_gameState);
						}
					}
					break;

				case "supplies":
					if (parts.Length > 1 && int.TryParse(parts[1], out int supplyChange))
					{
						_gameState.Supplies += supplyChange;
						_gameState.Supplies = Math.Max(0, _gameState.Supplies);
						_uiManager.AppendLog($"物资修改了 {supplyChange}，当前为 {_gameState.Supplies}");
						_uiManager.UpdateUI(_gameState);
					}
					break;

				case "trigger_event":
					if (_simulationEngine != null)
					{
						// 手动触发一些事件进行测试
						var testEvents = new List<GameEvent>();
						
						// 触发丧尸事件
						var zombieEvent = new GameEvent(GameEvent.EventType.Custom, _gameState.Day, "测试丧尸攻击事件");
						testEvents.Add(zombieEvent);
						
						foreach (var evt in testEvents)
						{
							this.Log(evt.Description);
							_narrativeEngine.GenerateEventNarrative(evt, _gameState, (narrative) =>
							{
								if (!string.IsNullOrEmpty(narrative.NarrativeText) && narrative.NarrativeText != evt.Description)
								{
									this.Log(narrative.NarrativeText);
								}
							});
						}
						
						this.Log("已手动触发测试事件");
					}
					break;

				default:
					_uiManager.AppendLog($"未知命令：{cmd}。输入 'help' 获取帮助。");
					break;
			}
		}

		/// <summary>
		/// 检查游戏结束条件
		/// </summary>
		private void CheckGameOver()
		{
			int aliveSurvivors = _gameState.GetAliveSurvivorCount();

			if (aliveSurvivors == 0)
			{
				_isGameOver = true;
				_uiManager.AppendLog("\n=== 游戏结束 ===");
				_uiManager.AppendLog(_narrativeEngine.GenerateEndingNarrative(_gameState, false));
				EmitSignal(SignalName.GameOver, false);
				return;
			}

			// 胜利条件可以根据需要自定义
			if (_gameState.Day > GameConstants.VICTORY_DAYS)
			{
				_isGameOver = true;
				_uiManager.AppendLog("\n=== 游戏结束 ===");
				_uiManager.AppendLog(_narrativeEngine.GenerateEndingNarrative(_gameState, true));
				EmitSignal(SignalName.GameOver, true);
				return;
			}
		}

		/// <summary>
		/// 获取当前游戏状态（用于外部查询）
		/// </summary>
		public GameState GetGameState()
		{
			return _gameState;
		}

		/// <summary>
		/// 保存游戏
		/// </summary>
		public void SaveGame(string savePath)
		{
			GD.Print($"保存游戏到 {savePath}");
			var error = ResourceSaver.Save(_gameState, savePath);
			if (error == Error.Ok)
			{
				GD.Print("游戏保存成功");
			}
			else
			{
				GD.PrintErr($"游戏保存失败：{error}");
			}
		}

		/// <summary>
		/// 加载游戏
		/// </summary>
		public void LoadGame(string savePath)
		{
			GD.Print($"加载游戏从 {savePath}");
			if (ResourceLoader.Exists(savePath))
			{
				_gameState = ResourceLoader.Load<GameState>(savePath);
				_uiManager.UpdateUI(_gameState);
				_uiManager.AppendLog("游戏已加载");
				GD.Print("游戏加载成功");
			}
			else
			{
				GD.PrintErr($"存档文件不存在：{savePath}");
			}
		}

		/// <summary>
		/// 导出日志按钮事件处理
		/// </summary>
		private void OnExportLogPressed()
		{
			if (_logExporter != null && _gameState != null)
			{
				_logExporter.ExportLogsToFile(_gameState);
				_logExporter.ExportLogsToJson(_gameState);
				_logExporter.ExportGameSummary(_gameState);
				_uiManager?.AppendLog("日志已导出到用户数据目录");
			}
		}
		
		/// <summary>
		/// 场所行动按钮事件处理
		/// </summary>
		private void OnLocationActionPressed(string locationName, string actionType)
		{
			GD.Print($"[GameManager] LocationActionPressed: {locationName}, {actionType}");
			if (_gameState != null && _locationManager != null)
			{
				// _uiManager?.ShowLocationStatus(_gameState);
				
				var actions = _locationManager.GetAvailableLocationActions(_gameState);
				if (actions.Count > 0)
				{
					_currentUIMode = "location_action";
					_uiManager?.ShowLocationActions(actions.ToArray());
				}
				else
				{
					_uiManager?.AppendLog("当前没有可用的场所行动。");
				}
			}
		}
		
		/// <summary>
		/// 任务行动按钮事件处理
		/// </summary>
		private void OnTaskActionPressed(string taskId, string survivorName)
		{
			GD.Print($"[GameManager] TaskActionPressed: {taskId}, {survivorName}");
			if (_gameState != null && _taskManager != null)
			{
				// 显示当前进行中的任务
				// var activeTasks = _taskManager.GetActiveTasks();
				// _uiManager?.ShowActiveTasks(activeTasks);
				
				// 显示可用任务
				var availableTasks = _taskManager.GenerateAvailableTasks(_gameState);
				if (availableTasks.Count > 0)
				{
					_pendingTasks = availableTasks;
					_currentUIMode = "task_select";
					_uiManager?.ShowAvailableTasks(availableTasks);
				}
				else
				{
					_uiManager?.AppendLog("当前没有可用任务。");
				}
			}
		}

		/// <summary>
		/// 处理幸存者卡片点击事件
		/// </summary>
		private void OnSurvivorCardClicked(Survivor survivor)
		{
			GD.Print($"[GameManager] 幸存者卡片被点击: {survivor.SurvivorName}");
			// 显示交互对话框
			_uiManager?.ShowInteractionDialog(survivor);
		}
	}
}
