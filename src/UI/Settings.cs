using Godot;
using System;
using System.IO;

public partial class Settings : Control
{
    private LineEdit ModelNameInput;
    private LineEdit apiKeyInput;
    private Button saveButton;
    private Button backButton;

    public override void _Ready()
    {
        ModelNameInput = GetNode<LineEdit>("SettingsContainer/ModelNameInput");
        apiKeyInput = GetNode<LineEdit>("SettingsContainer/ApiKeyInput");
        saveButton = GetNode<Button>("SettingsContainer/ButtonContainer/SaveButton");
        backButton = GetNode<Button>("SettingsContainer/ButtonContainer/BackButton");

        saveButton.Pressed += OnSavePressed;
        backButton.Pressed += OnBackPressed;

        // 加载现有的API Key
        LoadApiKey();

        // 居中设置容器
        var settingsContainer = GetNode<Control>("SettingsContainer");
        settingsContainer.AnchorLeft = 0.5f;
        settingsContainer.AnchorRight = 0.5f;
        settingsContainer.AnchorTop = 0.5f;
        settingsContainer.AnchorBottom = 0.5f;

        // 等待布局完成后再设置offsets
        CallDeferred(nameof(CenterSettingsContainer));
    }

    private void CenterSettingsContainer()
    {
        var settingsContainer = GetNode<Control>("SettingsContainer");
        var center = GetWindow().GetSize() / 2;
        settingsContainer.OffsetLeft = center.X - settingsContainer.Size.X / 2;
        settingsContainer.OffsetRight = settingsContainer.OffsetLeft + settingsContainer.Size.X;
        settingsContainer.OffsetTop = center.Y - settingsContainer.Size.Y / 2;
        settingsContainer.OffsetBottom = settingsContainer.OffsetTop + settingsContainer.Size.Y;
    }

    private void LoadApiKey()
    {
        string configPath = "user://llmapi.cfg";
        if (File.Exists(ProjectSettings.GlobalizePath(configPath)))
        {
            var config = new ConfigFile();
            config.Load(configPath);
            string apiKey = (string)config.GetValue("api", "key", "");
            apiKeyInput.Text = apiKey;
        }
    }

    private void OnSavePressed()
    {
        string model = ModelNameInput.Text.Trim();
        string apiKey = apiKeyInput.Text.Trim();
        if (string.IsNullOrEmpty(model))
        {
            GD.Print("模型名称不能为空");
            return;
        }
        if (string.IsNullOrEmpty(apiKey))
        {
            GD.Print("API Key不能为空");
            return;
        }

        string configPath = "user://LLMAPI.cfg";
        var config = new ConfigFile();
        config.SetValue("API", "model", model);
        config.SetValue("API", "key", apiKey);
        config.Save(configPath);

        GD.Print("API Key已保存");
    }

    private void OnBackPressed()
    {
        // 返回到开始菜单
        GetTree().ChangeSceneToFile("res://scenes/StartMenu.tscn");
    }
}