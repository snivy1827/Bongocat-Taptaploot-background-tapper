# 相容性與可攜性

## 能否每台電腦都用？

**目前不能這樣保證。** 已有一台 Windows 11 x64（build 26200）、Python 3.12.10 的遊戲內實測。其他電腦、Windows 版本、硬體、遊戲 Build、前景應用程式組合仍待驗證。

不同安裝磁碟或 Windows 使用者名稱不是固定限制：CLI 接受 `--game-dir`，路徑交由 Python/subprocess 參數傳遞，不組合 shell 指令，並在本機用該遊戲自己的 `mscorlib.dll`、`System.dll` 編譯。這是避免固定路徑與 Unity 裁剪 API 差異的設計，不等於跨機測試已完成。

## 目前已知原版雜湊

`tools/patch.py` 以原版 `Assembly-CSharp.dll` SHA-256 判斷已知版本，不憑遊戲名稱或「最新版」字樣判定。

| 遊戲 | 原版 SHA-256 | 歷史驗證 |
|---|---|---|
| Bongo Cat | `25dbce06fdf0c16d302fed8287e3f3f1edad6f4c41747f37826a01f94049f927` | 內部連點、F7、開箱結果回呼、前景隔離 |
| TapTapLoot | `716d02bd8368b44c227fce80741b4f4d806cf9590125ea9855b59068d111c675` | 內部連點、F7、原生計分路徑、前景隔離 |

這些是歷史已測版本，不宣稱目前 Steam 最新版必定相同。原版雜湊只證明檔案一致，不代表所有環境皆已驗證。

## 明確不支援或未驗證

- 32 位元 Python／遊戲、Windows ARM64：拒絕；原生 MSG 處理只針對 x64。
- macOS、Linux、Proton／Steam Deck：沒有支援。
- Unity IL2CPP／`GameAssembly.dll`：沒有 Mono `Assembly-CSharp.dll` 時無法使用。
- 被其他 MOD 修改過或已套用本補丁的原檔：拒絕當作新基準。
- F7 已被其他程式註冊：可能無法取得熱鍵；本模組不會強搶或改掉其他程式設定。
- 多使用者同時登入、不同提升權限或防護工具組合：尚未驗證。
- 遊戲被最小化、節流、暫停或低幀率：30 次／秒是目標，不能強迫遊戲每秒更新 30 次。

## 新遊戲 Build 的進階測試

預設拒絕未知雜湊。維護者完成程式結構檢查後，可準備未驗證候選：

```powershell
python tools/patch.py prepare --game ttl --game-dir "D:\SteamLibrary\steamapps\common\TapTapLoot" --allow-unknown-build
```

此命令只輸出 `.local/` 候選。若確定要在自己的電腦承擔測試，再明確執行：

```powershell
python tools/patch.py install --game ttl --allow-unverified
```

這不是「相容性修復」按鈕：新版本的事件、私有欄位、回呼或 Unity 裁剪 API 若改動，必須修改轉接程式，不能只把新雜湊加進白名單。先依 TESTING.md 實測，再提交新相容性紀錄。沒有實測就標示未驗證。

## 背景不干擾的實際範圍

此程式不使用 `SendInput`、`keybd_event`、`mouse_event`、`SetCursorPos`、`SetForegroundWindow` 或桌面座標點擊。F7 是全域註冊的開關，不是連續向前景發送的按鍵。

已測情境中游標與焦點保持不變、測試視窗收到 0 次按鍵；仍不能保證每個前景程式或遊戲自己的通知視窗都沒有其他互動。F7 被保留本身就是對快捷鍵行為的一項改變。
