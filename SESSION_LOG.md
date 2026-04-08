# SESSION_LOG.md — Tiến độ theo session

# Claude Code tự cập nhật file này. Không sửa thủ công phần log.

---

## Session 2026-04-08

### Đã làm

**File đã sửa:**
- `CLAUDE.md` — Thêm bảng danh sách module (5 module), bổ sung pattern PaletteSet (thứ tự khởi tạo bắt buộc), thêm quy tắc `SetFocusToDwgView()`, cập nhật GUID thật, đổi log message sang English (theo user chỉnh)
- `Commands/PaletteManager.cs` — Fix 4 lỗi audit: cú pháp KeepFocus, thứ tự khởi tạo, size 400x600, GUID thật

**File placeholder đã tạo (25 file):**
- `Commands/{Module}/{Module}Command.cs` — 5 file (DetailDesign, FittingManagement, PanelData, TableOfContent, Weight)
- `Models/{Module}/{Module}Model.cs` — 5 file
- `Services/{Module}/{Module}Service.cs` — 5 file
- `Views/{Module}/{Module}View.cs` — 5 file
- `Utilities/{Module}/{Module}Utility.cs` — 5 file (đổi từ Helper → Utility cho khớp folder name)

**Folder đã tạo:**
- 5 module folders trong mỗi layer (`Commands/`, `Models/`, `Services/`, `Views/`, `Utilities/`):
  - `FittingManagement`, `Weight`, `TableOfContent`, `DetailDesign`, `PanelData`
- Folder `Module1`, `Module2` (template gốc) vẫn còn — chưa xóa, chờ user confirm

### Trạng thái

- **Phase:** 0 — Scaffold & Setup
- Cấu trúc folder + placeholder files đã xong, sẵn sàng push lên GitHub
- PaletteManager.cs đã fix xong audit
- Chưa có file `.xaml` nào — plugin chưa build được (5 View chưa chuyển sang UserControl)

### Vấn đề tồn đọng (từ Audit)

| # | Vấn đề | Trạng thái |
|---|---|---|
| #2.5 | Thiếu `[CommandMethod]` cho MCG_Show/Hide/Toggle | Chờ bổ sung sau |
| #6.2 | Chưa có 5 View + ViewModel | Chờ bổ sung sau |
| #3.3 | `Hide()` thiếu try/catch | Chờ bổ sung sau |
| #3.4 | `Toggle()` thiếu log | Chờ bổ sung sau |
| #3.5 | `Initialize()` thiếu try/catch | Chờ bổ sung sau |
| — | Folder `Module1`, `Module2` chưa xóa | Chờ user confirm |

### Bước tiếp theo

1. **Tạo 5 View (UserControl)** — để plugin build được:
   - `Views/DetailDesign/DetailDesignView.xaml` + `.xaml.cs`
   - `Views/FittingManagement/FittingManagementView.xaml` + `.xaml.cs`
   - `Views/PanelData/PanelDataView.xaml` + `.xaml.cs`
   - `Views/TableOfContent/TableOfContentView.xaml` + `.xaml.cs`
   - `Views/Weight/WeightView.xaml` + `.xaml.cs`
2. **Thêm `[CommandMethod]`** cho MCG_Show / MCG_Hide / MCG_Toggle trong PaletteManager.cs
3. **Tạo 5 ViewModel** tương ứng trong `Views/` hoặc folder riêng
4. **Thử build lần đầu** — `dotnet build -c Debug`

### Ghi chú API

- **PaletteSet thứ tự khởi tạo:** `new PaletteSet()` → `AddVisual()` → `DockEnabled/Size/KeepFocus` → `Visible = true`. Nếu set Size/Dock trước AddVisual, Palette sẽ không dock đúng.
- **KeepFocus = true:** Giữ focus trên palette sau click — cần kết hợp với `Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView()` trong button handler khi muốn trả focus về bản vẽ.
- **GUID cố định:** `2b80cfe9-c560-49d6-8a09-9d636260fcf2` — không được thay đổi sau khi deploy.
- **Log message:** User đã chỉnh quy tắc — log message viết bằng **English** (không phải tiếng Việt như ban đầu).
