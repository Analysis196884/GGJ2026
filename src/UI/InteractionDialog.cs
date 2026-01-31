using Godot;
using System;
using MasqueradeArk.Core;
using MasqueradeArk.Engine;

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
        public TextEdit PlayerInput { get; set; }

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
            if (PlayerInput == null) PlayerInput = GetNode<TextEdit>("PlayerInput");
            if (SendButton == null) SendButton = GetNode<Button>("SendButton");

            // 连接信号
            if (SendButton != null)
            {
                SendButton.Pressed += OnSendPressed;
            }

            // 查找NarrativeEngine
            _narrativeEngine = GetNode<NarrativeEngine>("/root/GameManager/NarrativeEngine");
            if (_narrativeEngine == null)
            {
                GD.PrintErr("[InteractionDialog] 未找到 NarrativeEngine");
            }
        }

        /// <summary>
        /// 显示对话框并设置NPC
        /// </summary>
        public void ShowDialog(Survivor npc)
        {
            _currentNPC = npc;
            if (NPCLabel != null)
            {
                NPCLabel.Text = $"与 {npc.SurvivorName} ({npc.Role}) 对话";
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
                return;
            }

            string input = PlayerInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
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
            });
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