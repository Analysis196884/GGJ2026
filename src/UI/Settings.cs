using Godot;
using System;
using System.IO;

public partial class Settings : Control
{
    private LineEdit ModelNameInput;
    private LineEdit APIKeyInput;
    private LineEdit ApiEndpointInput;
    private Button saveButton;
    private Button backButton;
    private Button testButton;
    private Label testResultLabel;

    public override void _Ready()
    {
        ApiEndpointInput = GetNode<LineEdit>("SettingsContainer/APIEndpointContainer/ApiEndpointInput");
        ModelNameInput = GetNode<LineEdit>("SettingsContainer/ModelNameContainer/ModelNameInput");
        APIKeyInput = GetNode<LineEdit>("SettingsContainer/APIKeyContainer/ApiKeyInput");
        saveButton = GetNode<Button>("SettingsContainer/ButtonContainer/SaveButton");
        backButton = GetNode<Button>("SettingsContainer/ButtonContainer/BackButton");
        testButton = GetNode<Button>("SettingsContainer/ButtonContainer/TestButton");
        testResultLabel = GetNode<Label>("SettingsContainer/TestResultLabel");

        saveButton.Pressed += OnSavePressed;
        backButton.Pressed += OnBackPressed;
        testButton.Pressed += OnTestPressed;

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
        string configPath = "user://LLMAPI.cfg";
        if (File.Exists(ProjectSettings.GlobalizePath(configPath)))
        {
            var config = new ConfigFile();
            config.Load(configPath);
            string model = (string)config.GetValue("API", "model", "");
            string apiKey = (string)config.GetValue("API", "key", "");
            string endpoint = (string)config.GetValue("API", "endpoint", "");
            ModelNameInput.Text = model;
            APIKeyInput.Text = apiKey;
            ApiEndpointInput.Text = endpoint;
        }
    }

    private void OnSavePressed()
    {
        string model = ModelNameInput.Text.Trim();
        string apiKey = APIKeyInput.Text.Trim();
        string endpoint = ApiEndpointInput.Text.Trim();
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
        if (string.IsNullOrEmpty(endpoint))
        {
            GD.Print("API Endpoint不能为空");
            return;
        }

        string configPath = "user://LLMAPI.cfg";
        var config = new ConfigFile();
        config.SetValue("API", "endpoint", endpoint);
        config.SetValue("API", "model", model);
        config.SetValue("API", "key", apiKey);
        config.Save(configPath);

        GD.Print("API 配置已保存");
    }

    private void OnBackPressed()
    {
        // 返回到开始菜单
        GetTree().ChangeSceneToFile("res://scenes/StartMenu.tscn");
    }

    private void OnTestPressed()
    {
        string endpoint = ApiEndpointInput.Text.Trim();
        string model = ModelNameInput.Text.Trim();
        string apiKey = APIKeyInput.Text.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            GD.Print("API Key不能为空");
            return;
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            GD.Print("API Endpoint不能为空");
            return;
        }

        // 创建临时的LLMClient来测试连接
        var testClient = new MasqueradeArk.Engine.LLMClient();
        testClient.ApiKey = apiKey;
        testClient.Model = model;
        // 确保测试使用输入框中的 endpoint，而不是配置文件中的值
        testClient.ApiEndpoint = endpoint;
        testClient.Enabled = true;
        AddChild(testClient);

        testButton.Disabled = true;
        testButton.Text = "测试中...";

        testClient.TestConnection(endpoint, model, apiKey, (success, message) =>
        {
            testButton.Disabled = false;
            testButton.Text = "测试连接";

            if (success)
            {
                testResultLabel.Text = "✓ 连接成功";
                testResultLabel.Modulate = new Color(0, 1, 0); // 绿色
                GD.Print("连接测试成功: " + message);
            }
            else
            {
                // 如果错误信息太长，插入换行以便在UI中显示
                string wrapped = WrapText(message ?? "未知错误", 48);
                testResultLabel.Text = "✗ 连接失败: \n" + wrapped;
                testResultLabel.Modulate = new Color(1, 0, 0); // 红色
                GD.PrintErr("连接测试失败: " + message);
            }

            // 清理临时客户端
            testClient.QueueFree();
        });

    }

    private static string WrapText(string text, int maxLineLength)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new System.Text.StringBuilder();
        int pos = 0;
        while (pos < text.Length)
        {
            // 如果剩余长度小于maxLineLength，直接追加剩余并退出
            int len = Math.Min(maxLineLength, text.Length - pos);

            // 优先在本段中寻找最后一个空白以换行
            int lastSpace = -1;
            for (int i = 0; i < len; i++)
            {
                if (char.IsWhiteSpace(text[pos + i]))
                    lastSpace = i;
            }

            if (pos + len < text.Length && lastSpace > 0)
            {
                sb.Append(text.Substring(pos, lastSpace).TrimEnd());
                sb.Append('\n');
                pos += lastSpace + 1; // 跳过空白
            }
            else
            {
                sb.Append(text.Substring(pos, len).TrimEnd());
                pos += len;
                if (pos < text.Length)
                    sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}