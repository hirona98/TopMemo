using System.Runtime.InteropServices;
using TopMemo.App.Infrastructure;

namespace TopMemo.App.Services;

/// <summary>
/// Win+Tab 入力送出サービスです。
/// </summary>
public sealed class TaskViewInputService
{
    /// <summary>
    /// Win+Tab を送出します。
    /// </summary>
    /// <returns>送出に成功した場合は true。</returns>
    public bool SendWinTab()
    {
        // Win down, Tab down/up, Win up の順で入力を構築します。
        var inputs = new[]
        {
            CreateKeyInput(NativeMethods.VkLwin, keyUp: false),
            CreateKeyInput(NativeMethods.VkTab, keyUp: false),
            CreateKeyInput(NativeMethods.VkTab, keyUp: true),
            CreateKeyInput(NativeMethods.VkLwin, keyUp: true)
        };

        // SendInput で一括送出します。
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length;
    }

    /// <summary>
    /// キー入力構造体を作成します。
    /// </summary>
    /// <param name="virtualKey">仮想キーコード。</param>
    /// <param name="keyUp">離上の場合 true。</param>
    /// <returns>入力構造体。</returns>
    private static Input CreateKeyInput(ushort virtualKey, bool keyUp)
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
                    Flags = keyUp ? NativeMethods.KeyEventFKeyUp : 0,
                    Time = 0,
                    ExtraInfo = nint.Zero
                }
            }
        };
    }
}

