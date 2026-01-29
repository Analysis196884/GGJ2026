using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Core;
using MasqueradeArk.Engine;
using MasqueradeArk.UI;

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
        private GameState _gameState;
        private SimulationEngine _simulationEngine;
        private NarrativeEngine _narrativeEngine;
        private UIManager _uiManager;

        // 游戏状态
        private bool _isGameOver = false;
        private bool _isProcessing = false;

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

            // 尝试从场景树获取 UIManager
            _uiManager = GetNode<UIManager>("../UIManager");
            if (_uiManager == null)
            {
                GD.PrintErr("UIManager 未找到，将创建新实例");
                _uiManager = new UIManager();
                GetParent().AddChild(_uiManager);
            }

            // 连接 UI 信号
            ConnectUISignals();

            // 初始化 UI
            _uiManager.UpdateUI(_gameState);
            _uiManager.AppendLog("欢迎来到 Masquerade Ark");
            _uiManager.AppendLog(_narrativeEngine.GenerateDaySummary(_gameState));

            GD.Print($"游戏初始化完成。初始幸存者数：{_gameState.GetSurvivorCount()}");
        }

        /// <summary>
        /// 连接 UI 信号
        /// </summary>
        private void ConnectUISignals()
        {
            _uiManager.NextDayPressed += OnNextDayPressed;
            _uiManager.MeetingPressed += OnMeetingPressed;
            _uiManager.ChoiceSelected += OnChoiceSelected;
            _uiManager.PlayerInputSubmitted += OnPlayerInputSubmitted;
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
            // 这里可以根据选择执行不同的操作
            GD.Print($"玩家选择了选项 {choiceIndex}");
            _uiManager.AppendLog("你的决定已记录。");

            _isProcessing = false;
            _uiManager.SetActionButtonsEnabled(true);
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
            if (_gameState.Day > 30)
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
    }
}
