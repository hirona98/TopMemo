using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using WpfApplication = System.Windows.Application;

namespace TopMemo.App.Services;

/// <summary>
/// 単一インスタンス制御と既存インスタンス通知を提供するサービスです。
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listeningCts;
    private Task? _listeningTask;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="applicationId">アプリ識別子。</param>
    public SingleInstanceService(string applicationId)
    {
        // ミューテックス名とパイプ名を組み立てます。
        _mutexName = $@"Global\{applicationId}.Singleton";
        _pipeName = $"{applicationId}.Pipe";
    }

    /// <summary>
    /// プライマリインスタンスの取得を試みます。
    /// </summary>
    /// <returns>プライマリなら true。</returns>
    public bool TryAcquirePrimary()
    {
        // ミューテックスを生成して所有状態を確認します。
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        // セカンダリ側は所有していないため破棄します。
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    /// <summary>
    /// セカンダリインスタンスから既存インスタンスへ表示要求を送信します。
    /// </summary>
    public void NotifyExistingInstance()
    {
        try
        {
            // 既存インスタンスの named pipe へ接続します。
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 300);

            // SHOW メッセージを送信します。
            var payload = Encoding.UTF8.GetBytes("SHOW\n");
            client.Write(payload, 0, payload.Length);
            client.Flush();
        }
        catch
        {
            // 通知失敗時は何もしません。
        }
    }

    /// <summary>
    /// 既存インスタンス通知の待受を開始します。
    /// </summary>
    /// <param name="onShowRequested">表示要求時の処理。</param>
    public void StartListening(Action onShowRequested)
    {
        // 既存待受を停止してから新規開始します。
        StopListening();
        _listeningCts = new CancellationTokenSource();
        var token = _listeningCts.Token;

        // バックグラウンドで named pipe 待受ループを開始します。
        _listeningTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1接続ごとにサーバーを作成します。
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    // 接続待ちを行います。
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    if (!server.IsConnected)
                    {
                        continue;
                    }

                    // 受信データを読み取ります。
                    using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                    var message = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (!string.Equals(message, "SHOW", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // UI スレッドで表示処理を呼び出します。
                    var dispatcher = WpfApplication.Current?.Dispatcher;
                    if (dispatcher is null)
                    {
                        continue;
                    }

                    await dispatcher.InvokeAsync(onShowRequested);
                }
                catch (OperationCanceledException)
                {
                    // 停止要求時はループを抜けます。
                    break;
                }
                catch
                {
                    // 通信失敗時は継続します。
                }
            }
        }, token);
    }

    /// <summary>
    /// 待受処理を停止します。
    /// </summary>
    public void StopListening()
    {
        // 待受キャンセルを通知します。
        if (_listeningCts is null)
        {
            return;
        }

        _listeningCts.Cancel();

        // 終了待機してリソースを解放します。
        try
        {
            _listeningTask?.Wait(500);
        }
        catch
        {
            // 停止時例外は無視します。
        }
        finally
        {
            _listeningCts.Dispose();
            _listeningCts = null;
            _listeningTask = null;
        }
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // 待受を停止します。
        StopListening();

        // ミューテックス所有を解放します。
        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // 解放失敗は無視します。
        }
        finally
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
