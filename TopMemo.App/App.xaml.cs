using TopMemo.App.Controllers;
using TopMemo.App.Services;
using WpfApplication = System.Windows.Application;

namespace TopMemo.App;

/// <summary>
/// TopMemo アプリケーションの起動/終了を管理します。
/// </summary>
public partial class App : WpfApplication
{
    private SingleInstanceService? _singleInstanceService;
    private TopMemoController? _controller;

    /// <summary>
    /// 起動時処理です。
    /// </summary>
    /// <param name="eventArgs">起動引数。</param>
    protected override void OnStartup(System.Windows.StartupEventArgs eventArgs)
    {
        // 起動モードを明示的終了に設定します。
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        base.OnStartup(eventArgs);

        // 単一インスタンス制御を初期化します。
        _singleInstanceService = new SingleInstanceService("TopMemo");
        if (!_singleInstanceService.TryAcquirePrimary())
        {
            _singleInstanceService.NotifyExistingInstance();
            Shutdown();
            return;
        }

        // 画面とコントローラを初期化します。
        var window = new MainWindow();
        _controller = new TopMemoController(this, window);
        _controller.Initialize();

        // セカンダリ起動通知の待受を開始します。
        _singleInstanceService.StartListening(() => _controller.ShowEditor());
    }

    /// <summary>
    /// 終了時処理です。
    /// </summary>
    /// <param name="eventArgs">終了引数。</param>
    protected override void OnExit(System.Windows.ExitEventArgs eventArgs)
    {
        // 各サービスを解放します。
        _controller?.Dispose();
        _singleInstanceService?.Dispose();
        base.OnExit(eventArgs);
    }
}

