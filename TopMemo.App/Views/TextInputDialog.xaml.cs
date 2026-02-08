using System.Windows;

namespace TopMemo.App.Views;

/// <summary>
/// テキスト入力ダイアログです。
/// </summary>
public partial class TextInputDialog : Window
{
    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="currentValue">初期値。</param>
    public TextInputDialog(string currentValue)
    {
        InitializeComponent();

        // 初期値を反映して全選択します。
        NameTextBox.Text = currentValue;
        NameTextBox.SelectAll();
        NameTextBox.Focus();
    }

    /// <summary>
    /// 入力結果文字列を取得します。
    /// </summary>
    public string ResultText { get; private set; } = string.Empty;

    /// <summary>
    /// OK ボタンクリック処理です。
    /// </summary>
    /// <param name="sender">送信元。</param>
    /// <param name="eventArgs">イベント引数。</param>
    private void OkButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        // 入力結果を保持してダイアログを閉じます。
        ResultText = NameTextBox.Text.Trim();
        DialogResult = true;
    }
}

