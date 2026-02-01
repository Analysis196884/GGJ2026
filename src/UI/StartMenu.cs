using Godot;
using System;

public partial class StartMenu : Control
{
    private Button startGameButton;
    private Button settingsButton;
    private Button quitButton;

    public override void _Ready()
    {
        startGameButton = GetNode<Button>("MenuContainer/StartGameButton");
        settingsButton = GetNode<Button>("MenuContainer/SettingsButton");
        quitButton = GetNode<Button>("MenuContainer/QuitButton");

        startGameButton.Pressed += OnStartGamePressed;
        settingsButton.Pressed += OnSettingsPressed;
        quitButton.Pressed += OnQuitPressed;

        // 居中菜单容器
        var menuContainer = GetNode<Control>("MenuContainer");
        menuContainer.AnchorLeft = 0.5f;
        menuContainer.AnchorRight = 0.5f;
        menuContainer.AnchorTop = 0.5f;
        menuContainer.AnchorBottom = 0.5f;

        // 等待布局完成后再设置offsets
        CallDeferred(nameof(CenterMenuContainer));
    }

    private void CenterMenuContainer()
    {
        var menuContainer = GetNode<Control>("MenuContainer");
        var center = GetWindow().GetSize() / 2;
        menuContainer.OffsetLeft = center.X - menuContainer.Size.X / 2;
        menuContainer.OffsetRight = menuContainer.OffsetLeft + menuContainer.Size.X;
        menuContainer.OffsetTop = center.Y - menuContainer.Size.Y / 2;
        menuContainer.OffsetBottom = menuContainer.OffsetTop + menuContainer.Size.Y;
    }

    private void OnStartGamePressed()
    {
        // 切换到主游戏场景
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    private void OnSettingsPressed()
    {
        // 切换到设置界面
        GetTree().ChangeSceneToFile("res://scenes/Settings.tscn");
    }

    private void OnQuitPressed()
    {
        // 退出游戏
        GetTree().Quit();
    }
}