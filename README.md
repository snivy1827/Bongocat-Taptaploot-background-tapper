# Game Background Tapper

**遊戲在背景累積點擊，游標留給你。**

為 **Bongo Cat** 與 **TapTapLoot** 製作的 Windows x64／Unity Mono 遊戲內連點模組。呼叫遊戲原有的點擊流程，不向前景程式送出連按事件，不移動實體游標，也不主動搶走前景焦點。

> 這是設計目標與有限實測結果，**不是「所有電腦、遊戲版本或前景應用程式都絕不受影響」的保證**。目前只有一台 Windows 11 x64 電腦完成遊戲內實測；請先看[相容性](docs/COMPATIBILITY.md)。

## 功能

- **F7** 同步開始／停止已開啟的兩款遊戲連點。
- 目標每款每秒 **30 次**；實際速度受遊戲更新率及電腦負載影響。
- 兩款都關閉後，下次啟動預設停止；已有一款開啟時，另一款加入會跟隨共用狀態。
- 可選的 **Bongo Cat 自動開寶箱**：保留遊戲原本點數、冷卻及伺服器條件，與 F7 連點獨立。
- TapTapLoot 只加入連點，不加入開箱功能。
- 遊戲內執行，安裝後不用常駐 Python 連點器。
- 安裝前後雜湊查核、保留本機原版備份、拒絕覆寫未知檔案、提供還原。

**F7 的例外：**遊戲執行期間，F7 會保留給共用連點開關，前景程式不會再收到 F7 作為自己的快捷鍵。這是已知操作取捨，而非完全不改變快捷鍵行為。

## 支援範圍

| 環境 | 狀態 |
|---|---|
| 本機已測的 Windows 11 x64，遊戲版本符合已知原檔雜湊 | 有單機功能實測；仍需在你的電腦驗證 |
| 其他 Windows 10／11 x64 電腦、其他遊戲 Build | 尚未完成跨機測試；未知 Build 預設拒絕建置／安裝 |
| Windows ARM64、32 位元 | 不支援／未驗證，工具拒絕 |
| macOS、Linux、Steam Deck／Proton、IL2CPP 版 | 不支援 |

同一個 Steam 帳號不會同步這些模組。每台電腦都需用自己的遊戲檔案本機建置。

## 快速開始

需要 Windows x64、**64 位元 Python 3.10+**、.NET Framework C# 編譯器，以及你自己安裝的兩款遊戲。Python 3.12.10 已測；其他 Python 小版本尚未驗證。不同 Steam 磁碟可用 `--game-dir` 指定，無需放在 C 槽。

從本專案資料夾執行：

```powershell
python -m pip install -r requirements.txt
python tools/patch.py fetch-deps
```

先正常關閉要安裝的遊戲。在 Steam 遊戲「管理 → 瀏覽本機檔案」取得資料夾，替換下列範例路徑：

```powershell
# Bongo Cat：連點＋可選自動開箱
python tools/patch.py prepare --game bongo --game-dir "D:\SteamLibrary\steamapps\common\BongoCat" --auto-chests
python tools/patch.py install --game bongo

# TapTapLoot：連點
python tools/patch.py prepare --game ttl --game-dir "D:\SteamLibrary\steamapps\common\TapTapLoot"
python tools/patch.py install --game ttl
```

若不需要 Bongo 自動開箱，省略 `--auto-chests`。`prepare` 只寫入專案的 `.local/`，不修改遊戲；`install` 才會寫入遊戲目錄，且必須有該目錄的寫入權限。**本工具不會強制關閉遊戲、調低安全設定或安裝系統服務。**

開啟遊戲後按 F7。首次使用請依[驗證步驟](docs/TESTING.md)，確認真的計分、可停止、游標與焦點維持不變。

已有其他補丁或先前手動安裝版本時，先使用原安裝工具還原；無法還原則先用 Steam 驗證原版檔案，再建置。請勿把舊補丁當作新原檔。

## 狀態、停用與還原

```powershell
python tools/patch.py status --game bongo
python tools/patch.py status --game ttl

# 停用／重新啟用 Bongo 自動開箱；連點仍由 F7 控制
python tools/patch.py chests --game bongo --enabled off
python tools/patch.py chests --game bongo --enabled on

# 先正常關閉對應遊戲，再完整還原
python tools/patch.py restore --game bongo
python tools/patch.py restore --game ttl
```

**不要刪除 `.local/`：**其中有你的原版備份與還原資訊，但絕不能上傳 GitHub。若遊戲已更新，工具會拒絕拿舊備份覆寫新版。

## 更新不是自動保證相容

Steam 更新可能覆蓋補丁。`status` 顯示 `changed` 時，先停止連點並依[更新流程](docs/UPDATES.md)處理。此版本**沒有自動更新排程，也不會自動下載他人的遊戲 DLL**。

未知版本的進階建置及人工驗證方式見[相容性文件](docs/COMPATIBILITY.md)，不要只為跳過錯誤就強制安裝。

## 實測摘要

2026-11-02 的單機實測：兩款在穩定區間皆為 **300 次／10.015 秒，約 29.955 次／秒**；F7 停止後至少 7 筆紀錄沒有再增加自動觸發。65 秒共 635 筆樣本中游標與前景視窗不變，獨立前景視窗收到 0 次按鍵。

較早的自動開箱測試確認普通與表情寶箱都有 `SUCCESS_CALLBACK`、成功視覺狀態與冷卻重設。加速版沿用同一遊戲主 DLL；沒有在加速測試中重新等一輪寶箱冷卻。這些都不能替代你的跨機驗證，詳見[測試紀錄](docs/TESTING.md)。

## 原作者的開箱方案

早期參考過 [RoninOnis/BongoCat-auto](https://github.com/RoninOnis/BongoCat-auto) 的說明。此專案只附連結與致謝，**不附上作者的程式、圖片或 DLL，也不推薦下載未審核的二進位**。本專案可選的開箱 hook 是另外實作的本機建置方案。[授權與來源說明](THIRD_PARTY_NOTICES.md)

## 發佈與貢獻

只分享這個獨立資料夾的原始碼，不要上傳你的整個工作資料夾。建立經檢查的純原始碼 ZIP：

```powershell
python -m unittest discover -s tests -v
python tools/package_release.py
```

安裝包不含遊戲檔案。詳見[發佈方式](docs/PUBLISHING.md)、[問題回報](CONTRIBUTING.md)與 [MIT 授權](LICENSE)。原始碼 MIT 授權不代表遊戲或 Steam 官方允許此修改，也不保證帳號政策結果。

---

**English:** Source-only internal tapping adapters for two Windows x64 Unity Mono games. Shared F7, approximately 30 taps/s, optional locally generated Bongo Cat chest hook. No global click/key spam or cursor repositioning. Single-machine tested; universal compatibility and foreground isolation are not guaranteed. F7 is reserved while running. Supply your own game files; never distribute game-derived DLLs, saves or personal logs. See the linked documentation for build, restore, compatibility and attribution details.
