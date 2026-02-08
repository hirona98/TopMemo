using System.IO;
using System.Text;
using System.Text.Json;
using TopMemo.App.Models;

namespace TopMemo.App.Services;

/// <summary>
/// JSON とメモ本文ファイルを扱う保存サービスです。
/// </summary>
public sealed class JsonStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly FilePathService _filePathService;
    private readonly LoggingService _loggingService;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="filePathService">パスサービス。</param>
    /// <param name="loggingService">ログサービス。</param>
    public JsonStorageService(FilePathService filePathService, LoggingService loggingService)
    {
        // 依存関係を保持します。
        _filePathService = filePathService;
        _loggingService = loggingService;

        // 必要なディレクトリを作成します。
        _filePathService.EnsureDirectories();
    }

    /// <summary>
    /// 設定を読み込み、破損時は既定値を再生成します。
    /// </summary>
    /// <returns>設定。</returns>
    public AppSettings LoadSettingsOrCreate()
    {
        // ファイルが無い場合は既定値を作成します。
        if (!File.Exists(_filePathService.SettingsPath))
        {
            var defaults = AppSettings.CreateDefault();
            SaveSettings(defaults);
            return defaults;
        }

        try
        {
            // JSON を読み込んで検証します。
            var json = File.ReadAllText(_filePathService.SettingsPath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (!IsValid(settings))
            {
                throw new InvalidDataException("settings.json format is invalid.");
            }

            return settings!;
        }
        catch (Exception exception)
        {
            // 破損時は既定値へ戻して再保存します。
            _loggingService.Error("settings.json の読み込みに失敗したため既定値で再生成します。", exception);
            var defaults = AppSettings.CreateDefault();
            SaveSettings(defaults);
            return defaults;
        }
    }

    /// <summary>
    /// 設定を保存します。
    /// </summary>
    /// <param name="settings">設定。</param>
    public void SaveSettings(AppSettings settings)
    {
        // JSON を整形して保存します。
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePathService.SettingsPath, json, Encoding.UTF8);
    }

    /// <summary>
    /// タブ情報を読み込み、破損時は既定値を再生成します。
    /// </summary>
    /// <returns>タブ状態。</returns>
    public TabsState LoadTabsOrCreate()
    {
        // ファイルが無い場合は既定値を作成します。
        if (!File.Exists(_filePathService.TabsPath))
        {
            var defaults = TabsState.CreateDefault();
            SaveTabs(defaults);
            EnsureMemoFileExists(defaults.Tabs[0].FileName);
            return defaults;
        }

        try
        {
            // JSON を読み込んで検証します。
            var json = File.ReadAllText(_filePathService.TabsPath, Encoding.UTF8);
            var tabsState = JsonSerializer.Deserialize<TabsState>(json, JsonOptions);
            if (!IsValid(tabsState))
            {
                throw new InvalidDataException("tabs.json format is invalid.");
            }

            // 欠落本文を空で生成します。
            foreach (var tab in tabsState!.Tabs)
            {
                EnsureMemoFileExists(tab.FileName);
            }

            return tabsState;
        }
        catch (Exception exception)
        {
            // 破損時は既定値へ戻して再保存します。
            _loggingService.Error("tabs.json の読み込みに失敗したため既定値で再生成します。", exception);
            var defaults = TabsState.CreateDefault();
            SaveTabs(defaults);
            EnsureMemoFileExists(defaults.Tabs[0].FileName);
            return defaults;
        }
    }

    /// <summary>
    /// タブ情報を保存します。
    /// </summary>
    /// <param name="tabsState">タブ状態。</param>
    public void SaveTabs(TabsState tabsState)
    {
        // JSON を整形して保存します。
        var json = JsonSerializer.Serialize(tabsState, JsonOptions);
        File.WriteAllText(_filePathService.TabsPath, json, Encoding.UTF8);
    }

    /// <summary>
    /// メモ本文を読み込みます。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    /// <returns>本文。</returns>
    public string LoadMemo(string fileName)
    {
        // ファイルが無ければ空で作成して返します。
        EnsureMemoFileExists(fileName);
        return File.ReadAllText(_filePathService.GetMemoPath(fileName), Encoding.UTF8);
    }

    /// <summary>
    /// メモ本文を保存します。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    /// <param name="content">本文。</param>
    public void SaveMemo(string fileName, string content)
    {
        // 本文を UTF-8 で上書き保存します。
        File.WriteAllText(_filePathService.GetMemoPath(fileName), content, Encoding.UTF8);
    }

    /// <summary>
    /// メモファイルを改名します。
    /// </summary>
    /// <param name="oldFileName">旧ファイル名。</param>
    /// <param name="newFileName">新ファイル名。</param>
    public void RenameMemo(string oldFileName, string newFileName)
    {
        // 旧ファイルを確実に用意します。
        EnsureMemoFileExists(oldFileName);

        // 同名なら処理不要です。
        if (string.Equals(oldFileName, newFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 旧ファイルを新名へ移動します。
        File.Move(_filePathService.GetMemoPath(oldFileName), _filePathService.GetMemoPath(newFileName), overwrite: false);
    }

    /// <summary>
    /// メモファイルを削除します。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    public void DeleteMemo(string fileName)
    {
        // 対象ファイルがあれば削除します。
        var path = _filePathService.GetMemoPath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// ファイル存在有無を返します。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    /// <returns>存在する場合は true。</returns>
    public bool MemoExists(string fileName)
    {
        // 保存先の存在確認を返します。
        return File.Exists(_filePathService.GetMemoPath(fileName));
    }

    /// <summary>
    /// メモファイルが無い場合に空ファイルを作成します。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    private void EnsureMemoFileExists(string fileName)
    {
        // ファイルが無い時だけ空で作成します。
        var path = _filePathService.GetMemoPath(fileName);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, string.Empty, Encoding.UTF8);
        }
    }

    /// <summary>
    /// 設定値の必須項目を検証します。
    /// </summary>
    /// <param name="settings">検証対象。</param>
    /// <returns>有効なら true。</returns>
    private static bool IsValid(AppSettings? settings)
    {
        // null や必須欠落を確認します。
        if (settings is null ||
            settings.HotZones is null ||
            settings.EditorWindow is null ||
            settings.Behavior is null ||
            settings.Startup is null ||
            settings.Logging is null ||
            settings.Theme is null)
        {
            return false;
        }

        // 範囲値を確認します。
        if (settings.HotZones.ShowEditor.Width <= 0 ||
            settings.HotZones.ShowEditor.Height <= 0 ||
            settings.HotZones.SendTaskView.Width <= 0 ||
            settings.HotZones.SendTaskView.Height <= 0 ||
            settings.EditorWindow.Width <= 0 ||
            settings.EditorWindow.Height <= 0 ||
            settings.Behavior.TaskViewCooldownMs < 0 ||
            settings.Logging.MaxFileSizeKb <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// タブ状態の必須項目を検証します。
    /// </summary>
    /// <param name="tabsState">検証対象。</param>
    /// <returns>有効なら true。</returns>
    private static bool IsValid(TabsState? tabsState)
    {
        // null と空タブを確認します。
        if (tabsState is null || tabsState.Tabs is null || tabsState.Tabs.Count == 0)
        {
            return false;
        }

        // アクティブ ID が存在するかを確認します。
        if (string.IsNullOrWhiteSpace(tabsState.ActiveTabId))
        {
            return false;
        }

        // 各タブの必須値を確認します。
        foreach (var tab in tabsState.Tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.Id) ||
                string.IsNullOrWhiteSpace(tab.Title) ||
                string.IsNullOrWhiteSpace(tab.FileName))
            {
                return false;
            }
        }

        // アクティブ ID が一覧に存在するかを確認します。
        return tabsState.Tabs.Any(tab => tab.Id == tabsState.ActiveTabId);
    }
}

