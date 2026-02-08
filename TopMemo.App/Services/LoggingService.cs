using System.Globalization;
using System.IO;
using System.Text;

namespace TopMemo.App.Services;

/// <summary>
/// ファイル出力ログサービスです。
/// </summary>
public sealed class LoggingService
{
    private readonly object _lock = new();
    private readonly FilePathService _filePathService;
    private readonly int _maxBytes;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="filePathService">パスサービス。</param>
    /// <param name="maxFileSizeKb">ログ最大サイズ（KB）。</param>
    public LoggingService(FilePathService filePathService, int maxFileSizeKb = 100)
    {
        // 依存関係と上限値を保持します。
        _filePathService = filePathService;
        _maxBytes = Math.Max(1, maxFileSizeKb) * 1024;

        // 出力先ディレクトリを準備します。
        _filePathService.EnsureDirectories();
    }

    /// <summary>
    /// 情報ログを出力します。
    /// </summary>
    /// <param name="message">メッセージ。</param>
    public void Info(string message)
    {
        // INFO レベルとして記録します。
        Write("INFO", message, null);
    }

    /// <summary>
    /// エラーログを出力します。
    /// </summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="exception">例外。</param>
    public void Error(string message, Exception? exception = null)
    {
        // ERROR レベルとして記録します。
        Write("ERROR", message, exception);
    }

    /// <summary>
    /// ログを1行追加します。
    /// </summary>
    /// <param name="level">ログレベル。</param>
    /// <param name="message">メッセージ。</param>
    /// <param name="exception">例外。</param>
    private void Write(string level, string message, Exception? exception)
    {
        // 同時書き込みを防止します。
        lock (_lock)
        {
            try
            {
                // ローテーション要否を先に判定します。
                RotateIfNeeded();

                // 出力行を作成します。
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                var builder = new StringBuilder();
                builder.Append('[').Append(timestamp).Append("] ");
                builder.Append('[').Append(level).Append("] ");
                builder.AppendLine(message);

                // 例外詳細がある場合は追記します。
                if (exception is not null)
                {
                    builder.AppendLine(exception.ToString());
                }

                // UTF-8 でファイルへ追記します。
                File.AppendAllText(_filePathService.LogPath, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
                // ログ書き込み失敗時は二次障害防止のため握りつぶします。
            }
        }
    }

    /// <summary>
    /// サイズ上限超過時にログをローテーションします。
    /// </summary>
    private void RotateIfNeeded()
    {
        // ログが未作成なら処理不要です。
        if (!File.Exists(_filePathService.LogPath))
        {
            return;
        }

        // 現在サイズを確認します。
        var fileInfo = new FileInfo(_filePathService.LogPath);
        if (fileInfo.Length < _maxBytes)
        {
            return;
        }

        // 既存のバックアップを削除して世代を維持します。
        if (File.Exists(_filePathService.BackupLogPath))
        {
            File.Delete(_filePathService.BackupLogPath);
        }

        // 現行ログをバックアップ名へ移動します。
        File.Move(_filePathService.LogPath, _filePathService.BackupLogPath);
    }
}

