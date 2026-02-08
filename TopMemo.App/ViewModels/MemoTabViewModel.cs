using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopMemo.App.ViewModels;

/// <summary>
/// 画面表示と編集状態を持つタブ ViewModel です。
/// </summary>
public sealed class MemoTabViewModel : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;

    /// <summary>
    /// タブ ID を取得または設定します。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// タブ表示名を取得または設定します。
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            // 値が同じなら変更通知を抑止します。
            if (_title == value)
            {
                return;
            }

            // 新しい値を反映します。
            _title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 保存ファイル名を取得または設定します。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// タブ本文を取得または設定します。
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            // 値が同じなら変更通知を抑止します。
            if (_content == value)
            {
                return;
            }

            // 新しい値を反映します。
            _content = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 未保存変更フラグを取得または設定します。
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// プロパティ変更イベントです。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更通知を発行します。
    /// </summary>
    /// <param name="propertyName">プロパティ名。</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // 監視者に変更通知を送ります。
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

