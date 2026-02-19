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
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AutoHideIfNotHoveredDelay = TimeSpan.FromSeconds(2);
    private readonly DispatcherTimer _timer;
    private readonly Func<bool> _isEditorVisible;
    private readonly Func<NativeRect?> _getEditorRect;
    private readonly AppSettings _settings;
    private bool _wasInShowZone;
    private bool _wasInTaskViewZone;
    private bool _hasEnteredEditorSinceShown;
    private bool _hasRaisedEditorExited;
    private DateTime? _outsideEditorSinceUtc;
    private DateTime? _editorShownAtUtc;
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
        var inTaskViewZone = IsInTaskViewZone(point, _settings.HotZones.SendTaskView);
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
            // 表示開始時刻を初回のみ記録します。
            _editorShownAtUtc ??= DateTime.UtcNow;

            var rect = _getEditorRect();
            if (rect is not null)
            {
                // マウス操作中は誤非表示防止のため退出判定を停止します。
                if (IsLeftMouseButtonPressed())
                {
                    _outsideEditorSinceUtc = null;
                    _hasRaisedEditorExited = false;
                }
                else
                {
                    // エディタ内外の状態を判定します。
                    var isInsideEditor = rect.Value.Contains(point);
                    if (isInsideEditor)
                    {
                        // 一度でもエディタ内へ入った状態を記録します。
                        _hasEnteredEditorSinceShown = true;
                        _outsideEditorSinceUtc = null;
                        _hasRaisedEditorExited = false;
                    }
                    else if (_hasEnteredEditorSinceShown && !_hasRaisedEditorExited)
                    {
                        // エディタ外へ出た時刻を初回のみ記録します。
                        _outsideEditorSinceUtc ??= DateTime.UtcNow;

                        // 0.5秒継続して外にいる場合のみ非表示イベントを発火します。
                        var elapsed = DateTime.UtcNow - _outsideEditorSinceUtc.Value;
                        if (elapsed >= HideDelay)
                        {
                            EditorExited?.Invoke();
                            _hasRaisedEditorExited = true;
                        }
                    }
                }
            }

            // 一度もエディタへ入っていない場合は 2 秒で自動非表示します。
            if (!_hasEnteredEditorSinceShown && !_hasRaisedEditorExited && _editorShownAtUtc is not null)
            {
                var elapsedSinceShown = DateTime.UtcNow - _editorShownAtUtc.Value;
                if (elapsedSinceShown >= AutoHideIfNotHoveredDelay)
                {
                    EditorExited?.Invoke();
                    _hasRaisedEditorExited = true;
                }
            }
        }
        else
        {
            // 非表示中は状態を初期化します。
            _hasEnteredEditorSinceShown = false;
            _hasRaisedEditorExited = false;
            _outsideEditorSinceUtc = null;
            _editorShownAtUtc = null;
        }

        // 次回比較用状態を更新します。
        _wasInTaskViewZone = inTaskViewZone;
        _wasInShowZone = inShowZone;
    }

    /// <summary>
    /// 左マウスボタンが押下中かを判定します。
    /// </summary>
    /// <returns>押下中なら true。</returns>
    private static bool IsLeftMouseButtonPressed()
    {
        // GetAsyncKeyState の最上位ビットで押下状態を判定します。
        return (NativeMethods.GetAsyncKeyState(NativeMethods.VkLbutton) & 0x8000) != 0;
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

    /// <summary>
    /// 座標が Win+Tab 用の左下ゾーン内かを判定します。
    /// </summary>
    /// <param name="point">カーソル座標。</param>
    /// <param name="zone">ゾーン設定（幅・高さのみ使用）。</param>
    /// <returns>内包する場合は true。</returns>
    private static bool IsInTaskViewZone(NativePoint point, ZoneSetting zone)
    {
        // 主ディスプレイ左下基準でゾーン矩形を計算します。
        var zoneWidth = Math.Max(1, zone.Width);
        var zoneHeight = Math.Max(1, zone.Height);
        var left = 0;
        var top = 0;
        var primaryHeight = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen));
        var zoneTop = top + primaryHeight - zoneHeight;
        var zoneRight = left + zoneWidth;
        var zoneBottom = zoneTop + zoneHeight;

        // 矩形内包判定を実施します。
        return point.X >= left && point.X < zoneRight && point.Y >= zoneTop && point.Y < zoneBottom;
    }
}
