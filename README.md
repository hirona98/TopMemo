# TopMemo

TopMemo は Windows 11 向けの常駐型ホバーメモ帳です。  
ホットゾーンにマウスが侵入すると即表示し、領域外に出ると即非表示になります。

## 機能

- 常駐型メモ（ホットゾーンで表示、領域外で非表示）
- 単一インスタンス起動（2重起動時は既存インスタンスを前面化）
- システムトレイメニュー（`表示/非表示` `自動起動` `終了`）
- タブ管理（初期 0 タブ、作成/開く、改名、閉じる、ファイル削除、全タブ削除、並び替え）
- Markdown ハイライトとリンククリック
- 自動保存（非表示時 / タブ切替時 / 終了時）
- 自動起動はスタートアップフォルダ（`.lnk`）のみ

## 使い方

1. アプリを起動します（常駐します）。
2. 画面左上のホットゾーンへマウスを移動すると、エディタが表示されます。
3. エディタ領域の外へマウスを出すと、エディタが非表示になります。
4. タブ行の右クリックで `ファイルの作成` `ファイルを開く` `すべてのタブを閉じる` を使えます。
5. タブの右クリックで `名前変更` `タブを閉じる` `ファイルを削除` を使えます。
6. トレイアイコン右クリックで `表示/非表示` `自動起動` `終了` を操作できます。

## 保存ポリシー

- 設定・データはすべてアプリフォルダ内で完結
- ファイル構成: `settings.json` `tabs.json` `memos/<tabFileName>.md` `logs/app.log`

## ビルド

WSL では PowerShell 経由でビルドします。

```powershell
powershell.exe -NoProfile -Command "cd 'D:\AliceEncoder\TopMemo'; dotnet.exe build .\TopMemo.sln -c Debug"
```

## 自動リリース

- `.github/workflows/release.yml` により `v*` タグ push で自動リリースします
- 生成物: `TopMemo-<tag>-win-x64.zip`

手順:

```bash
git tag v0.1.0
git push origin v0.1.0
```

※ Release 作成に失敗する場合は、GitHub の `Settings > Actions > General > Workflow permissions` を `Read and write permissions` にしてください。

## 詳細設計

`docs/hover-notepad-design.md` を参照してください。
