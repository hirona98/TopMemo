using System.IO;
using System.Runtime.InteropServices;

namespace TopMemo.App.Services;

/// <summary>
/// Windows 自動起動登録を管理するサービスです。
/// </summary>
public sealed class StartupRegistrationService
{
    private const string StartupEntryName = "TopMemo";
    private readonly string _executablePath;

    /// <summary>
    /// 初期化します。
    /// </summary>
    public StartupRegistrationService()
    {
        // 実行ファイルの絶対パスを保持します。
        _executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is not available.");
    }

    /// <summary>
    /// 自動起動を有効化します。
    /// </summary>
    /// <param name="errorMessage">失敗メッセージ。</param>
    /// <returns>成功した場合は true。</returns>
    public bool Enable(out string errorMessage)
    {
        // 既定戻り値を設定します。
        errorMessage = string.Empty;

        try
        {
            // スタートアップフォルダへの .lnk 作成を試行します。
            CreateStartupShortcut();
            return true;
        }
        catch (Exception startupException)
        {
            // 失敗時はエラーを返します。
            errorMessage = startupException.Message;
            return false;
        }
    }

    /// <summary>
    /// 自動起動を無効化します。
    /// </summary>
    /// <param name="errorMessage">失敗メッセージ。</param>
    /// <returns>成功した場合は true。</returns>
    public bool Disable(out string errorMessage)
    {
        // 既定戻り値を設定します。
        errorMessage = string.Empty;

        try
        {
            // スタートアップフォルダ登録を解除します。
            DeleteStartupShortcutIfExists();
            return true;
        }
        catch (Exception exception)
        {
            // 解除失敗を返します。
            errorMessage = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 現在の自動起動有効状態を判定します。
    /// </summary>
    /// <returns>有効なら true。</returns>
    public bool IsEnabled()
    {
        // スタートアップフォルダ登録の有無を返します。
        return File.Exists(GetShortcutPath());
    }

    /// <summary>
    /// スタートアップショートカットを作成します。
    /// </summary>
    private void CreateStartupShortcut()
    {
        // COM 経由で .lnk を作成します。
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        var shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Failed to instantiate WScript.Shell.");
        try
        {
            dynamic dynamicShell = shell;
            dynamic shortcut = dynamicShell.CreateShortcut(GetShortcutPath());
            try
            {
                // ショートカットのリンク先と作業ディレクトリを設定します。
                shortcut.TargetPath = _executablePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(_executablePath);
                shortcut.Description = "TopMemo AutoStart";
                shortcut.Save();
            }
            finally
            {
                // COM オブジェクトを解放します。
                Marshal.FinalReleaseComObject(shortcut);
            }
        }
        finally
        {
            // COM オブジェクトを解放します。
            Marshal.FinalReleaseComObject(shell);
        }

        // 作成確認を行います。
        if (!File.Exists(GetShortcutPath()))
        {
            throw new IOException("Failed to create startup shortcut.");
        }
    }

    /// <summary>
    /// スタートアップショートカットを削除します。
    /// </summary>
    private void DeleteStartupShortcutIfExists()
    {
        // 既存ショートカットのみ削除します。
        var shortcutPath = GetShortcutPath();
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    /// <summary>
    /// スタートアップショートカットパスを返します。
    /// </summary>
    /// <returns>ショートカットパス。</returns>
    private static string GetShortcutPath()
    {
        // スタートアップフォルダ配下の lnk パスを返します。
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, $"{StartupEntryName}.lnk");
    }
}
