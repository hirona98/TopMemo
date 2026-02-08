using System.Windows.Threading;
using TopMemo.App.Infrastructure;
using TopMemo.App.Models;
using NativePoint = TopMemo.App.Infrastructure.Point;
using NativeRect = TopMemo.App.Infrastructure.Rect;

namespace TopMemo.App.Services;

/// <summary>
/// カーソル監視とホットゾーン判定を行うサービスです。
/// </summary>
internal sealed class HotZoneMonitorService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<bool> _isEditorVisible;
    private readonly Func<NativeRect?> _getEditorRect;
    private readonly AppSettings _settings;
    private bool _wasInShowZone;
    private bool _wasInTaskViewZone;
    private bool _hasEnteredEditorSinceShown;
    private DateTime _taskViewReadyAtUtc = DateTime.MinValue;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="settings">設定。</param>
    /// <param name="isEditorVisible">エディタ表示状態取得。</param>
    /// <param name="getEditorRect">エディタ矩形取得。</param>
    public HotZoneMonitorService(AppSettings settings, Func<bool> isEditorVisible, Func<NativeRect?> getEditorRect)
    {
        // 依存関係を保持します。
        _settings = settings;
        _isEditorVisible = isEditorVisible;
        _getEditorRect = getEditorRect;

        // 監視タイマーを初期化します。
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// 表示ホットゾーン侵入イベントです。
    /// </summary>
    public event Action? ShowZoneEntered;

    /// <summary>
    /// Win+Tab ホットゾーン侵入イベントです。
    /// </summary>
    public event Action? TaskViewZoneEntered;

    /// <summary>
    /// エディタ領域外へ出たイベントです。
    /// </summary>
    public event Action? EditorExited;

    /// <summary>
    /// 監視を開始します。
    /// </summary>
    public void Start()
    {
        // タイマー監視を有効化します。
        _timer.Start();
    }

    /// <summary>
    /// 監視を停止します。
    /// </summary>
    public void Stop()
    {
        // タイマー監視を停止します。
        _timer.Stop();
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // イベント購読を解除して停止します。
        _timer.Tick -= OnTick;
        _timer.Stop();
    }

    /// <summary>
    /// 監視ティック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void OnTick(object? sender, EventArgs eventArgs)
    {
        // カーソル位置が取れない場合は処理しません。
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        // Win+Tab と表示ゾーンの侵入判定を行います。
        var inTaskViewZone = IsInZone(point, _settings.HotZones.SendTaskView);
        var inShowZone = !inTaskViewZone && IsInZone(point, _settings.HotZones.ShowEditor);

        // Win+Tab は重複時優先で発火します。
        if (inTaskViewZone && !_wasInTaskViewZone && DateTime.UtcNow >= _taskViewReadyAtUtc)
        {
            TaskViewZoneEntered?.Invoke();
            _taskViewReadyAtUtc = DateTime.UtcNow.AddMilliseconds(Math.Max(0, _settings.Behavior.TaskViewCooldownMs));
        }

        // 表示ゾーン侵入時に表示要求を発火します。
        if (inShowZone && !_wasInShowZone)
        {
            ShowZoneEntered?.Invoke();
        }

        // エディタ表示中は領域外移動を監視します。
        if (_isEditorVisible())
        {
            var rect = _getEditorRect();
            if (rect is not null)
            {
                var isInsideEditor = rect.Value.Contains(point);
                if (isInsideEditor)
                {
                    _hasEnteredEditorSinceShown = true;
                }
                else if (_hasEnteredEditorSinceShown)
                {
                    EditorExited?.Invoke();
                    _hasEnteredEditorSinceShown = false;
                }
            }
        }
        else
        {
            // 非表示中は状態を初期化します。
            _hasEnteredEditorSinceShown = false;
        }

        // 次回比較用状態を更新します。
        _wasInTaskViewZone = inTaskViewZone;
        _wasInShowZone = inShowZone;
    }

    /// <summary>
    /// 座標がゾーン内かを判定します。
    /// </summary>
    /// <param name="point">カーソル座標。</param>
    /// <param name="zone">判定ゾーン。</param>
    /// <returns>内包する場合は true。</returns>
    private static bool IsInZone(NativePoint point, ZoneSetting zone)
    {
        // 矩形内包判定を実施します。
        var right = zone.X + zone.Width;
        var bottom = zone.Y + zone.Height;
        return point.X >= zone.X && point.X < right && point.Y >= zone.Y && point.Y < bottom;
    }
}
