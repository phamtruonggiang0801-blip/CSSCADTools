## Session 2026-07-04 21:35

### Đã làm

- User hỏi có cách nào tránh hardcode danh sách từ khóa (`{PLATE, PIPE, BAR}`) trong `SteelSpecClassifier` không, vì danh sách này sẽ luôn thiếu khi gặp tên vật tư mới trong tương lai.
- Thiết kế lại theo hướng nhận diện bằng **tín hiệu cấu trúc dữ liệu đã có sẵn**, không cần biết trước tên gọi tiếng Anh:
  1. `Drw` (mã tham chiếu bản vẽ khác) đang TRỐNG — qua dữ liệu thật: dòng assembly (TRIANGLE PIECE, HOLD-DOWN ASSEMBLY...) luôn CÓ Drw trỏ sang bản vẽ khác; dòng vật tư thô (PLATE, SQUARE PIPE, EYE PLATE, ANGLE BAR...) luôn KHÔNG có Drw.
  2. Size có "hình dạng" kích thước vật liệu: hoặc là 1 số đơn thuần (`"10.0"`), hoặc có chứa `x` nối các số (`"15.0x160x250"`, `"65x65x6.0, L=310"`). Size kiểu `"L=550mm"` (số kèm nhãn chữ, không `x`) bị loại.
- Viết lại hoàn toàn `Utilities/SteelSpecClassifier.cs`: bỏ `RawMaterialFormKeywords` (HashSet từ khóa), thay bằng `LooksLikeStockDimensions()` (regex kiểm tra hình dạng Size) kết hợp điều kiện `Drw` trống. Không còn phụ thuộc Description chứa từ khóa tiếng Anh nào.
- Build thành công → `CSSCADTools_20260704_2135.dll`.
- Sanity-check ngoài AutoCAD: 11/11 case đúng, gồm toàn bộ case cũ (PLATE, SQUARE PIPE, EYE PLATE, ANGLE BAR) + 2 case HOÀN TOÀN MỚI chưa từng có trong danh sách từ khóa nào ("CHANNEL", "TUBE OVAL") vẫn phân loại đúng — chứng minh hết phụ thuộc hardcode + các case an toàn (assembly có Drw, Size dạng "L=..." không có "x") vẫn bị loại đúng.

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ. Chưa test lại full 13 file trong AutoCAD với thay đổi này. `BomTemplateExporter.cs` không cần sửa gì thêm (logic highlight vẫn tương thích).

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_2135.dll`, chạy lại export trên toàn bộ 13 file, xác nhận kết quả phân loại giống hệt (hoặc tốt hơn) so với bản có hardcode từ khóa trước đó.
- Nếu về sau phát hiện 1 dòng vật tư thô có Drw KHÔNG trống (vd mã tham chiếu nội bộ không phải link sang bản vẽ khác) khiến bị loại nhầm, cần xem lại điều kiện #1 — có thể phải đổi từ "Drw trống" sang "Drw trống HOẶC không khớp bản vẽ nào trong batch" (giống logic `hasOwnDrawing` đã có trong `BomTemplateExporter.cs`).

## Session 2026-07-04 11:05

### Đã làm

- User báo file `4255`: item "EYE PLATE" (AH36, `378x165x30.0`) và "ANGLE BAR" (A Mild Steel, `65x65x6.0, L=310`) có đủ Material+Size hợp lệ nhưng vẫn bị bôi vàng ở sheet Material List.
- Nguyên nhân: `SteelSpecClassifier` cũ chỉ nhận diện dạng vật tư bằng cách so khớp **CHÍNH XÁC** toàn bộ Description với "PLATE" hoặc "SQUARE PIPE". "EYE PLATE" (có tiền tố chức năng "EYE") và "ANGLE BAR" (dạng vật tư hoàn toàn mới, chưa có trong danh sách) đều không khớp chính xác → bị coi là "chưa đủ rõ" → không phân loại → bôi vàng nhầm dù dữ liệu đủ.
- Fix: đổi điều kiện nhận diện từ so khớp CHÍNH XÁC toàn chuỗi sang so khớp **từ cuối cùng** của Description với tập từ khóa dạng vật tư thô `{"PLATE", "PIPE", "BAR"}`. Lý do: tên mô tả chức năng (EYE/COMPRESSION/ANGLE/ADJUSTING...) luôn đứng TRƯỚC từ khóa dạng vật tư ở cuối, và về bản chất vẫn là 1 tấm/thanh/ống tiêu chuẩn — độ dày vẫn là số nhỏ nhất trong Size (rule đã có từ trước, không đổi).
- Build thành công → `CSSCADTools_20260704_1105.dll`.
- Sanity-check ngoài AutoCAD: 8/8 case đúng — gồm 4 case cũ (không hồi quy), 2 case mới từ file 4255 (EYE PLATE→AH36/30.0, ANGLE BAR→A Mild Steel/6.0), 2 case an toàn (assembly HOLD-DOWN ASSEMBLY với Size="L=550mm" và TRIANGLE PIECE không Material vẫn KHÔNG bị phân loại nhầm).

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ. Chưa test lại full 13 file trong AutoCAD với thay đổi này.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_1105.dll`, chạy lại export trên toàn bộ 13 file, xác nhận EYE PLATE/ANGLE BAR (và các dạng tương tự như COMPRESSION PLATE, FLAT BAR, ROUND BAR nếu có) giờ được phân loại đúng, không còn bị bôi vàng nhầm.
- Nếu phát hiện thêm dạng vật tư có từ khóa cuối KHÁC "PLATE/PIPE/BAR" (vd "ANGLE", "CHANNEL", "TUBE"...) cần tự động phân loại, chỉ cần thêm từ khóa vào `RawMaterialFormKeywords` trong `SteelSpecClassifier.cs`.

## Session 2026-07-04 10:52

### Đã làm

- User cho biết quy tắc đọc độ dày tổng quát hơn: độ dày LUÔN là **số nhỏ nhất** trong Size, không phải cố định ở 1 vị trí (đầu/thứ 3) — vd `630X190X30.0` → dày=30.0 (số cuối), `138x50x20.0` → dày=20.0 (số cuối). Trước đây `PlateSizeRegex`/`SquarePipeSizeRegex` giả định vị trí cố định (PLATE=số đầu, SQUARE PIPE=số thứ 3) nên sẽ đọc SAI với 2 ví dụ này (PLATE sẽ nhầm lấy 630/138).
- Đơn giản hóa hoàn toàn `Utilities/SteelSpecClassifier.cs`: bỏ regex/rule riêng theo từng dạng, thay bằng 1 quy tắc chung — trích TẤT CẢ số trong Size, lấy số NHỎ NHẤT làm độ dày. Vẫn giữ "cổng an toàn" theo Description (chỉ áp dụng cho `PLATE`, `SQUARE PIPE` — dạng đã xác nhận Size thực sự mô tả kích thước vật liệu thô) để tránh áp nhầm quy tắc này lên Size của dòng assembly (vd "L=550mm" — chiều dài tổng thể, không phải độ dày).
- Build thành công → `CSSCADTools_20260704_1052.dll`.
- Sanity-check ngoài AutoCAD: chạy lại toàn bộ 6 case cũ (không hồi quy) + 2 case mới (630x190x30.0 → 30.0, 138x50x20.0 → 20.0) + 1 case an toàn (assembly "L=550mm" vẫn không bị phân loại nhầm) — cả 7/7 đều đúng.

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ. Chưa test lại full 13 file trong AutoCAD với thay đổi này.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_1052.dll`, chạy lại export trên toàn bộ 13 file, đối chiếu sheet "Material List" — đặc biệt các dòng PLATE có Size dạng "WxLxT" (dày ghi ở cuối) giờ phải phân loại đúng cột thay vì bị bỏ qua/gán sai như trước.

## Session 2026-07-04 10:43

### Đã làm

- User yêu cầu: tô vàng dòng trong sheet "Material List" mà Item Name (vd "COMPRESSION PLATE") vừa KHÔNG có bản vẽ riêng để tra cứu thêm, vừa KHÔNG có vật liệu tự phân loại được.
- Xác định điều kiện chính xác: "không có bản vẽ riêng" = `BomItem.Drw` trống HOẶC không khớp với bất kỳ Drawing No nào đang có mặt trong lô file đang xử lý (tức không thể chase sang file khác để tra cứu chi tiết vật liệu) — không đơn thuần là "Drw trống" (vì có thể có mã Drw nhưng mã đó không phải file thật trong lô, như "005" không khớp file nào cả).
- Mở rộng `Utilities/MinimalXlsxWriter.cs`: thêm `GridRow.Highlight`, thêm fill vàng (`fillId=2`) + `cellXfs` index 2 vào styles.xml; `BuildGridSheet` chọn `s="2"` khi `Highlight=true` (ưu tiên `Bold` nếu cả 2 đều bật, nhưng thực tế 2 cờ này không overlap ở dòng item).
- Cập nhật `Utilities/BomTemplateExporter.cs`: tính `hasOwnDrawing` (Drw khớp 1 trong các Drawing No đã gom nhóm trong batch) và `classified` (đã phân loại qua SteelSpecClassifier); `needsManualReview = !hasOwnDrawing && !classified` → truyền vào `Highlight` khi add hàng item.
- Build thành công → `CSSCADTools_20260704_1043.dll`.
- Sanity-check ngoài AutoCAD: COMPRESSION PLATE (Drw="005", không khớp file nào) → tô vàng đúng; TRIANGLE PIECE (Drw="5800", khớp file 5800.dxf có trong lô) → KHÔNG tô vàng dù cũng chưa phân loại được vật liệu; PLATE đã phân loại (AH36/10.0) → KHÔNG tô vàng dù Drw trống. Toàn bộ XML hợp lệ.

### Trạng thái

- Code xong, build xong, đã tự-test logic highlight bằng dữ liệu mô phỏng 3 trường hợp. Chưa test lại full 13 file trong AutoCAD.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_1043.dll`, chạy lại export trên toàn bộ 13 file, kiểm tra sheet "Material List": các dòng vàng có đúng là những dòng cần tra cứu/điền thủ công không (vd COMPRESSION PLATE nếu có trong bộ dữ liệu thật, hoặc các item khác không có Drw hợp lệ và không tự phân loại được).

## Session 2026-07-04 10:33

### Đã làm

- User phát hiện 2 vấn đề ở file `5800`:
  1. Item 1 (PLATE, Material = "Q355D,SM490B" — 2 grade tương đương) bị xuất thành **2 row riêng** trong sheet BOM thay vì gộp vào đúng 1 row của Item 1.
  2. Item 2, 3 (PLATE, Material = "SUS 316L") không xuất hiện ở sheet Material List dù dữ liệu hợp lệ.
- Phân tích bằng dữ liệu thô (X,Y,Text) trích trực tiếp từ block: phát hiện ô MATERIAL của Item 1 được AutoCAD/SolidWorks lưu thành **2 MTEXT riêng biệt** ở 2 Y lệch nhau ±7 đơn vị quanh baseline hàng ("Q355D, " phía trên, "SM490B" phía dưới) — KHÔNG gộp chung 1 entity. Lệch này vượt `ROW_TOLERANCE=2.0` nên bị thuật toán gom hàng theo Y tách thành 2 "hàng" riêng chỉ có 1 ô — sinh ra 2 dòng ma, đồng thời hàng Item 1 thật bị THIẾU giá trị Material.
  - Vấn đề #2 hóa ra là bug khác, không liên quan đến "thiếu cột SUS316L": Size của Item 2/3 chỉ ghi `"10.0"` (không có dạng `WxL` đi kèm), trong khi regex PLATE cũ bắt buộc phải có `"x"` theo sau số đầu → không match → bị coi là "chưa đủ rõ", không phân loại được (không phải do catalog thiếu cột — cột luôn sinh động).
- Fix `Utilities/BomTableParser.cs`:
  - Thêm `MergeFragmentRows`: sau khi gom hàng theo Y, tách hàng thành "hàng thật" (≥3 ô) và "mảnh vỡ" (<3 ô), gộp mảnh vỡ vào hàng thật gần nhất theo khoảng cách Y.
  - Đổi `AssignColumn` từ ghi đè sang **nối chuỗi** (`Append`) khi 1 cột nhận nhiều mảnh (xử lý đúng thứ tự nhờ sort cell theo Y giảm dần trước khi gán).
- Fix `Utilities/SteelSpecClassifier.cs`: nới lỏng `PlateSizeRegex` — chấp nhận Size chỉ có 1 số (không bắt buộc có "x" theo sau), vẫn coi là độ dày.
- Build thành công → `CSSCADTools_20260704_1033.dll`.
- Sanity-check ngoài AutoCAD bằng dữ liệu thật trích từ file `5800` (6 cell thô → 5 hàng dữ liệu → 3 hàng sau merge): Item 1 → Material="Q355D, SM490B" (đúng 1 hàng), Item 2/3 → Material="SUS 316L" Size="10.0" → phân loại đúng cột "SUS 316L / 10.0".

### Trạng thái

- Code xong, build xong, đã tự-test bằng dữ liệu thật (ngoài AutoCAD). Chưa test lại full 13 file trong AutoCAD với thay đổi này.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_1033.dll`, chạy lại export trên toàn bộ 13 file, kiểm tra: (1) sheet BOM không còn dòng ma nào (chỉ có Material, các cột khác trống), (2) file 5800 Item 1 có Material="Q355D, SM490B", (3) sheet Material List có thêm cột "SUS 316L / 10.0" chứa Item 2, 3.
- Lưu ý: `MIN_CELLS_FOR_REAL_ROW=3` là ngưỡng heuristic dựa trên dữ liệu quan sát được (mảnh vỡ luôn có đúng 1 ô, hàng thật luôn có ≥4-6 ô) — nếu sau này gặp file có hàng thật hợp lệ nhưng quá ít cột điền, cần xem lại ngưỡng này.

## Session 2026-07-04 10:11

### Đã làm

- User xác nhận quy tắc đọc độ dày cho dạng `SQUARE PIPE`: size viết dạng `WIDTHxWIDTHxTHICKNESS - LENGTH` (vd `250x250x9.0 - 750` → thickness = `9.0`, số thứ 3), khác với `PLATE` (`THICKNESSxWIDTHxLENGTH`, số đầu).
- Tái cấu trúc `Utilities/SteelSpecClassifier.cs` từ "chỉ nhận PLATE" thành **bảng quy tắc theo từng dạng vật tư** (`FormRule` list: form name + hàm đọc thickness riêng) — dễ mở rộng thêm dạng khác (ROUND BAR, PIPE...) khi có quy tắc xác nhận, không phải sửa lại logic tổng.
- Đã thêm rule cho `SQUARE PIPE`. Các dạng chưa được xác nhận quy tắc vẫn trả về `null` (để trống, không đoán).
- Build thành công → `CSSCADTools_20260704_1011.dll`.
- Sanity-check ngoài AutoCAD: SQUARE PIPE (250x250x9.0 - 750, AH36) → Material=AH36 Thickness=9.0 ✓; PLATE vẫn đúng như cũ; assembly/ROUND BAR/size thiếu đều trả về null đúng như thiết kế.

### Trạng thái

- Code xong, build xong, đã tự-test riêng phần classifier. Chưa test lại full 13 file trong AutoCAD với thay đổi này.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_1011.dll`, chạy lại export, kiểm tra sheet "Material List": dòng SQUARE PIPE trong file `4050` giờ phải xuất hiện giá trị 48.8 ở đúng cột `AH36 / 9.0` thay vì chỉ có ở TOTAL/KG.
- Nếu người dùng phát hiện thêm dạng vật tư khác (ROUND BAR, PIPE, FLAT BAR...) cần tự động phân loại, chỉ cần bổ sung 1 `FormRule` mới vào `Rules` trong `SteelSpecClassifier.cs` — không cần sửa `BomTemplateExporter.cs`.

## Session 2026-07-04 10:01

### Đã làm

- User báo `eProperClassSeparatorExpected` lại xảy ra ở file `4050` — file này TỪNG chạy tốt ở lần test trước, trong khi `1225`/`1235` (từng lỗi y hệt) lần này lại chạy được. Kết luận: lỗi **không cố định theo nội dung file**, mang tính trạng thái phiên AutoCAD (session-state) khi gọi `DxfIn` liên tiếp nhiều lần, không phải file bị hỏng.
- Fix: `BomDxfScanner.cs` — thêm cơ chế **tự động thử lại tối đa 3 lần** với `Database` hoàn toàn mới mỗi lần, chỉ báo warning vĩnh viễn nếu cả 3 lần đều lỗi.
- Build thành công → `CSSCADTools_20260704_1001.dll`.

### Trạng thái

- Đang chờ user test lại bản build mới trên toàn bộ 13 file để xác nhận cơ chế retry giải quyết được lỗi ngẫu nhiên này.

### Bước tiếp theo

- Nếu retry 3 lần vẫn không đủ (lỗi vẫn còn sau khi thử lại), cần tăng `MAX_ATTEMPTS` hoặc tìm cách khác (vd tách sang tiến trình AutoCAD riêng cho mỗi file, hoặc dùng `Application.DocumentManager.Open` thay vì side-database `DxfIn`).

## Session 2026-07-04 00:12

### Đã làm

- Thêm sheet mới **"Material List"** vào workbook xuất ra, mô phỏng layout file mẫu `1112470WD_Yard fitting_material quality quantity_FINAL(20-01-26).xlsm` (sheet "piirluet"), theo phương án đã thống nhất với user:
  - Title block (MacGregor, YARD/SHIP, CLASS, DATE, SIGN, EDITION) để trống hoàn toàn cho user tự điền.
  - Gom nhóm theo từng bản vẽ (Drawing No tách từ tên file qua regex `-(\d+)_`), mỗi section có hàng tiêu đề Drawing No + ô "1 PANEL" mặc định (user tự sửa số nếu khác).
  - Cột Item/Name/Size/Q'ty/Delivery map từ `BomItem.Drw/Description/Size/Qty/Delivery`.
  - Ma trận cột Grade+Thickness **sinh động** theo dữ liệu thực tế (không hardcode catalog) — chỉ tự động gán dòng nào Description="PLATE" + Material có giá trị + Size khớp pattern "số x số x số" (thickness = số đầu). Dòng dạng khác (SQUARE PIPE, ROUND BAR, assembly...) để trống cột chi tiết, chỉ có TOTAL/KG — tránh đoán sai vì các dạng vật tư khác viết Size không nhất quán.
  - Dòng tổng cuối cùng (TOTAL/KG) = tổng theo từng cột + tổng toàn bộ.
- File mới: `Utilities/SteelSpecClassifier.cs` (quy tắc phân loại PLATE an toàn), `Utilities/BomTemplateExporter.cs` (dựng grid layout).
- Mở rộng `Utilities/MinimalXlsxWriter.cs`: thêm `SheetData.GridRows` (chế độ ghi ô tự do theo `Dictionary<int,string>` + cờ Bold theo hàng) bên cạnh chế độ Headers+Rows cũ — không ảnh hưởng sheet BOM/Warnings đang chạy.
- `BomExcelExporter.cs`: thêm sheet "Material List" vào giữa sheet "BOM" và "Warnings" (đúng lựa chọn "thêm sheet mới bên cạnh sheet flat hiện có").
- Build thành công → `CSSCADTools_20260704_0012.dll`.
- Sanity-check ngoài AutoCAD bằng project console tạm, dùng DỮ LIỆU THẬT trích từ file `4050` (12 dòng PLATE có Material AH36/Mild Steel với nhiều độ dày khác nhau, cố tình có 1 dòng SQUARE PIPE có Material để test loại trừ) + file `1215` (2 dòng assembly không Material): kết quả cột động sinh đúng (AH36 tách 4 cột theo thickness 12/15/16/40, Mild Steel tách 2 cột 15/20), dòng SQUARE PIPE bị loại đúng như thiết kế, tổng từng cột và tổng toàn bộ (200.0) khớp chính xác với tính tay.

### Trạng thái

- Code xong, build xong, đã tự-test logic phân loại + tổng hợp số liệu bằng dữ liệu thật ngoài AutoCAD. CHƯA test thật trong AutoCAD với đầy đủ 13 file DXF.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260704_0012.dll`, chạy lại export trên toàn bộ 13 file, mở sheet "Material List" kiểm tra: layout hiển thị đúng, số liệu cột động hợp lý, so sánh cảm quan với file mẫu `.xlsm`.
- Nếu số lượng cột động quá nhiều/ít so với kỳ vọng thực tế của user (vì catalog sinh động theo đúng 13 file test, không phải theo toàn bộ dự án thật), cần trao đổi thêm.

### Ghi chú API

- `MinimalXlsxWriter` giờ có 2 chế độ: `Headers`+`Rows` (bảng đơn giản, dùng cho BOM/Warnings) và `GridRows` (layout tự do theo tọa độ ô, dùng cho Material List). Khi thêm sheet mới kiểu template, ưu tiên dùng `GridRows` thay vì cố nhồi vào chế độ Headers+Rows.

## Session 2026-07-03 23:54

### Đã làm

- Test lại sau khi thêm log `DxfIn`: 2 file `1225`/`1235` (từng lỗi `eProperClassSeparatorExpected`) đã qua được, chỉ còn cảnh báo cho `4010` ("không xác định được hàng header").
- User kiểm tra thủ công file `4010` và phát hiện: bảng BOM thật nằm trong block **`SW_TABLEANNOTATION_2`**, không phải `_1`. Xác nhận lại bằng raw-text: block `_1` trong file này chỉ có 1 MTEXT ghi chú vật liệu, block `_2` mới có đủ 10/10 header (ITEM/QTY/.../DELIVERY) và item bắt đầu từ "1" (PIN STOPPER D240).
- Nguyên nhân gốc: SolidWorks đánh số block table annotation tuần tự theo SỐ BẢNG có trong bản vẽ (`_0`, `_1`, `_2`...), vị trí bảng BOM trong dãy số này KHÔNG cố định giữa các file — hardcode `SW_TABLEANNOTATION_1` là sai giả định.
- Sửa `Utilities/BomDxfScanner.cs`: đổi từ tra cứu tên block cố định (`bt.Has("SW_TABLEANNOTATION_1")`) sang duyệt TẤT CẢ block có tiền tố `SW_TABLEANNOTATION_` trong file, gọi `BomTableParser.Parse` cho từng block, chỉ giữ lại (gộp) dữ liệu từ (các) block nào thực sự parse ra được item (`items.Count > 0`). Nếu không có block nào trong file cho ra dữ liệu, mới báo warning (kèm warning riêng của từng block con để dễ debug).
- Build thành công → `CSSCADTools_20260703_2354.dll`.

### Trạng thái

- Đang chờ user NETLOAD bản mới nhất, chạy lại toàn bộ 13 file để xác nhận đủ 13/13 (hoặc 12/13 nếu `4010` thật sự chỉ có 1 bảng lệch số — nhưng đã xác nhận `_2` có dữ liệu nên kỳ vọng 13/13 thành công).

### Bước tiếp theo

- Chạy lại full 13 file, đối chiếu tổng số dòng BOM xuất ra với kỳ vọng (12 file gốc ~74 dòng theo sanity-check trước + số dòng của `4010` từ block `_2`).
- Nếu vẫn còn cảnh báo bất thường (ví dụ 1 file có 2 block cùng hợp lệ → bị gộp trùng), cần xem lại có nên giới hạn "chỉ lấy 1 block duy nhất" hay "gộp tất cả" tùy theo dữ liệu thực tế người dùng gặp phải.

### Ghi chú API

- Tên block `SW_TABLEANNOTATION_N` (N = 0,1,2...) do SolidWorks tự sinh theo thứ tự tạo bảng trong bản vẽ — KHÔNG được coi N là định danh cố định cho "bảng BOM chính". Phải dò theo tiền tố + xác thực nội dung (có header hợp lệ hay không), không dò theo tên chính xác.

## Session 2026-07-03 23:41

### Đã làm

- Test thật trong AutoCAD (sau khi fix ClosedXML): xuất được file .xlsx (không còn lỗi SixLabors.Fonts), nhưng **0/13 file** ra BOM, cả 13 đều rơi vào warning `"lỗi đọc file — eMissingSymbolTableRec"`.
- Xác định nguyên nhân: `BomDxfScanner.cs` dùng `new Database(true, true)` (buildDefaultDrawing=true) trước khi gọi `db.DxfIn(...)`. Việc này tạo sẵn các bản ghi mặc định (Model_Space, layer "0"...) rồi mới merge nội dung DXF vào, gây xung đột symbol table khi merge — các file DXF này có nguồn gốc từ SolidWorks (tên block `SW_TABLEANNOTATION_1`) nên cấu trúc bảng ký hiệu có thể không khớp hoàn toàn kỳ vọng của AutoCAD khi merge.
- Fix: đổi thành `new Database(false, true)` — cùng pattern với `ReadDwgFile`, để `DxfIn` tự dựng toàn bộ bảng ký hiệu từ chính nội dung file DXF, không tạo default trước.
- Build lại thành công → `CSSCADTools_20260703_2341.dll`.

### Trạng thái

- Đang chờ user NETLOAD lại DLL mới nhất và test lại trên 13 file DXF mẫu để xác nhận hết lỗi `eMissingSymbolTableRec` và ra đúng số dòng BOM kỳ vọng (theo sanity-check ngoài AutoCAD trước đó: 12/13 file có bảng hợp lệ, tổng cộng nhiều chục dòng; file `4010` sẽ tiếp tục rơi vào Warnings vì chỉ có 1 ghi chú vật liệu, không phải bảng cột).

### Bước tiếp theo

- Nếu `new Database(false, true)` vẫn lỗi tương tự: thử `db.DxfIn(file, null)` với log file thật (truyền path thay vì `null`) để xem log chi tiết dòng nào trong DXF gây lỗi; hoặc thử đọc thẳng bằng `acad_strlsort`/`ObjectDBX` COM nếu managed API tiếp tục không ổn định với DXF nguồn SolidWorks.

### Ghi chú API

- `new Database(false, true)` + `DxfIn` là tổ hợp đúng để đọc 1 file DXF độc lập (side database), giống hệt pattern của `ReadDwgFile`. `buildDefaultDrawing=true` chỉ nên dùng khi cần MERGE dxf vào 1 database đã có sẵn nội dung khác (ví dụ import block vào bản vẽ đang mở), không phải khi mở dxf như 1 file gốc.

## Session 2026-07-03 23:35

### Đã làm

- Fix lỗi runtime khi bấm "EXPORT BOM TO EXCEL" trong AutoCAD: `The type initializer for 'SixLabors.Fonts.Tables.TableLoader' threw an exception`. Thử bỏ `AdjustToContents()` trước nhưng lỗi vẫn còn (ClosedXML đụng SixLabors.Fonts ngay khi tạo XLWorkbook/Worksheet, không chỉ lúc autofit) → kết luận: ClosedXML không dùng được ổn định trong tiến trình AutoCAD do xung đột assembly (`System.Memory`/`System.Buffers`) giữa host và plugin, AutoCAD không áp dụng app.config/binding redirect của plugin nên không patch được.
- Quyết định: **bỏ hẳn ClosedXML**, thay bằng `Utilities/MinimalXlsxWriter.cs` — tự viết tay OOXML (zip + XML thô qua `System.IO.Compression`), không phụ thuộc thư viện ngoài nào ngoài .NET Framework có sẵn. Hỗ trợ: nhiều sheet, header đậm, độ rộng cột cố định, freeze header row, autofilter, escape XML đúng chuẩn (`&`, `<`, `>`, ký tự điều khiển không hợp lệ).
- Viết lại `Utilities/BomExcelExporter.cs` dùng `MinimalXlsxWriter` thay vì `XLWorkbook`.
- `CSSCADTools.csproj`: bỏ `PackageReference ClosedXML`, thêm `Reference System.IO.Compression` + `System.IO.Compression.FileSystem`.
- Build lại thành công, xác nhận `bin/Debug/net48/` không còn `ClosedXML.dll`/`SixLabors.Fonts.dll`/`DocumentFormat.OpenXml.dll`/`System.Memory.dll` trong output mới.
- Test độc lập `MinimalXlsxWriter` bằng 1 console project tạm (net8, không phụ thuộc AutoCAD API): sinh file .xlsx mẫu có ký tự đặc biệt (`&`, `<`, `>`, dấu ngoặc kép, Unicode "Ø") và cảnh báo, mở lại bằng `ZipFile` + `XmlDocument.Load` cho từng entry — toàn bộ 7 XML part đều well-formed, escape đúng, Unicode giữ nguyên.

### Trạng thái

- Phase hiện tại: Tính năng Export BOM to Excel — đã fix lỗi ClosedXML/SixLabors.Fonts, build xong, đã tự-test cấu trúc OOXML hợp lệ ngoài AutoCAD. VẪN CHƯA test thật trong AutoCAD (NETLOAD bản build mới nhất + bấm nút + mở file .xlsx bằng Excel thật để xác nhận không báo lỗi corrupt).

### Bước tiếp theo

- Đóng AutoCAD hoặc NETUNLOAD DLL cũ đang giữ khóa file (`ClosedXML.dll`, `SixLabors.Fonts.dll`... đang bị lock bởi AutoCAD Application) → NETLOAD lại DLL mới nhất trong `bin\Debug\net48\` → bấm "EXPORT BOM TO EXCEL" → mở file .xlsx xuất ra bằng Excel thật để xác nhận không bị báo "file corrupt/cần sửa".

### Ghi chú API

- ClosedXML (qua SixLabors.Fonts) không an toàn để dùng trong plugin AutoCAD .NET — bất kỳ lib nào phụ thuộc SixLabors.Fonts/System.Drawing.Common cho đo font đều có rủi ro xung đột assembly tương tự trong tiến trình host. Nếu cần xuất Excel từ plugin AutoCAD, ưu tiên viết tay OOXML tối giản (`MinimalXlsxWriter`) hoặc dùng thư viện xuất CSV thuần.

## Session 2026-07-03 22:20

### Đã làm

- Thêm chức năng "Export BOM to Excel": đọc block `SW_TABLEANNOTATION_1` trong nhiều file DXF (thư mục do user chọn), tái dựng bảng BOM theo tọa độ (X,Y) vì ô trống không có entity, gộp toàn bộ vào 1 file `.xlsx` (1 sheet duy nhất + sheet "Warnings" cho file lỗi/thiếu header).
- File mới: `Models/BomModels.cs` (BomItem, BomScanResult), `Utilities/BomTableParser.cs` (thuật toán tái dựng bảng + dò header theo từ khóa, không giả định vị trí trên/dưới), `Utilities/BomDxfScanner.cs` (đọc nhiều DXF qua `Database.DxfIn`), `Utilities/BomExcelExporter.cs` (dùng ClosedXML).
- Sửa: `CSSCADTools.csproj` (thêm PackageReference ClosedXML 0.102.3), `Views/MainPaletteControl.xaml` + `.xaml.cs` (thêm nút "EXPORT BOM TO EXCEL", theo đúng pattern `BtnCheckDetails_Click` đã có).
- Đã build thành công (`dotnet build -c Debug`, 0 lỗi).
- Đã kiểm chứng thuật toán (row/column detection) bằng script awk độc lập chạy trên toàn bộ 13 file DXF mẫu trong "Giang -Yard Fitting Material": 12/13 file dò đủ 10/10 header keyword; phát hiện 2 file (`4050`, `4110`) dùng header "MASS" thay vì "Weight" → đã thêm alias "MASS" vào `HeaderKeywords`. File `4010` không có bảng cột (chỉ 1 ghi chú vật liệu dạng text tự do) → đúng như thiết kế, sẽ rơi vào sheet Warnings chứ không bị parse sai.

### Trạng thái

- Phase hiện tại: Tính năng Export BOM to Excel — code xong, build xong, đã sanity-check thuật toán ngoài AutoCAD. CHƯA test thật trong AutoCAD (NETLOAD + bấm nút) vì môi trường hiện tại không có AutoCAD runtime để chạy trực tiếp.

### Bước tiếp theo

- File: `Views/MainPaletteControl.xaml.cs` | Method: `BtnExportBom_Click` | Mục tiêu: NETLOAD DLL trong AutoCAD, chạy lệnh `CSSTools`, bấm "EXPORT BOM TO EXCEL", trỏ vào thư mục "Giang -Yard Fitting Material", kiểm tra file .xlsx xuất ra khớp dữ liệu thực tế và đối chiếu với file mẫu `1112470WD_Yard fitting_material...xlsm`.

### Ghi chú API

- `Database` không có `ReadDxfFile` — phải dùng `db.DxfIn(path, null)` với `new Database(true, true)` (buildDefaultDrawing=true), khác với `ReadDwgFile` dùng `new Database(false, true)`.
- Header của bảng BOM không cố định 1 wording: cột khối lượng có nơi ghi "Weight", có nơi ghi "MASS" — nên dò cột theo tập từ khóa (có alias), không hardcode 1 tên duy nhất.
- Bảng BOM trong block `SW_TABLEANNOTATION_1` có thể vẽ header ở dưới cùng, item tăng dần từ dưới lên trên — không được giả định hàng đầu tiên là header.
