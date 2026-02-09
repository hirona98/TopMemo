using System.Runtime.InteropServices;
using TopMemo.App.Infrastructure;

namespace TopMemo.App.Services;

/// <summary>
/// Win+Tab 入力送出サービスです。
/// </summary>
public sealed class TaskViewInputService
{
    /// <summary>
    /// 直近の SendInput エラーコードを取得します。
    /// </summary>
    public int LastErrorCode { get; private set; }

    /// <summary>
    /// 直近の SendInput 送出件数を取得します。
    /// </summary>
    public uint LastSentCount { get; private set; }

    /// <summary>
    /// Win+Tab を送出します。
    /// </summary>
    /// <returns>送出に成功した場合は true。</returns>
    public bool SendWinTab()
    {
        // Win down, Tab down/up, Win up の順で入力を構築します。
        var inputs = new[]
        {
            CreateKeyInput(NativeMethods.VkLwin, keyUp: false, extendedKey: true),
            CreateKeyInput(NativeMethods.VkTab, keyUp: false, extendedKey: false),
            CreateKeyInput(NativeMethods.VkTab, keyUp: true, extendedKey: false),
            CreateKeyInput(NativeMethods.VkLwin, keyUp: true, extendedKey: true)
        };

        // SendInput で一括送出します。
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        LastSentCount = sent;
        LastErrorCode = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        return sent == inputs.Length;
    }

    /// <summary>
    /// キー入力構造体を作成します。
    /// </summary>
    /// <param name="virtualKey">仮想キーコード。</param>
    /// <param name="keyUp">離上の場合 true。</param>
    /// <param name="extendedKey">拡張キーの場合 true。</param>
    /// <returns>入力構造体。</returns>
    private static Input CreateKeyInput(ushort virtualKey, bool keyUp, bool extendedKey)
    {
        // キー押下/離上の Input を組み立てます。
        return new Input
        {
            Type = NativeMethods.InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Vk = virtualKey,
                    Scan = 0,
                    Flags = (keyUp ? NativeMethods.KeyEventFKeyUp : 0) |
                        (extendedKey ? NativeMethods.KeyEventFExtendedKey : 0),
                    Time = 0,
                    ExtraInfo = nint.Zero
                }
            }
        };
    }

}
