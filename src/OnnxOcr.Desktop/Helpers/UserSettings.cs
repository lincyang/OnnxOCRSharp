//-----------------------------------------------------------------------
// <copyright file="UserSettings.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using OnnxOcr.Core.Configuration;

namespace OnnxOcr.Desktop.Helpers;

internal sealed class UserSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string SelectedPreset { get; set; } = nameof(OcrModelPreset.PpOcrV6Tiny);

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnnxOCRSharp");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 忽略本地偏好写入失败，不影响主流程
        }
    }

    public OcrModelPreset GetPresetOrDefault()
    {
        return Enum.TryParse<OcrModelPreset>(SelectedPreset, ignoreCase: true, out var preset)
            ? preset
            : OcrModelPreset.PpOcrV6Tiny;
    }

    public static void SavePreset(OcrModelPreset preset)
    {
        var settings = Load();
        settings.SelectedPreset = preset.ToString();
        settings.Save();
    }
}
