using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TopMemo.App.Services;

/// <summary>
/// Windows 自動起動登録を管理するサービスです。
/// </summary>
public sealed class StartupRegistrationService
{
    private const string StartupEntryName = "TopMemo";
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
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
    /// <param name="allowRegistryFallback">レジストリフォールバック可否。</param>
    /// <param name="provider">使用したプロバイダ。</param>
    /// <param name="errorMessage">失敗メッセージ。</param>
    /// <returns>成功した場合は true。</returns>
    public bool Enable(bool allowRegistryFallback, out string provider, out string errorMessage)
    {
        // 既定戻り値を設定します。
        provider = "None";
        errorMessage = string.Empty;

        try
        {
            // まずスタートアップフォルダへの .lnk 作成を試行します。
            CreateStartupShortcut();
            provider = "StartupFolder";
            return true;
        }
        catch (Exception startupException)
        {
            // フォールバック不可なら失敗を返します。
            if (!allowRegistryFallback)
            {
                errorMessage = startupException.Message;
                return false;
            }

            try
            {
                // フォールバックとして HKCU\Run を登録します。
                EnableRegistryRun();
                provider = "Registry";
                return true;
            }
            catch (Exception registryException)
            {
                // 両方失敗した場合は複合メッセージを返します。
                errorMessage = $"StartupFolder: {startupException.Message} / Registry: {registryException.Message}";
                return false;
            }
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
            // 両方式の登録を解除します。
            DeleteStartupShortcutIfExists();
            DisableRegistryRunIfExists();
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
        // どちらかの登録が有効なら true を返します。
        return File.Exists(GetShortcutPath()) || RegistryHasEntry();
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
    /// レジストリの Run 登録を有効化します。
    /// </summary>
    private void EnableRegistryRun()
    {
        // HKCU\Run にエントリを書き込みます。
        using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);
        if (runKey is null)
        {
            throw new InvalidOperationException("Failed to open HKCU Run key.");
        }

        runKey.SetValue(StartupEntryName, $"\"{_executablePath}\"", RegistryValueKind.String);
    }

    /// <summary>
    /// レジストリ Run 登録を削除します。
    /// </summary>
    private void DisableRegistryRunIfExists()
    {
        // エントリが存在する場合のみ削除します。
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
        if (runKey?.GetValue(StartupEntryName) is not null)
        {
            runKey.DeleteValue(StartupEntryName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// レジストリエントリ存在有無を返します。
    /// </summary>
    /// <returns>存在する場合は true。</returns>
    private static bool RegistryHasEntry()
    {
        // HKCU\Run のエントリ存在を確認します。
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
        return runKey?.GetValue(StartupEntryName) is not null;
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

