namespace TopMemo.App.Models;

/// <summary>
/// アプリ全体の設定を保持するモデルです。
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// ホットゾーン設定を取得または設定します。
    /// </summary>
    public HotZonesSettings HotZones { get; set; } = new();

    /// <summary>
    /// エディタウィンドウ位置設定を取得または設定します。
    /// </summary>
    public EditorWindowSettings EditorWindow { get; set; } = new();

    /// <summary>
    /// 挙動設定を取得または設定します。
    /// </summary>
    public BehaviorSettings Behavior { get; set; } = new();

    /// <summary>
    /// ログ設定を取得または設定します。
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();

    /// <summary>
    /// テーマ設定を取得または設定します。
    /// </summary>
    public ThemeSettings Theme { get; set; } = new();

    /// <summary>
    /// 既定設定を生成します。
    /// </summary>
    /// <returns>既定の <see cref="AppSettings"/>。</returns>
    public static AppSettings CreateDefault()
    {
        // 既定値を固定で返します。
        return new AppSettings
        {
            HotZones = new HotZonesSettings
            {
                ShowEditor = new ZoneSetting { X = 0, Y = 0, Width = 12, Height = 12 },
                SendTaskView = new ZoneSetting { X = 0, Y = 1070, Width = 12, Height = 12 }
            },
            EditorWindow = new EditorWindowSettings
            {
                X = 20,
                Y = 20,
                Width = 720,
                Height = 480
            },
            Behavior = new BehaviorSettings
            {
                TopMost = true,
                AutoStartEnabled = false,
                TaskViewCooldownMs = 300
            },
            Logging = new LoggingSettings
            {
                MaxFileSizeKb = 100,
                RotateFileCount = 2
            },
            Theme = new ThemeSettings
            {
                MarkdownColorProfile = "Default"
            }
        };
    }
}

/// <summary>
/// ホットゾーン関連設定です。
/// </summary>
public sealed class HotZonesSettings
{
    /// <summary>
    /// エディタ表示ゾーンを取得または設定します。
    /// </summary>
    public ZoneSetting ShowEditor { get; set; } = new();

    /// <summary>
    /// Win+Tab 送出ゾーンを取得または設定します。
    /// </summary>
    public ZoneSetting SendTaskView { get; set; } = new();
}

/// <summary>
/// 矩形ゾーン設定です。
/// </summary>
public sealed class ZoneSetting
{
    /// <summary>
    /// X 座標を取得または設定します。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y 座標を取得または設定します。
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 幅を取得または設定します。
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高さを取得または設定します。
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// エディタウィンドウ設定です。
/// </summary>
public sealed class EditorWindowSettings
{
    /// <summary>
    /// X 座標を取得または設定します。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y 座標を取得または設定します。
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 幅を取得または設定します。
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 高さを取得または設定します。
    /// </summary>
    public double Height { get; set; }
}

/// <summary>
/// 実行時挙動設定です。
/// </summary>
public sealed class BehaviorSettings
{
    /// <summary>
    /// 最前面固定を取得または設定します。
    /// </summary>
    public bool TopMost { get; set; }

    /// <summary>
    /// 自動起動の有効状態を取得または設定します。
    /// </summary>
    public bool AutoStartEnabled { get; set; }

    /// <summary>
    /// Win+Tab 再発火抑制時間（ミリ秒）を取得または設定します。
    /// </summary>
    public int TaskViewCooldownMs { get; set; }
}

/// <summary>
/// ログ出力設定です。
/// </summary>
public sealed class LoggingSettings
{
    /// <summary>
    /// ファイルサイズ上限（KB）を取得または設定します。
    /// </summary>
    public int MaxFileSizeKb { get; set; } = 100;

    /// <summary>
    /// ローテーション世代数を取得または設定します。
    /// </summary>
    public int RotateFileCount { get; set; } = 2;
}

/// <summary>
/// テーマ設定です。
/// </summary>
public sealed class ThemeSettings
{
    /// <summary>
    /// Markdown 色設定プロファイル名を取得または設定します。
    /// </summary>
    public string MarkdownColorProfile { get; set; } = "Default";
}

