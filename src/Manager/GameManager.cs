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

        // 游戏状态
        private bool _isGameOver = false;
        private bool _isProcessing = false;
        
        // 时间机制
        private Timer? _gameTimer;
        private bool _isAutoMode = false;
        private float _dayDuration = 10.0f; // 10秒为一天

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

            _narrativeEngine = new NarrativeEngine();
            AddChild(_narrativeEngine);

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

            // 为幸存者分配随机秘密
            _simulationEngine.AssignRandomSecretsToAll(_gameState);

            // 初始化 UI
            _uiManager.UpdateUI(_gameState);
            _uiManager.AppendLog("欢迎来到 Masquerade Ark");
            _uiManager.AppendLog(_narrativeEngine.GenerateDaySummary(_gameState));

            GD.Print($"游戏初始化完成。初始幸存者数：{_gameState.GetSurvivorCount()}");
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
            
            GD.Print($"[GameManager] 时间机制初始化完成。每天持续 {_dayDuration} 秒");
        }

        /// <summary>
        /// 计时器超时事件 - 自动推进天数
        /// </summary>
        private void OnTimerTimeout()
        {
            if (_isAutoMode && !_isGameOver && !_isProcessing)
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
                    _uiManager?.AppendLog("已开启自动模式 - 每10秒推进一天");
                }
                else
                {
                    _gameTimer.Stop();
                    _uiManager?.AppendLog("已关闭自动模式 - 手动控制");
                }
            }
            GD.Print($"[GameManager] 自动模式: {(_isAutoMode ? "开启" : "关闭")}");
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

            GD.Print("[GameManager] 连接 UIManager 信号...");
            
            try
            {
                // 使用 Godot 的 Connect 方法连接信号
                _uiManager.Connect(
                    UIManager.SignalName.NextDayPressed,
                    new Callable(this, MethodName.OnNextDayPressed)
                );
                GD.Print("[GameManager] NextDayPressed 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.MeetingPressed,
                    new Callable(this, MethodName.OnMeetingPressed)
                );
                GD.Print("[GameManager] MeetingPressed 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.AutoModePressed,
                    new Callable(this, MethodName.OnAutoModePressed)
                );
                GD.Print("[GameManager] AutoModePressed 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.ChoiceSelected,
                    new Callable(this, MethodName.OnChoiceSelected)
                );
                GD.Print("[GameManager] ChoiceSelected 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.PlayerInputSubmitted,
                    new Callable(this, MethodName.OnPlayerInputSubmitted)
                );
                GD.Print("[GameManager] PlayerInputSubmitted 连接成功");
                
                // 连接新的信号
                _uiManager.Connect(
                    UIManager.SignalName.ExportLogPressed,
                    new Callable(this, MethodName.OnExportLogPressed)
                );
                GD.Print("[GameManager] ExportLogPressed 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.LocationActionPressed,
                    new Callable(this, MethodName.OnLocationActionPressed)
                );
                GD.Print("[GameManager] LocationActionPressed 连接成功");
                
                _uiManager.Connect(
                    UIManager.SignalName.TaskActionPressed,
                    new Callable(this, MethodName.OnTaskActionPressed)
                );
                GD.Print("[GameManager] TaskActionPressed 连接成功");
                
                GD.Print("[GameManager] 所有信号连接完成");
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
            if (_isGameOver || _isProcessing)
                return;

            _isProcessing = true;
            _uiManager.SetActionButtonsEnabled(false);

            GD.Print($"\n=== 推进到第 {_gameState.Day} 天 ===");

            // Step 1: 模拟引擎计算
            var events = _simulationEngine.AdvanceDay(_gameState);

            // Step 2: 处理每个事件并生成叙事
            foreach (var evt in events)
            {
                GD.Print($"事件：{evt}");

                // 将事件添加到日志
                _gameState.AppendLog(evt.Description);

                // 生成叙事文本
                var narrative = _narrativeEngine.GenerateEventNarrative(evt, _gameState);
                _uiManager.AppendLog(narrative.NarrativeText);

                // 如果有选择，显示给玩家
                if (narrative.Choices.Length > 0)
                {
                    _uiManager.ShowChoices(narrative.Choices);
                    _isProcessing = false; // 等待玩家选择
                    return;
                }
            }

            // Step 3: 生成日间摘要
            var summary = _narrativeEngine.GenerateDaySummary(_gameState);
            _uiManager.AppendLog(summary);

            // Step 4: 更新 UI
            _uiManager.UpdateUI(_gameState);
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
            if (_isGameOver || _isProcessing)
                return;

            GD.Print("召开会议...");
            _uiManager.AppendLog("\n[会议开始]");
            _uiManager.AppendLog("团队聚集一起。气氛很紧张。");
            _uiManager.AppendLog("每个人都有眼神接触，互相猜疑。");

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

            // 可以在这里添加调试命令处理
            ProcessCommand(input);
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
            
            /// <summary>
            /// 导出日志按钮事件处理
            /// </summary>
            private void OnExportLogPressed()
            {
                GD.Print("[GameManager] ExportLogPressed 被按下");
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
                    _uiManager?.ShowLocationStatus(_gameState);
                    
                    var actions = _locationManager.GetAvailableLocationActions(_gameState);
                    _uiManager?.ShowLocationActions(actions.ToArray());
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
                    var availableTasks = _taskManager.GenerateAvailableTasks(_gameState);
                    var taskObjects = new List<object>();
                    foreach (var task in availableTasks)
                    {
                        taskObjects.Add(task);
                    }
                    _uiManager?.ShowAvailableTasks(taskObjects);
                }
            }
        }
    }
}
