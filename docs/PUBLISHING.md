# 上傳 GitHub

這是一份獨立新專案，不是原作者專案的完整 fork；沒有攜帶他的檔案或 Git 歷史。

## 包含

- 本專案 C# runtime／patch generator 原始碼。
- 路徑可設定、版本與雜湊查核的本機建置／安裝／還原工具。
- README、MIT 授權範圍、相容性、測試、來源致謝及問題回報範本。

## 不包含

- 任一遊戲原版或修改後 DLL、PDB、存檔、Steam 身分、個人日誌／畫面。
- 原作者的 `main.py`、圖片、下載版 DLL 或 PDB。
- `.local/`、本機使用者路徑、私人工作資料夾與先前專案的 Git 歷史。

## 步驟

1. 在這個資料夾執行工具測試及打包：

   ```powershell
   python -m unittest discover -s tests -v
   python tools/package_release.py
   ```

2. 檢查 `dist/` 中的純原始碼 ZIP；此 ZIP 不是預先編譯的一鍵遊戲補丁。
3. 在自己的 GitHub 建立新 repository。只上傳此獨立資料夾列出的來源；不要上傳外層工作資料夾。
4. 若用 Git CLI，可在這個獨立資料夾手動執行：

   ```powershell
   git init
   git add .gitignore README.md LICENSE THIRD_PARTY_NOTICES.md CONTRIBUTING.md SECURITY.md requirements.txt VERSION src tools tests docs .github
   git diff --cached --stat
   git status --short
   # 確認沒有任何遊戲檔案或私人內容後，再 commit、設定自己的 remote、push。
   ```

`.gitignore` 不會自動移除已被 Git 追蹤的檔案。發佈前仍需看 staged files；打包器則採明確來源白名單，避免將整個資料夾盲目壓縮。

## 可以附上原作者的開箱程式嗎？

目前只放來源連結與致謝，不附檔案。查核時未見獨立 LICENSE，沒有確認到再散布授權。若要附其程式、截圖或 DLL，需先取得明確授權，再保留其授權與署名；不要直接套用本專案 MIT。

本專案的可選開箱 patch generator 是另外實作，產生的遊戲檔案只留在使用者本機。不需要把原作者的 DLL 一起發佈，使用者也能選用開箱功能。

你可以在 README 稱它為「歷史參考連結」，不宜把未審核的下載版說成跨版本推薦安裝方案。
