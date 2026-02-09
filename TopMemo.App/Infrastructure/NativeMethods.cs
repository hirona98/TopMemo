using System.Runtime.InteropServices;

namespace TopMemo.App.Infrastructure;

/// <summary>
/// Win32 API 定義を集約するクラスです。
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// 拡張ウィンドウスタイル取得インデックスです。
    /// </summary>
    internal const int GwlExStyle = -20;

    /// <summary>
    /// ウィンドウスタイル取得インデックスです。
    /// </summary>
    internal const int GwlStyle = -16;

    /// <summary>
    /// Alt+Tab 非表示のための拡張スタイルです。
    /// </summary>
    internal const int WsExToolWindow = 0x00000080;

    /// <summary>
    /// システムメニュー有効化スタイルです。
    /// </summary>
    internal const int WsSysMenu = 0x00080000;

    /// <summary>
    /// キー入力種別フラグです。
    /// </summary>
    internal const uint InputKeyboard = 1;

    /// <summary>
    /// キー離上フラグです。
    /// </summary>
    internal const uint KeyEventFKeyUp = 0x0002;

    /// <summary>
    /// 左 Windows キーの仮想キーコードです。
    /// </summary>
    internal const ushort VkLwin = 0x5B;

    /// <summary>
    /// 左マウスボタンの仮想キーコードです。
    /// </summary>
    internal const int VkLbutton = 0x01;

    /// <summary>
    /// Tab キーの仮想キーコードです。
    /// </summary>
    internal const ushort VkTab = 0x09;

    /// <summary>
    /// カーソル位置を取得します。
    /// </summary>
    /// <param name="point">カーソル位置。</param>
    /// <returns>取得に成功した場合は true。</returns>
    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    /// <summary>
    /// 指定キーの押下状態を取得します。
    /// </summary>
    /// <param name="vKey">仮想キーコード。</param>
    /// <returns>押下状態値。</returns>
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// ウィンドウ矩形を取得します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="rect">矩形。</param>
    /// <returns>取得に成功した場合は true。</returns>
    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint hWnd, out Rect rect);

    /// <summary>
    /// 拡張スタイルを取得します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="nIndex">インデックス。</param>
    /// <returns>スタイル値。</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    internal static extern int GetWindowLong32(nint hWnd, int nIndex);

    /// <summary>
    /// 拡張スタイルを取得します (64bit)。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="nIndex">インデックス。</param>
    /// <returns>スタイル値。</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    internal static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    /// <summary>
    /// 拡張スタイルを設定します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="nIndex">インデックス。</param>
    /// <param name="newLong">設定値。</param>
    /// <returns>更新後の値。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    internal static extern int SetWindowLong32(nint hWnd, int nIndex, int newLong);

    /// <summary>
    /// 拡張スタイルを設定します (64bit)。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="nIndex">インデックス。</param>
    /// <param name="newLong">設定値。</param>
    /// <returns>更新後の値。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    internal static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint newLong);

    /// <summary>
    /// キー入力を送出します。
    /// </summary>
    /// <param name="nInputs">入力数。</param>
    /// <param name="inputs">入力配列。</param>
    /// <param name="cbSize">構造体サイズ。</param>
    /// <returns>送出件数。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, Input[] inputs, int cbSize);

    /// <summary>
    /// 現在環境に合わせて拡張スタイルを取得します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>スタイル値。</returns>
    internal static nint GetWindowLongPtr(nint hWnd)
    {
        // x64 と x86 で呼び出し先を切り替えます。
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GwlExStyle)
            : GetWindowLong32(hWnd, GwlExStyle);
    }

    /// <summary>
    /// 現在環境に合わせて拡張スタイルを設定します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="value">設定値。</param>
    internal static void SetWindowLongPtr(nint hWnd, nint value)
    {
        // x64 と x86 で呼び出し先を切り替えます。
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GwlExStyle, value);
            return;
        }

        SetWindowLong32(hWnd, GwlExStyle, value.ToInt32());
    }

    /// <summary>
    /// 現在環境に合わせてウィンドウスタイルを取得します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <returns>スタイル値。</returns>
    internal static nint GetWindowStyle(nint hWnd)
    {
        // x64 と x86 で呼び出し先を切り替えます。
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, GwlStyle)
            : GetWindowLong32(hWnd, GwlStyle);
    }

    /// <summary>
    /// 現在環境に合わせてウィンドウスタイルを設定します。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="value">設定値。</param>
    internal static void SetWindowStyle(nint hWnd, nint value)
    {
        // x64 と x86 で呼び出し先を切り替えます。
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hWnd, GwlStyle, value);
            return;
        }

        SetWindowLong32(hWnd, GwlStyle, value.ToInt32());
    }
}

/// <summary>
/// 画面座標です。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    /// <summary>
    /// X 座標です。
    /// </summary>
    public int X;

    /// <summary>
    /// Y 座標です。
    /// </summary>
    public int Y;
}

/// <summary>
/// 画面矩形です。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    /// <summary>
    /// 左座標です。
    /// </summary>
    public int Left;

    /// <summary>
    /// 上座標です。
    /// </summary>
    public int Top;

    /// <summary>
    /// 右座標です。
    /// </summary>
    public int Right;

    /// <summary>
    /// 下座標です。
    /// </summary>
    public int Bottom;

    /// <summary>
    /// 点を内包しているかを判定します。
    /// </summary>
    /// <param name="point">判定点。</param>
    /// <returns>内包する場合は true。</returns>
    public readonly bool Contains(Point point)
    {
        // 画面座標の内包判定を行います。
        return point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
    }
}

/// <summary>
/// SendInput 入力定義です。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    /// <summary>
    /// 入力種別です。
    /// </summary>
    public uint Type;

    /// <summary>
    /// キーボード入力データです。
    /// </summary>
    public InputUnion Union;
}

/// <summary>
/// SendInput 共用体です。
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    /// <summary>
    /// キーボード入力です。
    /// </summary>
    [FieldOffset(0)]
    public KeyboardInput Keyboard;
}

/// <summary>
/// キーボード入力構造体です。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    /// <summary>
    /// 仮想キーコードです。
    /// </summary>
    public ushort Vk;

    /// <summary>
    /// スキャンコードです。
    /// </summary>
    public ushort Scan;

    /// <summary>
    /// 入力フラグです。
    /// </summary>
    public uint Flags;

    /// <summary>
    /// タイムスタンプです。
    /// </summary>
    public uint Time;

    /// <summary>
    /// 追加情報です。
    /// </summary>
    public nint ExtraInfo;
}
