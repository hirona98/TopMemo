using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace TopMemo.App.Services;

/// <summary>
/// システムトレイアイコンとメニューを管理するサービスです。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleWindowMenuItem;
    private readonly Forms.ToolStripMenuItem _autoStartMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private bool _suppressAutoStartEvent;
    private bool _disposed;

    /// <summary>
    /// 初期化します。
    /// </summary>
    public TrayService()
    {
        // トレイアイコンを生成します。
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "TopMemo",
            Visible = true
        };

        // メニュー項目を生成します。
        _toggleWindowMenuItem = new Forms.ToolStripMenuItem("表示/非表示");
        _autoStartMenuItem = new Forms.ToolStripMenuItem("自動起動") { CheckOnClick = true };
        _exitMenuItem = new Forms.ToolStripMenuItem("終了");

        // メニューイベントを購読します。
        _toggleWindowMenuItem.Click += (_, _) => ToggleWindowRequested?.Invoke();
        _autoStartMenuItem.CheckedChanged += (_, _) =>
        {
            // 内部反映中はイベント通知を抑止します。
            if (_suppressAutoStartEvent)
            {
                return;
            }

            AutoStartToggled?.Invoke(_autoStartMenuItem.Checked);
        };
        _exitMenuItem.Click += (_, _) => ExitRequested?.Invoke();

        // コンテキストメニューを組み立てます。
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add(_toggleWindowMenuItem);
        contextMenu.Items.Add(_autoStartMenuItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(_exitMenuItem);
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    /// <summary>
    /// 表示/非表示要求イベントです。
    /// </summary>
    public event Action? ToggleWindowRequested;

    /// <summary>
    /// 自動起動トグルイベントです。
    /// </summary>
    public event Action<bool>? AutoStartToggled;

    /// <summary>
    /// 終了要求イベントです。
    /// </summary>
    public event Action? ExitRequested;

    /// <summary>
    /// 自動起動チェック状態を反映します。
    /// </summary>
    /// <param name="enabled">有効状態。</param>
    public void SetAutoStartState(bool enabled)
    {
        // 内部更新で通知ループを防止します。
        _suppressAutoStartEvent = true;
        _autoStartMenuItem.Checked = enabled;
        _suppressAutoStartEvent = false;
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // 多重解放を防止します。
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // アイコンを非表示化してメニューも解放します。
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
