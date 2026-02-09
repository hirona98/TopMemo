using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TopMemo.App.Models;
using TopMemo.App.Services;
using TopMemo.App.ViewModels;
using TopMemo.App.Views;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace TopMemo.App.Controllers;

/// <summary>
/// TopMemo の画面・保存・常駐制御を統括するコントローラです。
/// </summary>
public sealed class TopMemoController : IDisposable
{
    private readonly WpfApplication _application;
    private readonly MainWindow _window;
    private readonly FilePathService _filePathService;
    private readonly LoggingService _loggingService;
    private readonly JsonStorageService _storageService;
    private readonly MarkdownHighlightService _markdownHighlightService;
    private readonly LinkNavigationService _linkNavigationService;
    private readonly TaskViewInputService _taskViewInputService;
    private readonly TrayService _trayService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly ObservableCollection<MemoTabViewModel> _tabs = [];
    private AppSettings _settings;
    private HotZoneMonitorService? _hotZoneMonitorService;
    private MemoTabViewModel? _activeTab;
    private bool _isExiting;
    private int _activeDialogCount;

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="application">WPF アプリケーション。</param>
    /// <param name="window">メインウィンドウ。</param>
    public TopMemoController(WpfApplication application, MainWindow window)
    {
        // 必須依存を保持します。
        _application = application;
        _window = window;

        // 保存とログの基盤を初期化します。
        _filePathService = new FilePathService();
        var bootstrapLogger = new LoggingService(_filePathService, maxFileSizeKb: 100);
        var bootstrapStorage = new JsonStorageService(_filePathService, bootstrapLogger);
        _settings = bootstrapStorage.LoadSettingsOrCreate();
        _loggingService = new LoggingService(_filePathService, _settings.Logging.MaxFileSizeKb);
        _storageService = new JsonStorageService(_filePathService, _loggingService);

        // 補助サービスを初期化します。
        _markdownHighlightService = new MarkdownHighlightService();
        _linkNavigationService = new LinkNavigationService();
        _taskViewInputService = new TaskViewInputService();
        _trayService = new TrayService();
        _startupRegistrationService = new StartupRegistrationService();
    }

    /// <summary>
    /// 起動処理を実行します。
    /// </summary>
    public void Initialize()
    {
        // タブ定義と本文を読み込みます。
        LoadTabs();

        // 画面イベントを接続します。
        WireWindowEvents();
        WireTrayEvents();

        // エディタ表示設定を反映します。
        _window.BindTabs(_tabs);
        SelectInitialTab();
        UpdateCurrentMemoPath();
        ShowEditor();

        // 自動起動状態を初期化します。
        InitializeAutoStartState();

        // ホットゾーン監視を開始します。
        _hotZoneMonitorService = new HotZoneMonitorService(_settings, () => _window.IsEditorVisible, () => _window.TryGetWindowRect());
        _hotZoneMonitorService.ShowZoneEntered += ShowEditor;
        _hotZoneMonitorService.TaskViewZoneEntered += HandleTaskViewRequest;
        _hotZoneMonitorService.EditorExited += HideEditorAndSave;
        _hotZoneMonitorService.Start();
    }

    /// <summary>
    /// 外部インスタンスからの表示要求を処理します。
    /// </summary>
    public void ShowEditor()
    {
        // 既に表示中なら前面化だけ行います。
        if (_window.IsEditorVisible)
        {
            _window.Activate();
            return;
        }

        // 表示前に選択タブを復元します。
        if (_activeTab is not null)
        {
            _window.SelectTab(_activeTab);
        }

        // 設定座標でエディタを表示します。
        _window.ShowEditor(
            _settings.EditorWindow.X,
            _settings.EditorWindow.Y,
            _settings.EditorWindow.Width,
            _settings.EditorWindow.Height,
            _settings.Behavior.TopMost);
    }

    /// <summary>
    /// アプリ終了処理を実行します。
    /// </summary>
    public void RequestExit()
    {
        // 多重終了を防止します。
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        try
        {
            // 終了前保存を実行します。
            SaveDirtyTabs();
            PersistWindowPlacement();
            SaveTabsState();
            _storageService.SaveSettings(_settings);
        }
        catch (Exception exception)
        {
            // 終了前保存失敗を記録します。
            _loggingService.Error("終了前保存で例外が発生しました。", exception);
        }
        finally
        {
            // 監視とトレイを停止して終了します。
            _hotZoneMonitorService?.Dispose();
            _trayService.Dispose();
            _window.CloseForExit();
            _application.Shutdown();
        }
    }

    /// <summary>
    /// リソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // 終了時に必要なサービスを解放します。
        _hotZoneMonitorService?.Dispose();
        _trayService.Dispose();
    }

    /// <summary>
    /// 起動時のタブデータを読み込みます。
    /// </summary>
    private void LoadTabs()
    {
        // tabs.json と memo 本文を読み込みます。
        var tabsState = _storageService.LoadTabsOrCreate();
        _tabs.Clear();
        foreach (var definition in tabsState.Tabs)
        {
            // 保存識別子を取得し表示名をファイル名へ統一します。
            var fileIdentifier = definition.FileName;
            var displayName = Path.GetFileName(fileIdentifier);
            _tabs.Add(new MemoTabViewModel
            {
                Id = definition.Id,
                Title = displayName,
                FileName = fileIdentifier,
                Content = _storageService.LoadMemo(fileIdentifier),
                IsDirty = false
            });
        }

        // アクティブタブを復元します。
        _activeTab = _tabs.FirstOrDefault(tab => tab.Id == tabsState.ActiveTabId) ?? _tabs.First();
    }

    /// <summary>
    /// 初期選択タブを反映します。
    /// </summary>
    private void SelectInitialTab()
    {
        // 起動時タブを選択します。
        if (_activeTab is null)
        {
            return;
        }

        _window.SelectTab(_activeTab);
    }

    /// <summary>
    /// 画面イベントを接続します。
    /// </summary>
    private void WireWindowEvents()
    {
        // タブ追加・改名・削除イベントを接続します。
        _window.AddTabRequested += HandleAddTabRequested;
        _window.RenameTabRequested += HandleRenameTabRequested;
        _window.DeleteTabRequested += HandleDeleteTabRequested;
        _window.DeleteFileRequested += HandleDeleteFileRequested;
        _window.TabOrderChanged += HandleTabOrderChanged;

        // 編集と表示制御イベントを接続します。
        _window.SelectedTabChanged += HandleSelectedTabChanged;
        _window.EditorLoaded += HandleEditorLoaded;
        _window.EditorTextChanged += HandleEditorTextChanged;
        _window.HideRequested += HideEditorAndSave;
        _window.LinkOpenRequested += HandleLinkOpenRequested;
        _window.OpenFileDialogRequested += HandleOpenFileDialogRequested;
    }

    /// <summary>
    /// トレイイベントを接続します。
    /// </summary>
    private void WireTrayEvents()
    {
        // トレイ操作イベントを接続します。
        _trayService.ToggleWindowRequested += HandleToggleWindowRequested;
        _trayService.AutoStartToggled += HandleAutoStartToggled;
        _trayService.ExitRequested += RequestExit;
    }

    /// <summary>
    /// 起動時の自動起動状態を反映します。
    /// </summary>
    private void InitializeAutoStartState()
    {
        // 設定上有効なら登録を試行します。
        if (_settings.Behavior.AutoStartEnabled)
        {
            var enabled = _startupRegistrationService.Enable(
                _settings.Startup.AllowRegistryFallback,
                out var provider,
                out var errorMessage);

            if (enabled)
            {
                _settings.Startup.LastProvider = provider;
            }
            else
            {
                _settings.Behavior.AutoStartEnabled = false;
                _settings.Startup.LastProvider = "None";
                _loggingService.Error($"自動起動の有効化に失敗しました。{errorMessage}");
            }
        }

        // トレイのチェック状態を同期します。
        _trayService.SetAutoStartState(_settings.Behavior.AutoStartEnabled);
    }

    /// <summary>
    /// 表示/非表示トグル要求を処理します。
    /// </summary>
    private void HandleToggleWindowRequested()
    {
        // 表示中なら保存して非表示、非表示なら表示します。
        if (_window.IsEditorVisible)
        {
            HideEditorAndSave();
        }
        else
        {
            ShowEditor();
        }
    }

    /// <summary>
    /// タブ追加要求を処理します。
    /// </summary>
    private void HandleAddTabRequested()
    {
        // 初期表示ディレクトリを現在タブ基準で決定します。
        var initialDirectory = _filePathService.MemosDirectory;
        if (_activeTab is not null && Path.IsPathRooted(_activeTab.FileName))
        {
            initialDirectory = Path.GetDirectoryName(_activeTab.FileName) ?? _filePathService.MemosDirectory;
        }

        // 新規作成ダイアログを表示します。
        var dialog = new Win32SaveFileDialog
        {
            Title = "新しいファイルを作成",
            InitialDirectory = initialDirectory,
            Filter = "Markdown (*.md)|*.md|すべてのファイル (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".md",
            OverwritePrompt = false
        };
        var createResult = RunDialogGuarded(() => dialog.ShowDialog(_window));
        if (createResult != true)
        {
            return;
        }

        // 選択パスを正規化します。
        var selectedPath = Path.GetFullPath(dialog.FileName);
        var selectedFileName = Path.GetFileName(selectedPath);
        if (string.IsNullOrWhiteSpace(selectedFileName))
        {
            return;
        }

        // 同一ファイルが既に開いている場合は作成しません。
        var existingTab = _tabs.FirstOrDefault(tab =>
            string.Equals(ResolveTabFilePath(tab.FileName), selectedPath, StringComparison.OrdinalIgnoreCase));
        if (existingTab is not null)
        {
            ShowMessageDialog("同じファイルは既に開いています。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        // 既存ファイルは新規作成対象外です。
        if (File.Exists(selectedPath))
        {
            ShowMessageDialog("既に存在するファイルです。ファイルを開くを使用してください。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        // 切替前に未保存内容を保存します。
        SaveDirtyTabs();

        // 空ファイルを作成してタブを追加します。
        _storageService.SaveMemo(selectedPath, string.Empty);
        var createdTab = new MemoTabViewModel
        {
            Id = $"tab-{Guid.NewGuid():N}",
            Title = selectedFileName,
            FileName = selectedPath,
            Content = string.Empty,
            IsDirty = false
        };
        _tabs.Add(createdTab);

        // 状態を保存して新規タブへ移動します。
        SaveTabsState();
        _activeTab = createdTab;
        _window.SelectTab(createdTab);
        UpdateCurrentMemoPath();
    }

    /// <summary>
    /// タブ改名要求を処理します。
    /// </summary>
    /// <param name="tab">対象タブ。</param>
    private void HandleRenameTabRequested(MemoTabViewModel tab)
    {
        // 改名入力ダイアログを表示します。
        var dialog = new TextInputDialog(tab.Title)
        {
            Owner = _window
        };
        var renameResult = RunDialogGuarded(() => dialog.ShowDialog());
        if (renameResult != true)
        {
            return;
        }

        // 入力文字列を .md ファイル名へ正規化します。
        var newFileName = TabFileNameService.NormalizeFileName(dialog.ResultText);

        // 同名のタブ名は許可しません。
        if (TabFileNameService.IsDuplicatedFileName(newFileName, _tabs, tab.Id))
        {
            ShowMessageDialog("同じファイル名のタブは作成できません。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }

        try
        {
            // 現在ディレクトリを維持した新しい保存識別子を組み立てます。
            var newFileIdentifier = Path.IsPathRooted(tab.FileName)
                ? Path.Combine(Path.GetDirectoryName(tab.FileName) ?? string.Empty, newFileName)
                : newFileName;

            // 保存識別子が変わる場合はファイルも改名します。
            if (!string.Equals(tab.FileName, newFileIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                _storageService.RenameMemo(tab.FileName, newFileIdentifier);
                tab.FileName = newFileIdentifier;
            }

            // タブ表示名をファイル名へ同期します。
            tab.Title = newFileName;
            SaveTabsState();
            UpdateCurrentMemoPath();
        }
        catch (Exception exception)
        {
            // 改名失敗を通知して戻します。
            _loggingService.Error("タブ名変更に失敗しました。", exception);
            ShowMessageDialog("タブ名変更に失敗しました。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
    }

    /// <summary>
    /// タブ削除要求を処理します。
    /// </summary>
    /// <param name="tab">対象タブ。</param>
    private void HandleDeleteTabRequested(MemoTabViewModel tab)
    {
        // 最低 1 タブ維持ルールを適用します。
        if (!CanDeleteTab())
        {
            return;
        }

        // 変更を退避してタブのみ削除します。
        SaveDirtyTabs();
        RemoveTab(tab);
    }

    /// <summary>
    /// ファイル削除要求を処理します。
    /// </summary>
    /// <param name="tab">対象タブ。</param>
    private void HandleDeleteFileRequested(MemoTabViewModel tab)
    {
        // 最低 1 タブ維持ルールを適用します。
        if (!CanDeleteTab())
        {
            return;
        }

        // ファイル削除確認を表示します。
        var confirmation = ShowMessageDialog(
            $"ファイル \"{tab.FileName}\" を削除しますか？\nこの操作は元に戻せません。",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Warning);
        if (confirmation != WpfMessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            // 先に実ファイルを削除してからタブを閉じるします。
            _storageService.DeleteMemo(tab.FileName);
            RemoveTab(tab);
        }
        catch (Exception exception)
        {
            // 削除失敗を通知します。
            _loggingService.Error($"ファイル削除に失敗しました。file={tab.FileName}", exception);
            ShowMessageDialog("ファイル削除に失敗しました。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
    }

    /// <summary>
    /// タブ切替イベントを処理します。
    /// </summary>
    /// <param name="tab">新しいタブ。</param>
    private void HandleSelectedTabChanged(MemoTabViewModel? tab)
    {
        // 選択が無い場合は処理しません。
        if (tab is null)
        {
            return;
        }

        // 同一タブなら処理不要です。
        if (_activeTab is not null && _activeTab.Id == tab.Id)
        {
            return;
        }

        // タブ切替時保存を実行します。
        SaveDirtyTabs();

        // 新しいタブへ切り替えて状態を保存します。
        _activeTab = tab;
        UpdateCurrentMemoPath();
        SaveTabsState();
    }

    /// <summary>
    /// タブ順序変更イベントを処理します。
    /// </summary>
    private void HandleTabOrderChanged()
    {
        // 並び順変更を即時保存します。
        SaveTabsState();
    }

    /// <summary>
    /// エディタ本文変更イベントを処理します。
    /// </summary>
    /// <param name="tab">編集対象タブ。</param>
    /// <param name="text">本文。</param>
    private void HandleEditorTextChanged(MemoTabViewModel tab, string text)
    {
        // 編集されたタブを現在タブとして扱います。
        _activeTab = tab;

        // 本文更新と dirty 化を行います。
        tab.Content = text;
        tab.IsDirty = true;
    }

    /// <summary>
    /// エディタ初期化イベントを処理します。
    /// </summary>
    /// <param name="textEditor">初期化済みエディタ。</param>
    private void HandleEditorLoaded(TextEditor textEditor)
    {
        // Markdown 色付けを適用します。
        _markdownHighlightService.Apply(textEditor);
    }

    /// <summary>
    /// リンククリック要求を処理します。
    /// </summary>
    /// <param name="document">クリック元ドキュメント。</param>
    /// <param name="offset">クリック位置オフセット。</param>
    /// <returns>リンクを開けた場合は true。</returns>
    private bool HandleLinkOpenRequested(TextDocument document, int offset)
    {
        // リンク遷移を試行します。
        return _linkNavigationService.TryOpenLink(document, offset);
    }

    /// <summary>
    /// 開くダイアログ要求を処理します。
    /// </summary>
    private void HandleOpenFileDialogRequested()
    {
        // 初期表示ディレクトリを現在タブ基準で決定します。
        var initialDirectory = _filePathService.MemosDirectory;
        if (_activeTab is not null && Path.IsPathRooted(_activeTab.FileName))
        {
            initialDirectory = Path.GetDirectoryName(_activeTab.FileName) ?? _filePathService.MemosDirectory;
        }

        // 開くダイアログを表示します。
        var dialog = new Win32OpenFileDialog
        {
            Title = "メモを開く",
            InitialDirectory = initialDirectory,
            Filter = "Markdown (*.md)|*.md|すべてのファイル (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        var openResult = RunDialogGuarded(() => dialog.ShowDialog(_window));
        if (openResult != true)
        {
            return;
        }

        // 選択パスを正規化します。
        var selectedPath = Path.GetFullPath(dialog.FileName);
        var selectedFileName = Path.GetFileName(selectedPath);
        if (string.IsNullOrWhiteSpace(selectedFileName))
        {
            return;
        }

        // 切替前に未保存内容を保存します。
        SaveDirtyTabs();

        // 既存タブがあれば再利用します。
        var existingTab = _tabs.FirstOrDefault(tab =>
            string.Equals(ResolveTabFilePath(tab.FileName), selectedPath, StringComparison.OrdinalIgnoreCase));
        if (existingTab is not null)
        {
            // 同一ファイルの重複オープンを禁止します。
            ShowMessageDialog("同じファイルは既に開いています。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            return;
        }

        // 新規タブとしてファイルを開きます。
        var openedTab = new MemoTabViewModel
        {
            Id = $"tab-{Guid.NewGuid():N}",
            Title = selectedFileName,
            FileName = selectedPath,
            Content = _storageService.LoadMemo(selectedPath),
            IsDirty = false
        };
        _tabs.Add(openedTab);
        _activeTab = openedTab;
        _window.SelectTab(openedTab);
        UpdateCurrentMemoPath();
        SaveTabsState();
    }

    /// <summary>
    /// 自動起動トグル要求を処理します。
    /// </summary>
    /// <param name="enabled">有効状態。</param>
    private void HandleAutoStartToggled(bool enabled)
    {
        // 要求状態に応じて登録/解除を実行します。
        if (enabled)
        {
            var success = _startupRegistrationService.Enable(
                _settings.Startup.AllowRegistryFallback,
                out var provider,
                out var errorMessage);
            if (!success)
            {
                _loggingService.Error($"自動起動の有効化に失敗しました。{errorMessage}");
                ShowMessageDialog("自動起動の有効化に失敗しました。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                _settings.Behavior.AutoStartEnabled = false;
                _settings.Startup.LastProvider = "None";
                _trayService.SetAutoStartState(false);
                _storageService.SaveSettings(_settings);
                return;
            }

            _settings.Behavior.AutoStartEnabled = true;
            _settings.Startup.LastProvider = provider;
        }
        else
        {
            var success = _startupRegistrationService.Disable(out var errorMessage);
            if (!success)
            {
                _loggingService.Error($"自動起動の無効化に失敗しました。{errorMessage}");
                ShowMessageDialog("自動起動の無効化に失敗しました。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                _settings.Behavior.AutoStartEnabled = true;
                _trayService.SetAutoStartState(true);
                _storageService.SaveSettings(_settings);
                return;
            }

            _settings.Behavior.AutoStartEnabled = false;
            _settings.Startup.LastProvider = "None";
        }

        // 設定を保存してトレイ状態を同期します。
        _trayService.SetAutoStartState(_settings.Behavior.AutoStartEnabled);
        _storageService.SaveSettings(_settings);
    }

    /// <summary>
    /// Win+Tab 要求を処理します。
    /// </summary>
    private void HandleTaskViewRequest()
    {
        // Win+Tab を送出します。
        var sent = _taskViewInputService.SendWinTab();
        if (!sent)
        {
            _loggingService.Error("Win+Tab の送出に失敗しました。");
        }
    }

    /// <summary>
    /// 非表示時保存を実行してウィンドウを隠します。
    /// </summary>
    private void HideEditorAndSave()
    {
        // いずれかのダイアログ表示中は非表示にしません。
        if (_activeDialogCount > 0)
        {
            return;
        }

        // 表示中のみ保存と非表示を実行します。
        if (!_window.IsEditorVisible)
        {
            return;
        }

        // 非表示トリガー保存を実行します。
        SaveDirtyTabs();
        PersistWindowPlacement();
        SaveTabsState();
        _storageService.SaveSettings(_settings);
        _window.HideEditor();
    }

    /// <summary>
    /// dirty タブを保存します。
    /// </summary>
    private void SaveDirtyTabs()
    {
        // dirty の全タブを順に保存します。
        foreach (var tab in _tabs.Where(tab => tab.IsDirty))
        {
            try
            {
                _storageService.SaveMemo(tab.FileName, tab.Content);
                tab.IsDirty = false;
            }
            catch (Exception exception)
            {
                _loggingService.Error($"タブ保存に失敗しました。file={tab.FileName}", exception);
            }
        }
    }

    /// <summary>
    /// タブ定義を tabs.json へ保存します。
    /// </summary>
    private void SaveTabsState()
    {
        // 現在状態から保存モデルを組み立てます。
        var tabsState = new TabsState
        {
            ActiveTabId = _activeTab?.Id ?? _tabs.First().Id,
            Tabs = _tabs.Select(tab => new TabDefinition
            {
                Id = tab.Id,
                Title = tab.Title,
                FileName = tab.FileName
            }).ToList()
        };

        // tabs.json を保存します。
        _storageService.SaveTabs(tabsState);
    }

    /// <summary>
    /// ウィンドウ配置を設定へ反映します。
    /// </summary>
    private void PersistWindowPlacement()
    {
        // 現在位置とサイズを設定へ書き戻します。
        var (x, y, width, height) = _window.GetWindowPlacement();
        _settings.EditorWindow.X = x;
        _settings.EditorWindow.Y = y;
        _settings.EditorWindow.Width = width;
        _settings.EditorWindow.Height = height;
    }

    /// <summary>
    /// 現在タブのファイルパス表示を更新します。
    /// </summary>
    private void UpdateCurrentMemoPath()
    {
        // アクティブタブが無い場合は空を表示します。
        if (_activeTab is null)
        {
            _window.SetCurrentMemoPath(string.Empty);
            return;
        }

        // 現在タブの保存パスを表示します。
        _window.SetCurrentMemoPath(ResolveTabFilePath(_activeTab.FileName));
    }

    /// <summary>
    /// タブ保存識別子を比較用の絶対パスへ正規化します。
    /// </summary>
    /// <param name="fileIdentifier">保存識別子。</param>
    /// <returns>絶対パス。</returns>
    private string ResolveTabFilePath(string fileIdentifier)
    {
        // 絶対パスはそのまま正規化します。
        if (Path.IsPathRooted(fileIdentifier))
        {
            return Path.GetFullPath(fileIdentifier);
        }

        // 相対名は memos 配下へ解決します。
        return Path.GetFullPath(_filePathService.GetMemoPath(fileIdentifier));
    }

    /// <summary>
    /// タブ削除可能かを判定します。
    /// </summary>
    /// <returns>削除可能なら true。</returns>
    private bool CanDeleteTab()
    {
        // 最低 1 タブ維持ルールを適用します。
        if (_tabs.Count > 1)
        {
            return true;
        }

        ShowMessageDialog("最後の1タブは削除できません。", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        return false;
    }

    /// <summary>
    /// タブのみを削除して選択状態を整えます。
    /// </summary>
    /// <param name="tab">削除対象タブ。</param>
    private void RemoveTab(MemoTabViewModel tab)
    {
        // 対象タブが一覧に無い場合は処理しません。
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        // アクティブタブ削除かを保持します。
        var deletingActive = _activeTab?.Id == tab.Id;

        // タブ一覧から削除します。
        _tabs.RemoveAt(index);

        // アクティブ削除時は近傍タブを選択します。
        if (deletingActive)
        {
            var nextIndex = Math.Min(index, _tabs.Count - 1);
            _activeTab = _tabs[nextIndex];
            _window.SelectTab(_activeTab);
        }

        // 表示更新と永続化を実行します。
        UpdateCurrentMemoPath();
        SaveTabsState();
    }

    /// <summary>
    /// ダイアログ表示を監視抑止付きで実行します。
    /// </summary>
    /// <typeparam name="TResult">戻り値型。</typeparam>
    /// <param name="dialogAction">ダイアログ処理。</param>
    /// <returns>ダイアログ戻り値。</returns>
    private TResult RunDialogGuarded<TResult>(Func<TResult> dialogAction)
    {
        // 初回突入時にホットゾーン監視を停止します。
        if (_activeDialogCount == 0)
        {
            _hotZoneMonitorService?.Stop();
        }

        _activeDialogCount++;
        try
        {
            return dialogAction();
        }
        finally
        {
            // ダイアログ終了時に監視を再開します。
            _activeDialogCount--;
            if (_activeDialogCount == 0 && !_isExiting)
            {
                _hotZoneMonitorService?.Start();
            }
        }
    }

    /// <summary>
    /// メッセージダイアログを監視抑止付きで表示します。
    /// </summary>
    /// <param name="message">表示文言。</param>
    /// <param name="button">ボタン種別。</param>
    /// <param name="image">アイコン種別。</param>
    /// <returns>ユーザー選択結果。</returns>
    private WpfMessageBoxResult ShowMessageDialog(string message, WpfMessageBoxButton button, WpfMessageBoxImage image)
    {
        // MessageBox 表示を共通ガードで実行します。
        return RunDialogGuarded(() => WpfMessageBox.Show(_window, message, "TopMemo", button, image));
    }
}
