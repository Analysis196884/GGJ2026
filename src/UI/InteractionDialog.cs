using Godot;
using System;
using MasqueradeArk.Core;
using MasqueradeArk.Engine;
using MasqueradeArk.Manager;

namespace MasqueradeArk.UI
{
    /// <summary>
    /// 玩家与NPC交互对话框
    /// </summary>
    [GlobalClass]
    public partial class InteractionDialog : Control
    {
        [Export]
        public Label NPCLabel { get; set; }

        [Export]
        public LineEdit PlayerInput { get; set; }

        [Export]
        public Button SendButton { get; set; }

        private Survivor _currentNPC;
        private NarrativeEngine _narrativeEngine;
        private bool _isProcessing = false;

        public override void _Ready()
        {
            base._Ready();

            // 获取节点引用
            if (NPCLabel == null) NPCLabel = GetNode<Label>("NPCLabel");
            if (PlayerInput == null) PlayerInput = GetNode<LineEdit>("PlayerInput");
            if (SendButton == null) SendButton = GetNode<Button>("SendButton");

            // 如果节点不存在，创建默认 UI
            if (NPCLabel == null || PlayerInput == null || SendButton == null)
            {
                CreateDefaultUI();
            }

            // 连接信号
            if (SendButton != null)
            {
                SendButton.Pressed += OnSendPressed;
            }
            if (PlayerInput != null)
            {
                PlayerInput.TextSubmitted += OnTextSubmitted;
            }

            // 查找NarrativeEngine
            _narrativeEngine = GetNode<NarrativeEngine>("/root/GameManager/NarrativeEngine");
            if (_narrativeEngine == null)
            {
                // 尝试通过组查找
                var root = GetTree().Root;
                _narrativeEngine = root.GetNodeOrNull<NarrativeEngine>("/GameManager/NarrativeEngine");
            }
        }

        /// <summary>
        /// 创建默认 UI 节点（当场景未提供时）
        /// </summary>
        private void CreateDefaultUI()
        {
            // 创建 Label
            if (NPCLabel == null)
            {
                NPCLabel = new Label();
                NPCLabel.Name = "NPCLabel";
                NPCLabel.Text = "与 NPC 对话";
                NPCLabel.AddThemeFontSizeOverride("font_size", 16);
                AddChild(NPCLabel);
            }

            // 创建 TextEdit
            if (PlayerInput == null)
            {
                PlayerInput = new LineEdit();
                PlayerInput.Name = "PlayerInput";
                PlayerInput.PlaceholderText = "输入你想说的话...";
                PlayerInput.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                // 不透明背景
                PlayerInput.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = Colors.Black });
                AddChild(PlayerInput);
            }

            // 创建 Button
            if (SendButton == null)
            {
                SendButton = new Button();
                SendButton.Name = "SendButton";
                SendButton.Text = "发送";
                SendButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                AddChild(SendButton);
            }
        }

        /// <summary>
        /// 尝试查找 NarrativeEngine（如果尚未找到）
        /// </summary>
        private void TryFindNarrativeEngine()
        {
            if (_narrativeEngine != null) return;
            
            // 首先通过组查找（避免路径错误）
            var nodes = GetTree().GetNodesInGroup("NarrativeEngine");
            if (nodes.Count > 0 && nodes[0] is NarrativeEngine ne)
            {
                _narrativeEngine = ne;
                return;
            }
            
            // 组查找失败，尝试路径查找（可能会产生错误日志，但忽略）
            var root = GetTree().Root;
            _narrativeEngine = root.GetNodeOrNull<NarrativeEngine>("/root/GameManager/NarrativeEngine");
            if (_narrativeEngine == null)
            {
                _narrativeEngine = GetNode<NarrativeEngine>("GameManager/NarrativeEngine");
            }
            if (_narrativeEngine == null)
            {
                GD.PrintErr("[InteractionDialog] 无法找到 NarrativeEngine");
            }
            else
            {
                GD.Print($"[InteractionDialog] 通过路径找到 NarrativeEngine: {_narrativeEngine}");
            }
        }
        
        /// <summary>
        /// 获取NPC名字对应的颜色
        /// </summary>
        private Color GetNPCColor(Survivor survivor)
        {
            // 使用名字哈希生成稳定色调
            int hash = survivor.SurvivorName.GetHashCode();
            float hue = Mathf.Abs(hash % 1000) / 1000.0f; // 0-1
            return Color.FromHsv(hue, 0.7f, 0.9f);
        }
        
        /// <summary>
        /// 显示对话框并设置NPC
        /// </summary>
        public void ShowDialog(Survivor npc)
        {
            // 确保 NarrativeEngine 存在
            TryFindNarrativeEngine();
            
            _currentNPC = npc;
            if (NPCLabel != null)
            {
                NPCLabel.Text = $"与 {npc.SurvivorName} ({npc.Role}) 对话";
                NPCLabel.AddThemeColorOverride("font_color", GetNPCColor(npc));
            }
            if (PlayerInput != null)
            {
                PlayerInput.Text = "";
                PlayerInput.GrabFocus();
            }
            if (SendButton != null)
            {
                SendButton.Disabled = false;
                SendButton.Text = "发送";
            }
            _isProcessing = false;

            // 设置对话框布局
            SetupDialogLayout();
            
            Visible = true;
        }

        /// <summary>
        /// 设置对话框布局
        /// </summary>
        private void SetupDialogLayout()
        {
            // 设置对话框大小和位置
            Size = new Vector2(400, 300);
            Position = (GetViewportRect().Size - Size) / 2; // 居中显示

            // 设置子节点布局
            if (NPCLabel != null)
            {
                NPCLabel.Position = new Vector2(20, 20);
                NPCLabel.Size = new Vector2(360, 30);
            }

            if (PlayerInput != null)
            {
                PlayerInput.Position = new Vector2(20, 60);
                PlayerInput.Size = new Vector2(360, 180);
            }

            if (SendButton != null)
            {
                SendButton.Position = new Vector2(150, 250);
                SendButton.Size = new Vector2(100, 40);
            }
        }

        /// <summary>
        /// 隐藏对话框
        /// </summary>
        public void HideDialog()
        {
            Visible = false;
            _currentNPC = null;
        }

        private void OnSendPressed()
        {
            if (_currentNPC == null || _narrativeEngine == null || PlayerInput == null || SendButton == null)
            {
                GD.PrintErr($"[InteractionDialog] 条件不满足: _currentNPC={_currentNPC}, _narrativeEngine={_narrativeEngine}, PlayerInput={PlayerInput}, SendButton={SendButton}");
                return;
            }

            string input = PlayerInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                GD.Print("[InteractionDialog] 输入为空");
                return;
            }

            // 开始处理
            _isProcessing = true;
            SendButton.Disabled = true;
            SendButton.Text = "处理中...";

            // 调用交互处理
            _narrativeEngine.ProcessPlayerInteraction(_currentNPC, input, (response) =>
            {
                // 处理完成
                _isProcessing = false;
                SendButton.Disabled = false;
                SendButton.Text = "发送";

                // 显示结果
                ShowInteractionResult(response);

                // 清空输入
                PlayerInput.Text = "";
                
                // 自动关闭对话框
                HideDialog();
            });
        }

        /// <summary>
        /// 处理文本提交（按 Enter 键）
        /// </summary>
        private void OnTextSubmitted(string text)
        {
            OnSendPressed();
        }

        /// <summary>
        /// 显示交互结果
        /// </summary>
        private void ShowInteractionResult(NarrativeActionResponse response)
        {
            // 发送信号给UIManager处理反馈
            EmitSignal(SignalName.InteractionCompleted, response);
        }

        [Signal]
        public delegate void InteractionCompletedEventHandler(NarrativeActionResponse response);
    }
}