using System.IO;

namespace TopMemo.App.Services;

/// <summary>
/// TopMemo の保存パスを管理するサービスです。
/// </summary>
public sealed class FilePathService
{
    /// <summary>
    /// 初期化します。
    /// </summary>
    public FilePathService()
    {
        // 実行フォルダを基準ディレクトリとして保持します。
        BaseDirectory = AppContext.BaseDirectory;
        SettingsPath = Path.Combine(BaseDirectory, "settings.json");
        TabsPath = Path.Combine(BaseDirectory, "tabs.json");
        MemosDirectory = Path.Combine(BaseDirectory, "memos");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
        LogPath = Path.Combine(LogsDirectory, "app.log");
        BackupLogPath = Path.Combine(LogsDirectory, "app.log.1");
    }

    /// <summary>
    /// アプリ基準ディレクトリを取得します。
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// 設定ファイルパスを取得します。
    /// </summary>
    public string SettingsPath { get; }

    /// <summary>
    /// タブ管理ファイルパスを取得します。
    /// </summary>
    public string TabsPath { get; }

    /// <summary>
    /// メモ保存ディレクトリを取得します。
    /// </summary>
    public string MemosDirectory { get; }

    /// <summary>
    /// ログ保存ディレクトリを取得します。
    /// </summary>
    public string LogsDirectory { get; }

    /// <summary>
    /// 現在ログファイルパスを取得します。
    /// </summary>
    public string LogPath { get; }

    /// <summary>
    /// バックアップログファイルパスを取得します。
    /// </summary>
    public string BackupLogPath { get; }

    /// <summary>
    /// メモファイルの完全パスを返します。
    /// </summary>
    /// <param name="fileName">ファイル名。</param>
    /// <returns>完全パス。</returns>
    public string GetMemoPath(string fileName)
    {
        // memos 配下に保存先を組み立てます。
        return Path.Combine(MemosDirectory, fileName);
    }

    /// <summary>
    /// 必要なディレクトリを作成します。
    /// </summary>
    public void EnsureDirectories()
    {
        // メモとログのディレクトリを作成します。
        Directory.CreateDirectory(MemosDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}

