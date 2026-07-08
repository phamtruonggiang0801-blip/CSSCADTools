## Session 2026-07-08 02:20

### Đã làm

- User dán kết quả thật (Catalog + REVERSE) cho 2 nhóm detail mới: **G56** (DBText "DET.G56" ở 4 file khác nhau 2006/2154/2204/2304, MỖI FILE chỉ 1 lần) và **J1-J5** (5 DBText ở file 2701 + 3 MText tham chiếu "MIRROR IMAGE" ở 2702/2703/2704) — CẢ 2 nhóm đều báo UNREFERENCED 100% dù rõ ràng nhiều file đang dùng chung.
- **Phân tích**: khác session trước (detail lặp ≥2 lần TRONG CÙNG 1 file), đây là detail xuất hiện Ở NHIỀU FILE KHÁC NHAU, mỗi file chỉ 1 lần — rule "≥2 lần/file" (session trước) không bắt được case này, nên rơi về nhánh cũ: file `detectedSourceType == null` → MỌI file đều bị coi là "definer". Vì `DetailDefinitions` là Dictionary (chỉ thêm nếu chưa có key), CHỈ file được xử lý ĐẦU TIÊN trong vòng lặp quét mới thật sự đăng ký được — các file còn lại (dù đang tham chiếu tới CÙNG Detail ID) bị bỏ qua ÂM THẦM, không tạo được `SourceDetails` nào → REVERSE luôn báo UNREFERENCED.
- Xác nhận qua AskUserQuestion: các file này (2006/2154/2204/2304/2701-2704) có title block thuộc loại **"General Details"** — KHÔNG nằm trong 4 loại đang nhận diện (TOP PLATE/BOTTOM PLATE/LONGITUDINAL BEAM/TRANSVERSAL PLATE) — đúng khớp phân tích.
- **Thiết kế lại `DwgDatabase.ScanFiles`** thành 2 GIAI ĐOẠN thay vì xử lý xen kẽ trong lúc quét (tránh phụ thuộc thứ tự file — rủi ro thật: nếu file tham chiếu vô tình được xử lý TRƯỚC file định nghĩa thật, code cũ sẽ đăng ký NHẦM file tham chiếu làm "definer"):
  1. **Giai đoạn quét**: mỗi file chỉ THU THẬP `(FileName, SourceType, DistinctDetails, OccurrenceCount)` vào `perFileDetails`, KHÔNG quyết định định nghĩa/tham chiếu ngay.
  2. **Giai đoạn phân loại** (`ClassifyDetails`, chạy SAU khi quét xong TOÀN BỘ file): (a) tự đủ trong 1 file (rule cũ, ≥2 lần/file, không đổi); (b) **MỚI** — với Detail ID chưa tự đủ, nếu KHÔNG phân loại được (title block không khớp 4 loại đã biết) VÀ xuất hiện ở ≥2 FILE KHÁC NHAU trong nhóm này → tất cả các file đó tự thỏa mãn lẫn nhau: chọn 1 file theo THỨ TỰ ALPHABET (ổn định, không phụ thuộc thứ tự quét) làm "Defined In File", các file còn lại đăng ký THAM CHIẾU CHÉO thật (SourceType="GENERAL DETAILS") thay vì bị bỏ qua âm thầm; (c) chỉ 1 file duy nhất có Detail ID đó trong toàn batch → giữ nguyên hành vi cũ (định nghĩa, có thể UNREFERENCED nếu thật sự không ai dùng).
- Build 0 lỗi. `DwgDatabase.cs` dùng AutoCAD API nên tách riêng ĐÚNG thuật toán `ClassifyDetails` (thuần, không phụ thuộc AutoCAD) ra scratch project `detailtest`, mô phỏng chính xác dữ liệu G56 và J-series (kể cả test đảo NGƯỢC thứ tự file đầu vào để xác nhận order-independence): 8/8 test PASS — G56 định nghĩa đúng ở 2006 (alphabet đầu), 3 file còn lại đúng thành tham chiếu; J1-J5 định nghĩa đúng ở 2701, 3 file MText đúng thành tham chiếu; thứ tự file đảo ngược cho kết quả GIỐNG HỆT.

### Trạng thái

- `DwgDatabase.ScanFiles` giờ tách rõ 2 pha (thu thập → phân loại), loại bỏ hoàn toàn phụ thuộc thứ tự xử lý file — an toàn hơn đáng kể so với cách cũ (xử lý xen kẽ, "ai trước thắng").
- Nhóm file "General Details" (hoặc bất kỳ loại title block nào KHÔNG thuộc 4 loại đã biết) giờ được xử lý đúng như 1 mạng lưới định nghĩa/tham chiếu CHÉO LẪN NHAU, thay vì mặc định coi TẤT CẢ là "definer" như trước.

### Bước tiếp theo

- NETLOAD bản build mới, bấm CHECK DETAILS trên đúng bộ file đã phân tích (G56/J-series) — xác nhận G56 hiện đúng "Defined In 2006.dwg", tham chiếu từ 2154/2204/2304 đều OK (không còn UNREFERENCED); J1-J5 tương tự với 2701 làm gốc.
- Nếu sau này phát hiện thêm loại title block khác (ngoài "General Details") cũng rơi vào tình huống tương tự (không thuộc 4 loại đã biết), cơ chế mới sẽ TỰ ĐỘNG xử lý đúng — không cần sửa code thêm, vì logic không còn phụ thuộc vào việc liệt kê hết mọi loại title block có thể có.

## Session 2026-07-08 01:40

### Đã làm

- User hỏi lại tác dụng sheet REVERSE — đã giải thích: DETAIL kiểm tra chiều "tham chiếu → có định nghĩa không" (bắt tham chiếu treo/MISSING), REVERSE kiểm tra chiều NGƯỢC LẠI "định nghĩa → có ai tham chiếu tới không" (bắt detail mồ côi/UNREFERENCED — dư thừa hoặc lỗi gõ sai tên).
- **Tự phát hiện hệ quả từ fix session trước** (detail "tự đủ" ≥2 lần/file, vd S1/S1M): các detail này được thêm vào `DetailDefinitions` nhưng bị loại khỏi `SourceDetails` (không cần tra cứu chéo nữa) — mà REVERSE lại xây lookup từ `SourceDetails`, nên các detail tự đủ này giờ LUÔN bị báo UNREFERENCED sai, dù thực chất đang được dùng (2 lần ngay trong file của chính nó).
- User xác nhận cần sửa. Đã thêm `ScanResult.SelfDefinedDetails` (`HashSet<string>`, `Models/DetailModels.cs`) — đánh dấu riêng các Detail ID đã được xác định là "tự đủ" trong `DwgDatabase.cs` (ngay tại điểm đăng ký vào `DetailDefinitions` do đạt ngưỡng ≥2 lần). Sửa `FileExporter.BuildReverseSheet`: bỏ qua (không tạo dòng) cho bất kỳ Detail ID nào có trong `SelfDefinedDetails`, trước khi kiểm tra UNREFERENCED.
- Build 0 lỗi. `FileExporter.cs` không phụ thuộc AutoCAD nên test lại được trực tiếp bằng scratch project `reporttest` (đã có từ session tạo Excel report): thêm ca test "DET.S1 tự đủ" — xác nhận DET.S1 hoàn toàn KHÔNG xuất hiện trong sheet REVERSE nữa, trong khi DET.102/DET.103 (thật sự không ai tham chiếu) vẫn đúng UNREFERENCED như cũ. 0 lỗi OpenXml validation.

### Trạng thái

- Sheet REVERSE giờ CHỈ còn báo UNREFERENCED cho detail THẬT SỰ không ai dùng tới (cả tự dùng lẫn tham chiếu chéo đều không có) — không còn báo nhầm các detail tiêu chuẩn/điển hình tự đủ.
- `ScanResult.SelfDefinedDetails` là điểm nối chung giữa `DwgDatabase.cs` (nơi phát hiện) và `FileExporter.cs` (nơi dùng để lọc) — nếu sau này có thêm sheet/kiểm tra khác cần biết "detail nào tự đủ", tái dùng field này, không cần tính lại.

### Bước tiếp theo

- NETLOAD bản build mới, bấm CHECK DETAILS trên đúng bộ file đã phân tích — xác nhận S1/S1M không còn xuất hiện ở CẢ 2 sheet DETAIL và REVERSE (tự đủ, không cần kiểm tra chiều nào); Y2 vẫn đúng MISSING (DETAIL) nếu thật sự chưa có nơi định nghĩa.

## Session 2026-07-08 01:20

### Đã làm

- User dán kết quả thật từ báo cáo Excel mới (sheet DATALOG + DETAIL) của tính năng Check Details: **100% Detail ID (S1, S1M, Y2) đều báo MISSING**, dù DATALOG cho thấy dữ liệu rõ ràng có đầy đủ (DBText "DET.S1"→S1, "DET.S1m"→S1M, kèm MText ghi chú).
- **Phân tích root cause**: `Utilities/DwgDatabase.cs` phân loại **CẢ FILE** (không phải từng Detail ID) vào 1 trong 2 vai trò DUY NHẤT — "REFERENCE" (nếu title block `_MCG_TITLE_NEW` khớp 1 trong 4 loại cấu kiện: TOP PLATE/BOTTOM PLATE/LONGITUDINAL BEAM/TRANSVERSAL PLATE) hoặc "DEFINITION" (nếu không khớp loại nào). Vì **cả 6 file** trong dữ liệu mẫu đều được phân loại "LONGITUDINAL BEAM" → tất cả rơi vào nhánh REFERENCE → `DetailDefinitions` (nơi tra cứu "detail này định nghĩa ở đâu") **RỖNG HOÀN TOÀN** → mọi tra cứu đều MISSING, bất kể dữ liệu thật.
- Xác nhận qua 2 vòng AskUserQuestion: (1) detail S1/S1m/Y2 được vẽ **NGAY TRÊN chính các bản vẽ Longitudinal Beam** đó (không tách file Detail Sheet riêng); (2) đây là detail **TIÊU CHUẨN/ĐIỂN HÌNH** — mỗi bản vẽ áp dụng tự vẽ lại độc lập, KHÔNG phải Detail ID duy nhất toàn dự án cần tra cứu chéo; (3) quy tắc nhận diện "tự đủ": Detail ID xuất hiện **≥ 2 lần trong CHÍNH file đó** (bất kể loại entity DBText/MText) thì coi là đã tự vẽ đầy đủ tại chỗ, không cần tra cứu chéo, không báo MISSING.
- Sửa `Utilities/DwgDatabase.cs` (phần "Detail classification" trong `ScanFiles`): đổi từ phân loại NHỊ PHÂN theo CẢ FILE sang phân loại THEO TỪNG DETAIL ID — đếm `occurrenceCount` của mỗi Detail ID trong CHÍNH file đó (mọi entity, kể cả trùng lặp); Detail ID đạt ≥2 lần → LUÔN đăng ký vào `DetailDefinitions` (tự đủ, bất kể title block); phần còn lại (chỉ 1 lần) → giữ nguyên logic cũ (file có Source Type → coi là tham chiếu cần tra cứu chéo; file không phân loại được → vẫn coi là định nghĩa).
- Build 0 lỗi. `DwgDatabase.cs` dùng AutoCAD API nên không test trực tiếp được — tách riêng ĐÚNG thuật toán phân loại (không phụ thuộc AutoCAD, chỉ thao tác `List<string>`/`Dictionary`) ra scratch project `detailtest` độc lập, mô phỏng đúng dữ liệu thật (S1/S1M xuất hiện 2 lần/file trên 4 file, Y2 chỉ 1 lần/file trên 2 file, không nơi nào khác định nghĩa Y2): 5/5 test PASS — S1/S1M không còn vào danh sách cần tra cứu chéo (tự đủ); Y2 vẫn đúng là MISSING thật (không nơi nào định nghĩa, khớp đúng quy tắc ≥2 lần).

### Trạng thái

- Cơ chế Detail classification giờ hoạt động ở CẤP ĐỘ TỪNG DETAIL ID thay vì cả file — 1 file có thể VỪA tự định nghĩa 1 số detail (tiêu chuẩn, lặp lại ≥2 lần) VỪA tham chiếu detail khác (chỉ xuất hiện 1 lần, cần tra cứu chéo) trong CÙNG lúc, khớp đúng thực tế bản vẽ.
- Sheet DETAIL trong báo cáo giờ CHỈ còn liệt kê các Detail ID THẬT SỰ cần tra cứu chéo (xuất hiện đúng 1 lần trong file chứa nó) — các detail tiêu chuẩn tự đủ sẽ không còn tạo dòng kiểm tra thừa nữa (giảm nhiễu, chỉ báo đúng những trường hợp cần chú ý thật).

### Bước tiếp theo

- NETLOAD bản build mới, bấm CHECK DETAILS trên đúng bộ 6 file đã phân tích — xác nhận S1/S1M không còn xuất hiện trong sheet DETAIL (tự đủ), Y2 vẫn đúng MISSING nếu thật sự không có bản vẽ nào khác định nghĩa nó.
- Nếu sau này phát hiện detail tiêu chuẩn nào đó CHỈ xuất hiện đúng 1 lần trong 1 file (vd chỉ có DBText, không có MText ghi chú kèm theo) nhưng vẫn là loại "tự vẽ tại chỗ" — quy tắc "≥2 lần" sẽ không bắt được, cần xem xét lại ngưỡng hoặc bổ sung tín hiệu khác.

## Session 2026-07-08 00:45

### Đã làm

- User yêu cầu ngắn gọn "Detail Management: Gộp 4 file thành 1 file report" — không rõ ngay "Detail Management" là gì trong project này, đã dùng Explore agent tra cứu: đây là nhãn mô tả (không phải class/namespace) cho tính năng nút **CHECK DETAILS** hiện có (`Views/MainPaletteControl.xaml.cs:26` `BtnCheckDetails_Click`) — quét thư mục DWG, đối chiếu Detail giữa Top Plate và Detail Sheet, xuất báo cáo. Không liên quan tới BOM/Assembly List đang làm các session trước.
- Đọc `Utilities/FileExporter.cs` (cũ): tính năng này xuất **4 file CSV riêng** — `_DETAIL.csv`, `_REVERSE.csv`, `_SECTION.csv` (cả 3 từ `ExportReport()`), và `_DATALOG.csv` (từ `ExportDataLog()`, gọi riêng ở `MainPaletteControl.xaml.cs:64`).
- Xác nhận qua AskUserQuestion (2 vòng, vì yêu cầu ban đầu quá ngắn): (1) 4 file nguồn = 4 CSV từ chính chức năng Check Details; (2) thay HẲN nút CHECK DETAILS bằng xuất 1 file `.xlsx` duy nhất (không giữ CSV song song).
- Viết lại hoàn toàn `Utilities/FileExporter.cs`: bỏ `ExportReport`/`ExportDataLog` (CSV), thay bằng `ExportReportExcel(scan, outputPath)` — dùng LẠI `MinimalXlsxWriter` (đã có sẵn, viết tay OOXML, không dùng ClosedXML — tránh đúng lỗi xung đột SixLabors.Fonts trong AutoCAD đã gặp ở phần BOM) để xuất 1 file `.xlsx` với 4 sheet tên **DETAIL / REVERSE / SECTION / DATALOG** (đặt tên theo đúng hậu tố của 4 file CSV cũ, đúng yêu cầu user). Logic nghiệp vụ (tra cứu `DetailDefinitions`, gộp `refLookup` cho Reverse Check...) giữ NGUYÊN 100%, chỉ đổi định dạng ghi ra — riêng phần `EscapeCsvField` (escape dấu phẩy/ngoặc kép cho CSV) được BỎ vì không cần thiết với Excel (MinimalXlsxWriter tự escape XML).
- Sửa `Views/MainPaletteControl.xaml.cs` (`BtnCheckDetails_Click`): đổi `SaveFileDialog` filter từ `*.csv` sang `*.xlsx`, gọi `FileExporter.ExportReportExcel(scan, reportPath)` thay vì 2 lệnh CSV cũ, rút gọn thông báo hoàn tất còn 1 dòng tên file (không còn liệt kê 4 file).
- Build 0 lỗi (0 caller nào khác của `ExportReport`/`ExportDataLog` bị bỏ sót — xác nhận qua build sạch, không cần giữ lại code chết).
- `FileExporter.cs` KHÔNG phụ thuộc AutoCAD API (chỉ dùng `Models.ScanResult` thuần + `MinimalXlsxWriter`) nên test được ngoài AutoCAD: tạo scratch project `reporttest` (net8 + OpenXml SDK), dữ liệu mẫu phủ đủ nhánh (Detail OK/MISSING, Reverse OK/UNREFERENCED, DataLog có dấu phẩy+ngoặc kép để xác nhận không còn artifact escape CSV) — kết quả: cả 4 sheet đúng tên, đúng dữ liệu, đúng logic nghiệp vụ gốc, 0 lỗi OpenXml validation.

### Trạng thái

- Nút CHECK DETAILS giờ xuất 1 file Excel duy nhất (4 sheet) thay vì 4 file CSV rời rạc — đúng yêu cầu "gộp 4 file thành 1 file report".
- Đây là thay đổi cho tính năng KHÁC (Check Details / Detail Management) — hoàn toàn tách biệt với luồng BOM/Assembly List đã xây các session trước, không ảnh hưởng lẫn nhau.

### Bước tiếp theo

- NETLOAD bản build mới, bấm CHECK DETAILS trên 1 bộ DWG thật, xác nhận file `.xlsx` xuất ra đúng 4 sheet với dữ liệu đúng như 4 file CSV trước đây.

## Session 2026-07-08 00:10

### Đã làm

- User đưa ví dụ LOCATOR (bản vẽ 4010): dòng gốc trong ExportedData có `DRW=4010` **trùng** `DRAWING NO=4010` (tự tham chiếu) và ĐÃ có Material="Q345D" đầy đủ, nhưng vẫn không sang được Assembly List — kể cả 3 bản vẽ khác (1215/1225/1235) tham chiếu chéo sang 4010 cũng rơi vào N/A theo.
- **Root cause**: `SteelSpecClassifier.Classify` (đã sửa 2 session trước để dùng tín hiệu "Drw có dạng số hiệu bản vẽ thật hay không") chỉ nhìn vào GIÁ TRỊ của Drw, không biết dòng đó đang nằm TRONG bản vẽ nào — nên "Drw=4010" bị coi là tham chiếu sang bản vẽ khác (đúng dạng số), dù thực chất đó là CHÍNH bản vẽ chứa dòng này (tự tham chiếu, giống pattern "SELF LABEL ROW" đã xử lý ở đồ thị tham chiếu 3 session trước — nhưng lần đó CHỈ sửa `BuildReferenceGraph`, chưa sửa `SteelSpecClassifier`). Hệ quả dây chuyền: dòng gốc 4010 không phân loại được → `drawingColumnSums["4010"]` rỗng → 1215/1225/1235 tra cứu chéo sang 4010 cũng ra rỗng theo, dù dữ liệu ExportedData đã đầy đủ.
- Sửa `Utilities/SteelSpecClassifier.cs`: `Classify` giờ tính thêm `ownDrawing = BomIdentity.ExtractDrawingNumber(item.SourceFile)` (đã có sẵn trong `BomItem.SourceFile`, không cần thêm tham số/context mới) và so sánh với `item.Drw` — chỉ loại khi Drw THẬT SỰ khác bản vẽ chứa nó; tự tham chiếu (Drw == chính bản vẽ đó) không còn bị loại.
- Build 0 lỗi. Verify bằng scratch project (mô phỏng đúng LOCATOR: bản vẽ 4010 tự tham chiếu Material="Q345D" Weight=84.9, cộng 2 bản vẽ khác tham chiếu chéo Qty=2 và Qty=3): LOCATOR (4010) giờ phân loại đúng vào cột "Q345D"; 2 bản vẽ tham chiếu chéo tính đúng = 84.9×2=169.8 và 84.9×3=254.7 (khớp tay tuyệt đối). 0 lỗi OpenXml validation.

### Trạng thái

- Cơ chế "tự tham chiếu" giờ nhất quán ở CẢ 2 nơi: đồ thị tham chiếu (`BuildReferenceGraph`, fix 3 session trước — tránh vòng lặp giả) VÀ bộ phân loại vật liệu (`SteelSpecClassifier`, fix session này — không chặn nhầm phân loại). Trước đây chỉ fix 1 nửa nên vẫn còn lỗi ẩn cho các bản vẽ có dòng tự tham chiếu MÀ CŨNG có Material đầy đủ (như LOCATOR).

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật — xác nhận LOCATOR (bản vẽ 4010) và các item tương tự (dòng tự tham chiếu có Material đầy đủ) giờ lên đúng cột vật liệu, không còn N/A; các bản vẽ tham chiếu chéo tới chúng cũng tính đúng.

## Session 2026-07-07 23:40

### Đã làm

- User mô tả 1 pattern mới: 1 số bản vẽ có 2 block `SW_TABLEANNOTATION_*` — 1 block là bảng BOM thật (1 item, đại diện chính bản vẽ đó), block còn lại KHÔNG phải bảng mà là ghi chú tự do dạng "Material: Q345D or equivalent" chứa Material của item ở block kia.
- **Phân tích**: đây rất có thể chính là nguyên nhân thật của cảnh báo "Không xác định được hàng header" đã gặp ở session trước (5 file 1502-1506) — block ghi chú Material không có cấu trúc bảng (không ITEM/QTY/...) nên `BomTableParser` luôn thất bại khi cố tìm header, sinh cảnh báo SAI (block đó chưa từng có ý định là bảng). Hệ quả: item duy nhất của bản vẽ thiếu Material, không bao giờ phân loại được vào cột Grade+Thickness nào.
- Xác nhận qua AskUserQuestion: (1) định dạng "Material: X" luôn có, "or equivalent" có thể có/không; (2) chỉ áp dụng khi file có ĐÚNG 1 item (an toàn nhất, đúng mô tả của user).
- Sửa `Utilities/BomTableParser.cs`: thêm `MaterialNoteRegex` (`MATERIAL\s*:\s*(.+?)(?:\s+or\s+equivalent\.?)?\s*$`, case-insensitive, lazy + `$` tự tìm đúng ranh giới khi có/không hậu tố "or equivalent"). Thêm `TryParseMaterialNote(btr, tr, out material)` (đọc MTEXT trong block, ghép lại, khớp regex) và tách riêng phần logic thuần `TryExtractMaterialNote(List<string>, out material)` — KHÔNG phụ thuộc AutoCAD (để kiểm thử độc lập ngoài AutoCAD được, vì `BlockTableRecord`/`MText` chỉ tồn tại trong tiến trình AutoCAD).
- Sửa `Utilities/BomFileScanner.cs`: gom item của TỪNG block vào `fileItems` thay vì add thẳng vào `result.Items` ngay trong vòng lặp (cần gom đủ hết mới biết file có đúng 1 item hay không). Với block KHÔNG parse ra bảng (0 item), thử `TryParseMaterialNote` TRƯỚC khi coi là lỗi — nếu khớp, lưu lại giá trị Material, KHÔNG ghi cảnh báo cho block đó (hành vi bình thường). Sau khi duyệt hết block: nếu `fileItems.Count == 1` và item đó đang thiếu Material và có tìm được ghi chú Material → điền vào; rồi mới gán `SourceFile` + add vào `result.Items`.
- Build 0 lỗi. `BomTableParser.cs`/`BomFileScanner.cs` dùng AutoCAD API nên KHÔNG build/test được trong scratch .NET 8 project — đã tách riêng phần logic thuần (`TryExtractMaterialNote`) ra 1 scratch project độc lập khác (`regextest`, không có dependency AutoCAD) để kiểm thử được: 9/9 test case PASS (có/không "or equivalent", chữ hoa/thường, nhiều khoảng trắng, ghi chú tách thành 2 MTEXT riêng label+value, cụm "or" KHÔNG phải "or equivalent" giữ nguyên cả cụm, không khớp khi không có "Material:", danh sách rỗng, có dấu chấm cuối).

### Trạng thái

- Phần orchestration trong `BomFileScanner.cs` (gom fileItems, quyết định điền Material khi nào) đã review kỹ bằng mắt nhưng CHƯA chạy thử thật trong AutoCAD (cần AutoCAD process, không thể test tự động ngoài đó) — cần NETLOAD + quét thật để xác nhận cuối cùng.
- Cảnh báo "Không xác định được hàng header" cho các file có đúng pattern này (bảng BOM 1-item + ghi chú Material riêng) sẽ tự động biến mất, đồng thời item đó giờ có Material đầy đủ để phân loại vào đúng cột Grade+Thickness (hoặc cột gộp theo Material nếu Size không phải dạng kích thước, theo cơ chế đã xây 2 session trước).

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật trên các file 1502-1506 (và bất kỳ file nào từng báo lỗi header) — xác nhận: (a) cảnh báo "Không xác định được hàng header" biến mất nếu đúng là do pattern ghi chú Material; (b) item của các bản vẽ đó giờ có Material đúng và lên đúng cột trong Assembly List.
- Nếu vẫn còn cảnh báo header cho file nào đó sau khi fix này — nghĩa là nguyên nhân KHÔNG phải pattern ghi chú Material, cần đọc nội dung "hàng khớp nhiều từ khóa nhất" (đã thêm ở cảnh báo từ session trước) để chẩn đoán tiếp.

## Session 2026-07-07 23:00

### Đã làm

- User hỏi tiếp: tại sao FLEXIPAD (Material="Rubber", đã tự phân loại "K105955" không phải bản vẽ thật giống fix trước) vẫn không lên cột riêng trong Assembly List, vẫn rơi vào N/A.
- **Phân tích root cause**: `SteelSpecClassifier.Classify` cần CẢ 2 điều kiện — (1) Drw không phải số hiệu bản vẽ thật (FLEXIPAD đã PASS từ session trước), (2) Size phải "giống kích thước phôi" (số đơn hoặc chứa "x"). FLEXIPAD có `Size="TYPE LL 450/85"` — đây là MÃ KIỂU LOẠI catalog, không chứa "x" → FAIL điều kiện 2.
- **Cảnh báo quan trọng đã nêu cho user TRƯỚC khi sửa**: nếu chỉ đơn giản nới điều kiện 2 để chấp nhận dạng "số/số" rồi vẫn lấy "số nhỏ nhất" làm thickness như cũ, sẽ tạo BUG NẶNG HƠN — "TYPE LL 450/85", "600/85", "800/85" đều có số nhỏ nhất là "85" → 3 loại FLEXIPAD khác nhau (18kg/6kg/30kg) sẽ bị CỘNG NHẦM vào chung 1 cột "Rubber|85".
- User chọn: thêm cột "Rubber" (gộp theo TÊN VẬT LIỆU, không cần tách theo Type/Size).
- Sửa `Utilities/SteelSpecClassifier.cs`: khi Size KHÔNG có dạng kích thước phôi nhưng Material đã biết rõ, VẪN phân loại — Thickness để RỖNG (`""`) thay vì trả về null. Nhờ vậy các item có Size dạng mã kiểu loại (không phải kích thước) vẫn gộp đúng theo Material (khóa `"Rubber|"`), không cố đoán thickness từ những con số không mang ý nghĩa kích thước.
- **Không cần sửa gì thêm ở `BomTemplateExporter.cs`** — kiến trúc `specForItem`/`columnIndex` đã tổng quát hóa từ session trước (fix NAME PLATE) nên tự động áp dụng đúng cho case này, kể cả closure nhiều cấp (khi 1 bản vẽ khác tham chiếu tới bản vẽ có FLEXIPAD, hệ số Rubber cũng tự cuốn theo đúng).
- Build 0 lỗi. Verify bằng scratch project (3 loại FLEXIPAD khác nhau across nhiều bản vẽ, kể cả 1 bản vẽ tham chiếu chéo tới bản vẽ có FLEXIPAD): cột "Rubber" xuất hiện đúng, gộp đúng tổng theo từng bản vẽ (18+6=24 cho 1216, đúng con số tay tính), closure cross-reference tự động cuốn theo đúng (FITTING ARRANGEMENT ở 1210 tham chiếu 1216 tính ra 24 Rubber, khớp tay). Hiệu ứng phụ nhất quán: "Steel" cũng thành 1 cột riêng cho TWISTLOCK FOUNDATION/FOUNDATION CONTAINER (Material="Steel" chung chung, cùng cơ chế). 0 lỗi OpenXml validation.

### Trạng thái

- `SteelSpecClassifier` giờ phân loại được MỌI item có Material rõ ràng, bất kể Size có đúng dạng kích thước phôi hay không — Size dạng kích thước thật (có "x"/số đơn) tạo cột "Material | Thickness" cụ thể; Size dạng mã kiểu loại/mô tả tạo cột "Material" gộp chung (Thickness rỗng), tránh đụng độ giữa các loại khác nhau vô tình có cùng 1 con số.
- Cột "MATERIAL (N/A)" giờ CHỈ còn chứa item hoàn toàn không rõ Material (vd STEEL STRUCTURE tham chiếu bản vẽ chưa quét, không biết cả weight lẫn material) — đúng đúng nghĩa nguyên gốc.

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật: xác nhận cột "Rubber" (và có thể cả "Steel" nếu có TWISTLOCK FOUNDATION/FOUNDATION CONTAINER tương tự) xuất hiện đúng trong Assembly List, tổng số khớp với dữ liệu ExportedData.

## Session 2026-07-07 22:30

### Đã làm

- User hỏi tại sao NAME PLATE (ExportedData: Drw="K101791", Material="MTL 57", Size="235x95", Weight=1.1) không được đưa vào cột vật liệu nào trong Assembly List (chỉ rơi vào N/A).
- **Root cause**: `Utilities/SteelSpecClassifier.cs:38` (cũ) — điều kiện `if (!string.IsNullOrWhiteSpace(item.Drw)) return null;` coi BẤT KỲ Drw không rỗng nào cũng là "có tham chiếu bản vẽ khác, không phải vật tư thô", nên loại NAME PLATE ngay từ đầu dù Material/Size đầy đủ. Nhưng "K101791" **không phải số hiệu bản vẽ thật** — là MÃ CATALOG/nhà cung cấp (giống K103058, K105955 đã gặp trước) — khác bản chất với tham chiếu thật như "4202" (luôn bắt đầu bằng chữ số). Rule cũ gộp nhầm 2 khái niệm khác nhau.
- Sửa `SteelSpecClassifier.cs`: đổi tín hiệu từ "Drw rỗng" → "Drw KHÔNG có hình dạng số hiệu bản vẽ thật" (`LooksLikeDrawingReference`: rỗng thì false, có giá trị thì kiểm tra bắt đầu bằng chữ số hay không qua regex `^\s*\d`) — vẫn là tín hiệu CẤU TRÚC, không hardcode danh sách mã catalog, đúng triết lý sẵn có của file.
- Sửa `Utilities/BomTemplateExporter.cs` — `BuildAssemblyListSheet`: thêm tham số `columnIndex`/`specForItem` (trước đây chỉ `BuildMaterialListSheet` có). Thứ tự xử lý mỗi item giờ: (1) thử tra cứu chéo nếu Drw trỏ bản vẽ ĐÃ quét (không đổi); (2) nếu KHÔNG, kiểm tra `specForItem` — nếu tự phân loại được (như NAME PLATE) thì ghi thẳng công thức vào ĐÚNG cột Grade+Thickness (tham chiếu trực tiếp ExportedData × hệ số Qty section, không qua N/A); (3) chỉ khi CẢ 2 đều thất bại mới rơi vào N/A (công thức nếu biết weight, chữ "N/A" nếu không biết gì). Cảnh báo "bản vẽ chưa quét" CHỈ ghi khi item KHÔNG tự phân loại được (item tự phân loại được thì không còn thật sự "thiếu dữ liệu").
- Build 0 lỗi. Verify bằng scratch project (dọn bỏ 1 bộ dữ liệu test cũ bị trùng lặp gây nhiễu kết quả): NAME PLATE giờ tạo cột "MTL 57" mới (thickness=95, đúng số nhỏ nhất trong "235x95"), công thức đúng dạng `ExportedData!I{row}*sectionQtyRef` = 1.1, KHÔNG còn ở cột N/A; cảnh báo "tham chiếu K101791" biến mất. Các trường hợp N/A hợp lệ khác (FITTING ARRANGEMENT tham chiếu bản vẽ ĐÃ quét nhưng không có material) không bị ảnh hưởng, vẫn đúng như cũ. 0 lỗi OpenXml validation.

### Trạng thái

- Cơ chế phân loại giờ nhất quán giữa Material List và Assembly List: bất kỳ item nào (dù nằm trong bản vẽ 1*-prefixed) có Material/Size đủ rõ và Drw không phải tham chiếu bản vẽ thật đều tự động vào đúng cột Grade+Thickness, không cần Drw phải rỗng tuyệt đối.
- Cột "MATERIAL (N/A)" giờ CHỈ còn chứa đúng nghĩa "thật sự không biết vật liệu gì" — không còn lẫn các item catalog đã biết rõ material.

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật, xác nhận NAME PLATE và các item catalog tương tự (có Material rõ ràng, Drw dạng "K...") giờ xuất hiện đúng cột vật liệu riêng thay vì dồn vào N/A.

## Session 2026-07-07 22:00

### Đã làm

- User yêu cầu 2 việc cho sheet Assembly List: (1) bỏ hẳn cột TOTAL/KG; (2) tô nổi bật dòng tổng cuối (footer).
- Sửa `Utilities/MinimalXlsxWriter.cs`: thêm `GridRow.FooterStyle` (bool) + 2 style mới `STYLE_FOOTER=7` (nền xám đậm #D9D9D9 + chữ đậm) và `STYLE_FOOTER_CENTER=8` (như trên + căn giữa, dùng cho ô SỐ) — cùng cơ chế 2 chiều (row-style × numeric-hay-không) đã xây session trước. Thứ tự ưu tiên style hàng: HeaderStyle > FooterStyle > Bold > Highlight > Default.
- Sửa `Utilities/BomTemplateExporter.cs`:
  - `AddHeaderRows`: đổi `totalColIdx` từ bắt buộc sang `int?` tùy chọn (Material List vẫn truyền có giá trị — giữ nguyên cột TOTAL/KG; Assembly List truyền `null`).
  - `BuildAssemblyListSheet`: bỏ hẳn biến `totalColIdx` — cột "MATERIAL (N/A)" (`naColIdx`) giờ là cột CUỐI CÙNG. Xóa toàn bộ logic tính TOTAL/KG riêng từng dòng (SUM ngang các cột vật liệu của 1 item) VÀ TOTAL/KG gộp ở dòng tổng cuối (SUM tất cả cột vật liệu thành 1 số) — mỗi cột vật liệu (Grade+Thickness + N/A) giờ ĐỨNG ĐỘC LẬP, không còn cộng dồn thành 1 con số chung (cộng khối lượng NHIỀU LOẠI vật liệu khác nhau lại không có ý nghĩa thực tế cho việc đặt mua). Dòng tổng cuối đổi `Bold=true` → `FooterStyle=true`.
  - `BuildColumnWidths`: thêm tham số `includeTotalColumn` (mặc định `true` — Material List không đổi) để Assembly List có thể bỏ luôn độ rộng cột thừa.
  - `FullBorderColumnCount` của Assembly List đổi từ `totalColIdx` sang `naColIdx` (cột cuối thật sự bây giờ).
- Build 0 lỗi. Verify bằng scratch project + OpenXml SDK: header row Assembly List kết thúc đúng ở cột "MATERIAL (N/A)" (không còn "TOTAL/KG" sau đó); dòng tổng cuối sheet — ô text (nhãn "TOTAL/KG" ở cột B, đại diện label dòng) style index 7 (nền xám), ô số (từng cột Grade+Thickness/N/A) style index 8 (xám + căn giữa); mỗi cột tổng vẫn tính ĐÚNG số cũ (60/40/15/132.5) chỉ khác là KHÔNG còn cộng gộp thành 1 số duy nhất nữa. 0 lỗi OpenXml validation.

### Trạng thái

- Assembly List giờ hiển thị TỪNG loại vật liệu riêng biệt ở dòng tổng cuối (không gộp nhầm các grade thép khác nhau lại với nhau), dòng tổng có màu nền xám nổi bật dễ nhận ra ngay khi cuộn tới cuối bảng.
- Material List KHÔNG bị ảnh hưởng — vẫn giữ nguyên cột TOTAL/KG như cũ (không nằm trong phạm vi yêu cầu lần này).

### Bước tiếp theo

- NETLOAD bản build mới, mở file thật kiểm tra trực quan: cột TOTAL/KG đã biến mất khỏi Assembly List, dòng tổng cuối có nền xám nổi bật.

## Session 2026-07-07 21:30

### Đã làm

- User yêu cầu 2 việc: (1) chỉ để lộ 2 sheet Summary + Assembly List, ẩn các sheet còn lại; (2) format lại bảng cho user-friendly hơn.
- **Ẩn sheet**: thêm `SheetData.Hidden` (bool) vào `Utilities/MinimalXlsxWriter.cs` — `BuildWorkbook` ghi `state="hidden"` vào `<sheet>` tương ứng, đồng thời thêm `<bookViews><workbookView activeTab="X"/></bookViews>` trỏ tới sheet KHÔNG ẩn đầu tiên (mặc định Excel mở tab đầu tiên trong danh sách, mà ExportedData giờ bị ẩn nên phải chỉ định rõ). Lưu ý: `bookViews` PHẢI đứng TRƯỚC `sheets` theo thứ tự schema OOXML (CT_Workbook) — ban đầu đặt sai chỗ, OpenXml validator báo lỗi "unexpected child element bookViews", đã sửa đúng thứ tự. `Utilities/BomExcelExporter.cs`: đánh dấu `Hidden=true` cho ExportedData, Material List, Warnings — giữ Summary + Assembly List hiển thị.
- **Format bảng**: hỏi user qua AskUserQuestion (multi-select) — chọn: (a) tô màu header nổi bật, (b) căn giữa số/căn trái text, (c) ẩn gridline mặc định Excel. KHÔNG chọn định dạng số (giữ nguyên "0.####" hiện tại).
  - Thêm 7 style (`STYLE_DEFAULT=0, BOLD=1, HIGHLIGHT=2, HEADER=3, CENTER=4, BOLD_CENTER=5, HIGHLIGHT_CENTER=6`) trong `BuildStyles()` — HEADER = nền xanh đậm (#1F4E78) + chữ trắng đậm + căn giữa; 3 style *_CENTER còn lại = bản căn giữa của DEFAULT/BOLD/HIGHLIGHT (dùng cho Ô SỐ trong hàng tương ứng, giữ nguyên nền/chữ đậm của hàng).
  - Thêm `GridRow.HeaderStyle` (bool) — TÁCH BIỆT với `Bold` (Bold còn dùng cho title block/dòng TOTAL/section header, không phải lúc nào cũng nên tô xanh) — chỉ áp dụng cho hàng tiêu đề CỘT thật sự (DESCRIPTION/QTY/... ở Summary; Drawing/Name/ITEM/... ở Material List/Assembly List).
  - `BuildGridSheet`/`BuildSheet`: style của mỗi Ô giờ tính theo 2 chiều — style GỐC của hàng (Header/Bold/Highlight/Default) × Ô có phải SỐ hay không (Formulas/NumberCells → dùng biến thể `_CENTER` qua hàm `CenteredVariant()`; Cells/TextFormulas text → giữ nguyên, không center).
  - `showGridLines="0"` ghi LUÔN vào `<sheetView>` (trước đây chỉ ghi `<sheetViews>` khi có FreezeHeaderRow — nay tách riêng, luôn có sheetView để tắt gridline, freeze chỉ là `<pane>` con tùy chọn bên trong).
  - `Utilities/BomSummaryExporter.cs`: đổi hàng header từ `Bold=true` sang `HeaderStyle=true`. `Utilities/BomTemplateExporter.cs`: mở rộng delegate `addRow` từ 3 tham số (cells,bold,highlight) sang 4 (thêm headerStyle); `AddHeaderRows` (dùng chung Material List + Assembly List) đổi header1/header2 sang `HeaderStyle=true` thay vì chỉ Bold.
- Build 0 lỗi. Verify bằng scratch project + OpenXml SDK: sheet visibility đúng (`ExportedData/Material List/Warnings=hidden`, `Summary/Assembly List=visible`, `activeTab=1` trỏ đúng Summary); style header = index 3 cho MỌI ô header (kể cả hàng thickness sub-header); ô số (TOTAL QTY/WEIGHT/OCCURRENCES ở Summary, Qty/N/A-formula/TOTAL-KG ở Assembly List) đúng index căn giữa tương ứng theo NGỮ CẢNH hàng (default→4, highlight→6); ô text giữ index cũ (0/1/2, không center); `showGridLines=False` xác nhận qua `SheetView.ShowGridLines`. 0 lỗi OpenXml validation.

### Trạng thái

- File xuất ra giờ chỉ hiện 2 tab (Summary, Assembly List) khi mở — các sheet còn lại vẫn đầy đủ công thức, chỉ ẩn tab, user có thể tự Unhide khi cần chẩn đoán (vd xem Warnings).
- Bảng có header nổi bật (xanh đậm/chữ trắng), số liệu căn giữa nhất quán theo NGỮ CẢNH hàng (không phá vỡ tô vàng/đậm sẵn có), không còn gridline mặc định gây rối mắt.

### Bước tiếp theo

- NETLOAD bản build mới, mở file thật trong Excel để xác nhận cảm nhận trực quan (màu sắc, căn giữa, ẩn gridline) đúng như mong đợi — quan trọng vì OpenXml SDK chỉ xác nhận đúng CẤU TRÚC, không thay thế việc nhìn bằng mắt.
- Nếu sau này muốn thêm định dạng số (dấu phẩy ngăn cách hàng nghìn, cố định số chữ số thập phân) — user đã CHỦ ĐỘNG KHÔNG chọn mục này lần này, cần hỏi lại rõ ràng nếu muốn làm sau.

## Session 2026-07-07 20:45

### Đã làm

- User chỉ ra thiết kế "MATERIAL (N/A)" ở session trước CHƯA đúng: ví dụ NAME PLATE (bản vẽ 1210, Drw→K101791) đã có sẵn khối lượng WEIGHT=1.1 trong ExportedData, nhưng cột N/A đang ghi chữ "N/A" (text) thay vì hiện SỐ 1.1 — trong khi TOTAL/KG lại đúng (1.1). Yêu cầu: cột N/A phải ưu tiên hiện SỐ khi đã biết khối lượng, chỉ ghi "N/A" khi thật sự không biết gì cả (như FITTING ARRANGEMENT → 1216/1219: cả Material lẫn Weight đều rỗng).
- **Thiết kế lại theo hướng thống nhất**: coi cột "MATERIAL (N/A)" là 1 cột vật liệu BỔ SUNG, tham gia CÙNG cơ chế tổng hợp (`termsPerColumn`/`cachedPerColumn`, giờ có kích thước `dynCount + 1`, slot cuối = cột N/A) như các cột Grade+Thickness thật — không còn là 1 nhánh xử lý riêng (`fallbackTotalCellRefs`) như trước.
  - Item KHÔNG tra cứu chéo được: nếu `item.Weight` khác 0 → ghi CÔNG THỨC (`=ExportedData!I{row}*sectionQtyRef`) vào cột N/A (số thật, tính vào SUM); nếu Weight CŨNG rỗng/0 → ghi text "N/A" (không phải Formula, tự động KHÔNG bị tính vào SUM vì cơ chế tổng hợp chỉ đọc `Formulas` dict, không đọc `Cells` text).
  - TOTAL/KG riêng từng dòng: LUÔN = `SUM(cột Grade+Thickness đầu tiên : cột N/A)` — dải SUM mở rộng thêm 1 cột để gồm cả N/A, áp dụng ĐỒNG NHẤT cho mọi item (không cần tách nhánh `if (crossReferenced) {...} else {...}` như trước).
  - Dòng tổng cuối sheet: vòng lặp cộng theo cột mở rộng `for i in 0..dynCount` (gồm cả N/A), dải SUM ở dòng tổng cũng mở rộng tới cột N/A — code đơn giản hơn hẳn bản trước (bỏ hẳn danh sách `fallbackTotalCellRefs`/`fallbackCachedTotal` riêng).
- Sửa `Utilities/BomTemplateExporter.cs`: viết lại phần thân `BuildAssemblyListSheet` theo thiết kế trên (per-item logic + gom theo section + dòng tổng cuối). Build 0 lỗi.
- Verify bằng scratch project với đúng kịch bản user nêu (1210 chứa FITTING ARRANGEMENT→1216, FITTING ARRANGEMENT→1219 — cả 2 bản vẽ ĐÃ quét nhưng closure rỗng thật sự không biết khối lượng — và NAME PLATE→K101791 weight=1.1):
  - `J14/J15 = "N/A"` (text, không phải công thức), `K14/K15` để trống — đúng như FITTING ARRANGEMENT trong ví dụ user.
  - `J16 = [ExportedData!I10*$E$13] = 1.1`, `K16 = [SUM(G16:J16)] = 1.1` — ĐÚNG như yêu cầu, không còn ghi "N/A" cho NAME PLATE.
  - Dòng tổng cuối: `J41 = SUM(J14:J16)+SUM(J28:J33) = 132.5` (chỉ cộng các ô N/A dạng CÔNG THỨC, tự động bỏ qua các ô N/A dạng text) — `K41 = SUM(G41:J41) = 247.5`.
  - 0 lỗi OpenXml validation.

### Trạng thái

- Cột "MATERIAL (N/A)" giờ hoạt động đúng như 1 "cột dự phòng chứa khối lượng chưa phân loại được Grade+Thickness cụ thể" — không phải chỉ là 1 nhãn cảnh báo. Chữ "N/A" (text) chỉ xuất hiện khi THẬT SỰ không có bất kỳ thông tin khối lượng nào — đúng ý nghĩa "cần user tự điền tay".
- Code gọn hơn bản trước (loại bỏ nhánh xử lý N/A riêng biệt, thống nhất vào cùng 1 cơ chế cột).

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật trên toàn bộ file, xác nhận: NAME PLATE (K101791) và các item catalog có sẵn WEIGHT hiện đúng số ở cả N/A lẫn TOTAL/KG; các item thật sự không rõ gì (như FITTING ARRANGEMENT→1216/1219 nếu bản vẽ đích cũng rỗng) vẫn đúng là "N/A" text.

## Session 2026-07-07 20:15

### Đã làm

- User dán 10 dòng ExportedData thật của bản vẽ 1226 (Assembly List), hỏi tại sao KHÔNG dòng nào hiện material breakdown. Phân tích: chia 2 nhóm nguyên nhân —
  1. **Nhóm A** (STEEL STRUCTURE→2151, VENTILATOR→K103058, FLEXIPAD→K105955 ×2): có Drw nhưng trỏ bản vẽ catalog/chưa quét — tra cứu chéo hiện tại KHÔNG có phương án dự phòng, nên dù VENTILATOR/FLEXIPAD đã có sẵn WEIGHT ngay trên dòng (80.6, 6, 30 — SolidWorks tự điền cho hàng catalog) vẫn bị bỏ phí hoàn toàn.
  2. **Nhóm B** (TWISTLOCK FOUNDATION, FOUNDATION CONTAINER ×5): Drw rỗng, Material="Steel" nhưng Size ("LIFTING SOCKET", "SINGLE H=110"...) không giống kích thước phôi nên `SteelSpecClassifier` không phân loại được — code hiện tại (`if (targetDrawing.Length > 0)`) thậm chí không chạy vào nhánh nào cho các dòng Drw rỗng, nên hoàn toàn vô hình (~63kg bị bỏ sót).
- Thống nhất qua nhiều vòng AskUserQuestion (+ mockup preview xác nhận): thêm 1 nhóm xử lý mới ("Nhóm C") — KHÔNG tách sheet/section riêng, vẫn nằm NGAY TẠI dòng/cột tương ứng trong chính Assembly List:
  - Thêm 1 cột mới **"MATERIAL (N/A)"** (sau các cột Grade+Thickness, trước TOTAL/KG): ghi chữ "N/A" cho item mà Drw có giá trị nhưng KHÔNG tra cứu chéo được (dù Material đã có giá trị chung chung như "Rubber" — vẫn N/A vì đây là hàng cần xác nhận/đặt mua, không phải NVL đã biết rõ). Để TRỐNG (không N/A) nếu Drw rỗng và CÓ Material (vd "Steel" của cụm hàn) — coi như đã biết đủ.
  - Thêm **TOTAL/KG riêng từng dòng** (trước đây Assembly List chỉ có TOTAL/KG ở dòng tổng cuối, không có per-item): item tra cứu chéo được → `SUM` các cột Grade+Thickness của CHÍNH dòng đó; item KHÔNG tra cứu chéo được nhưng biết khối lượng riêng (Weight ≠ 0) → công thức dự phòng `=ExportedData!I{row}*sectionQtyRef` (LIVE, nhân hệ số Qty bản vẽ, KHÔNG nhân thêm Qty của chính dòng — theo đúng quy ước Weight đã là tổng cả dòng, giống Material List).
  - Dòng TOTAL/KG cuối sheet: cộng thêm các ô TOTAL/KG dự phòng này (nối thêm số hạng "+", không đổi cách tính cột Grade+Thickness hiện có) để không còn bỏ sót khối lượng.
- Sửa `Utilities/BomTemplateExporter.cs`: `AddHeaderRows` thêm tham số `naColIdx` tùy chọn; `BuildColumnWidths` thêm tham số `includeNaColumn`; `BuildAssemblyListSheet` — thêm biến `naColIdx`/`totalColIdx` dịch thêm 1 cột, track `crossReferenced` cho mỗi item, nhánh N/A + TOTAL/KG dự phòng khi không cross-ref được, nhánh SUM riêng dòng khi cross-ref được, gom `fallbackTotalCellRefs`/`fallbackCachedTotal` cộng vào dòng tổng cuối. Material List KHÔNG đổi (vẫn dùng `AddHeaderRows`/`BuildColumnWidths` không truyền tham số mới, giữ nguyên layout cũ).
- **Sửa 1 lỗi logic tự phát hiện khi viết code**: lúc đầu dùng điều kiện N/A chỉ dựa vào "Material rỗng hay không", khiến FLEXIPAD (có Material="Rubber" nhưng Drw trỏ catalog) sẽ KHÔNG bị đánh dấu N/A — sai với mockup đã xác nhận. Sửa lại đúng: điều kiện N/A dựa vào "Drw CÓ giá trị" (bất kể Material), không phải "Material rỗng".
- Build 0 lỗi. Verify bằng scratch project với dữ liệu mô phỏng ĐÚNG 10 dòng thật của 1226: kết quả khớp 100% với mockup — STEEL STRUCTURE (N/A, TOTAL/KG trống vì không biết), FLEXIPAD ×2 (N/A dù có Material, TOTAL/KG=6 và 30), VENTILATOR (N/A, TOTAL/KG=80.6), TWISTLOCK FOUNDATION/FOUNDATION CONTAINER (KHÔNG N/A, TOTAL/KG=6.7/8.1) — dòng tổng cuối sheet cộng đúng thêm 131.4kg từ nhóm này (trước đây góp 0). 0 lỗi OpenXml validation.

### Trạng thái

- Assembly List giờ không còn "mất tích" khối lượng đã biết của bất kỳ item nào, dù không tra cứu chéo hay phân loại Grade+Thickness được — luôn cố gắng hiện TOTAL/KG nếu biết Weight, và đánh dấu N/A rõ ràng để user tự điền tay khi hàng cần xác nhận thêm (catalog/bản vẽ chưa quét).
- Cột "MATERIAL (N/A)" là điểm khởi đầu cho việc lọc ra danh sách "cần Purchase xác nhận/đặt mua" sau này (nếu cần thêm, có thể lọc theo AutoFilter ngay trên cột này).

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật trên toàn bộ file, xác nhận: (a) các item catalog (K-series) hiện đúng khối lượng ở TOTAL/KG + N/A ở cột mới; (b) TWISTLOCK FOUNDATION/FOUNDATION CONTAINER không bị đánh dấu N/A nhưng vẫn có TOTAL/KG; (c) dòng tổng cuối Assembly List tăng đúng so với trước.
- Nếu sau này cần lọc/liệt kê RIÊNG danh sách "N/A" thành 1 báo cáo Purchase độc lập (thay vì chỉ đánh dấu inline), có thể tái dùng cách tiếp cận Summary (gộp theo item + TOTAL QTY nhân hệ số Assembly) nhưng lọc theo điều kiện MATERIAL COLUMN KEY rỗng — chưa làm vì user chọn giữ inline trong Assembly List ở lần này.

## Session 2026-07-07 19:10

### Đã làm

- User dán tiếp nội dung sheet Warnings đầy đủ từ 1 lần export thật (bao gồm cả chuỗi vòng lặp '4010' đầy đủ nhờ fix session trước), yêu cầu phân tích. 3 nhóm phát hiện:
  1. **Vòng lặp '4010' — XÁC ĐỊNH RÕ NGUYÊN NHÂN**: cả 6 dòng cảnh báo đều kết thúc `"... -> 4010 -> 4010"` — không phải nhiều bản vẽ tham chiếu chéo nhau, mà CHÍNH bản vẽ 4010 có 1 dòng BOM tự trỏ Drw về "4010". User xác nhận (qua AskUserQuestion): đây là dòng nhãn/tổng của chính assembly đó (quy ước BOM SolidWorks tự điền số bản vẽ của mình vào cột DRW), không phải sub-assembly thật.
  2. **5 file DXF (1271071WD-1502 đến 1506_001_A.dxf) lỗi "Không xác định được hàng header"**: dùng Explore agent tra `BomTableParser.cs`/`BomFileScanner.cs` — xác nhận đây là MẤT DỮ LIỆU THẬT (không chỉ cảnh báo phụ): `BomFileScanner` chỉ ghi warning khi KHÔNG block nào trong file cho ra item nào (`totalItemsInFile == 0`), nghĩa là 5 file này đóng góp 0 dòng BOM vào toàn bộ export. Cơ chế nhận diện header hiện tại: mỗi hàng (gom theo Y, dung sai `ROW_TOLERANCE=2.0`) được đếm số từ khóa khớp (`HeaderKeywords`, so khớp `.Contains()` chữ hoa), cần ≥ `MIN_HEADER_MATCHES=4` để được công nhận là header. Nguyên nhân khả dĩ (chưa xác định chắc chắn vì không có file thật để soi): (a) header dùng từ khác không có trong danh sách keyword (vd "QUANTITY" thay vì "QTY"), hoặc (b) hàng header bị vỡ thành nhiều mảnh do lệch Y > 2.0 đơn vị.
  3. **Nhóm "thiếu bản vẽ tham chiếu"**: không có gì mới so với phân tích trước — vẫn là 2 loại (linh kiện catalog K-series, và bản vẽ 2xxx/-M chưa đưa vào batch quét).
- Sửa `Utilities/BomTemplateExporter.cs` — `BuildReferenceGraph`: bỏ qua cạnh mà item TỰ tham chiếu chính bản vẽ chứa nó (`drw.Equals(section.Drawing)`) — loại tận gốc self-reference khỏi đồ thị, không chỉ dựa vào cycle-guard bắt được nó. Build 0 lỗi. Verify bằng scratch project (thêm ca test: bản vẽ "4010" có 1 item Drw tự trỏ về "4010" + 1 bản vẽ 1*-prefixed "1400" tham chiếu "4010" qua Qty=2): KHÔNG còn cảnh báo vòng lặp giả cho ca này, TOTAL WEIGHT của BASE PLATE (bản vẽ 4010, weight gốc=20) tính đúng = 40 (20×2, đúng hệ số từ 1400) trong cả Assembly List lẫn Summary — 0 lỗi OpenXml validation.
- Sửa `Utilities/BomTableParser.cs` (diagnostic, không đổi logic parse): (1) thay chuỗi cứng `"SW_TABLEANNOTATION_1"` trong 2 message cảnh báo bằng `btr.Name` thật (bug nhỏ trước đó: message luôn ghi "_1" dù block thật là "_2","_3"...); (2) khi KHÔNG tìm được header, giờ ghi thêm vào cảnh báo: hàng khớp NHIỀU từ khóa nhất (dù chưa đủ ngưỡng 4) và toàn bộ nội dung text của hàng đó — vd `"Hàng khớp nhiều từ khóa nhất chỉ đạt 2/4, nội dung: [QUANTITY, DESCRIPTION, ...]"` — giúp user (và Claude ở phiên sau) chẩn đoán NGAY qua sheet Warnings thay vì phải mò file CAD thủ công. File này dùng AutoCAD API nên KHÔNG test được bằng scratch .NET 8 project (chỉ build-check).

### Trạng thái

- Cảnh báo vòng lặp '4010' đã hết VĨNH VIỄN (không chỉ ẩn đi mà loại đúng gốc) — không ảnh hưởng tính đúng của rollup vì self-reference chưa từng có ý nghĩa vật liệu thật.
- 5 file DXF mất dữ liệu vẫn CHƯA được fix triệt để (cần xem nội dung thật của header row lần export tiếp theo mới biết chính xác nguyên nhân) — nhưng lần export tới sẽ tự động cho biết đủ thông tin để chẩn đoán mà không cần hỏi lại.

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật: xác nhận cảnh báo '4010' đã biến mất; đọc nội dung "hàng khớp nhiều nhất" trong cảnh báo của 5 file 1502-1506 để biết chính xác nguyên nhân (từ khóa khác hay hàng bị vỡ) rồi quyết định hướng sửa `HeaderKeywords`/`ROW_TOLERANCE` phù hợp — KHÔNG nên đoán mò sửa trước khi có dữ liệu thật.
- Nếu nguyên nhân là từ khóa khác cách viết chuẩn: cân nhắc mở rộng `HeaderKeywords` (thêm từ đồng nghĩa). Nếu là do hàng bị vỡ: cân nhắc tăng `ROW_TOLERANCE` hoặc áp dụng `MergeFragmentRows`-style gộp mảnh CHO CẢ bước tìm header (hiện chỉ áp dụng cho data row).

## Session 2026-07-07 18:20

### Đã làm

- User dán nội dung sheet Warnings thật (từ 1 lần export thật) yêu cầu phân tích. Phân tích:
  1. **Vòng lặp '4010'** (2 dòng): là vòng lặp THẬT trong đồ thị tham chiếu Drw (không phải bug false-positive — đã kiểm tra lại logic `ExpandReach`/`ComputeClosure`, hình kim cương/đa đường dẫn KHÔNG bị coi nhầm là vòng lặp vì `visiting`/`path` chỉ theo dõi nhánh đệ quy hiện tại). Nguyên nhân khả dĩ: 1 dòng BOM trong bản vẽ 4010 tự trỏ Drw về "4010", HOẶC 2 file khác nhau cùng trích ra số hiệu "4010" (do regex `-(\d+)_` trong `BomIdentity.ExtractDrawingNumber`) rồi bị gộp làm 1 "bản vẽ". Hạn chế: cảnh báo cũ chỉ nêu tên bản vẽ, không nêu CHUỖI cụ thể, khó dò trong hàng trăm bản vẽ thật.
  2. **Tham chiếu tới bản vẽ không có trong danh sách quét**: chia 2 nhóm theo mẫu số — (a) K101791/K103058/K105955 (NAME PLATE/VENTILATOR/FLEXIPAD, lặp ở nhiều bản vẽ 12xx) — tiền tố "K" + lặp lại nhất quán cho thấy đây là LINH KIỆN CATALOG/MUA NGOÀI, không có file DXF riêng — cảnh báo này ĐÚNG NHƯ KỲ VỌNG, không phải lỗi; (b) 2101/2131/2151/2161/2201/2211 (STEEL STRUCTURE) và 1226-M/1236-M (FITTING ARRANGEMENT) — số hiệu tăng dần khớp theo từng bản vẽ chính, khả năng cao là file CÓ THẬT nhưng CHƯA được đưa vào batch quét này.
- User xác nhận qua AskUserQuestion: muốn sửa cảnh báo vòng lặp để in ra TOÀN BỘ chuỗi bản vẽ (thay vì chỉ tên 1 bản vẽ), giúp tự dò đúng dòng BOM gây lỗi.
- Sửa `Utilities/BomTemplateExporter.cs`: đổi tham số `visiting: HashSet<string>` thành `path: List<string>` (có thứ tự) trong CẢ `ExpandReach` và `ComputeClosure` — cycle-guard vẫn dùng `path.Contains(drawing)` (đường đi hiện tại), nhưng khi phát hiện vòng lặp, message giờ in đầy đủ `string.Join(" -> ", path) + " -> " + drawing` (hàm mới `FormatCyclePath`) — vd `"1300 -> 9001 -> 9002 -> 9001"` thay vì chỉ `"9001"`. Cập nhật các call site tương ứng (`new List<string>()` thay vì `new HashSet<string>(...)`).
- Build 0 lỗi. Verify lại bằng scratch project (kịch bản cycle 9001↔9002 dùng lại từ trước): warnings giờ hiển thị chuỗi đầy đủ `"1300 -> 9001 -> 9002 -> 9001"` và `"9001 -> 9002 -> 9001"` (2 điểm khởi đầu khác nhau, đúng như 2 nhánh đệ quy độc lập). Toàn bộ formula/giá trị cache khác (60/15/75...) không đổi — 0 lỗi OpenXml validation.

### Trạng thái

- Cảnh báo vòng lặp giờ đủ thông tin để user tự tra trong CAD (biết chính xác chuỗi bản vẽ nào tạo vòng lặp) — không cần đoán mò như trước.
- Nhóm cảnh báo "thiếu bản vẽ tham chiếu" xác nhận KHÔNG phải bug — hoạt động đúng thiết kế, chỉ là thông tin để user quyết định bổ sung file quét hay chấp nhận (hàng catalog).

### Bước tiếp theo

- NETLOAD bản build mới, chạy lại export thật, xem chuỗi đầy đủ của vòng lặp '4010' để xác định nguyên nhân thật (tự trỏ trong BOM hay trùng số hiệu do gộp nhiều file) và xử lý tận gốc trong dữ liệu CAD.
- Nếu muốn rollup đầy đủ cho 1216/1219/1226/1227/1236/1237: cân nhắc bổ sung các file 2101/2131/2151/2161/2201/2211 và 1226-M/1236-M (nếu tồn tại) vào batch quét.
- Ghi nhớ: nếu sau này thấy sheet Warnings quá dài do nhiều dòng LẶP LẠI cùng 1 bản vẽ đích thiếu (vd K105955 lặp 6 lần), có thể cân nhắc gộp thành 1 dòng liệt kê tất cả nơi tham chiếu — CHƯA làm vì user chưa yêu cầu.

## Session 2026-07-07 17:45

### Đã làm

- User yêu cầu triển khai tính năng đã nêu ở session trước: TOTAL QTY/TOTAL WEIGHT ở Summary phải tính CẢ hệ số Qty của Assembly List (tổng NVL thật cần cho cả dự án, không chỉ theo 1 lần build gốc), trong khi OCCURRENCES giữ nguyên (đã xác nhận trước đó).
- **Thiết kế**: mỗi bản vẽ (bất kỳ cấp nào, không chỉ 1*) có thể được 0, 1, hay NHIỀU bản vẽ TOP (1*-prefixed) "reach" tới qua đồ thị tham chiếu Drw, mỗi đường đi có hệ số nhân dồn riêng — hệ số hiệu dụng của 1 bản vẽ = TỔNG (Qty header của từng TOP × hệ số dồn theo đường đi đó). Bản vẽ không được TOP nào reach tới (đứng độc lập) giữ hệ số = 1 (không đổi hành vi cũ).
- Tạo `Utilities/AssemblyRollupContext.cs`: cầu nối dữ liệu giữa `BomTemplateExporter` (biết Excel row chính xác của từng ô Qty header trong Assembly List) và `BomSummaryExporter` (cần tham chiếu CHÉO SHEET tới đúng ô đó) — gồm `AssemblyHeaderRowOf` (top drawing -> Excel row) và `ContributionsOf` (MỌI bản vẽ -> danh sách (top drawing, hệ số dồn)).
- `Utilities/BomTemplateExporter.cs`: thêm `ExpandReach` (đệ quy CÓ cycle-guard, KHÔNG lọc theo cột Grade+Thickness — khác `ComputeClosure` cũ, vì hệ số "build bao nhiêu lần" không phụ thuộc vật liệu bên trong) chạy 1 lần cho mỗi bản vẽ TOP, đổ kết quả vào `rollup.ContributionsOf`. `BuildAssemblyListSheet` ghi thêm `rollup.AssemblyHeaderRowOf[section.Drawing] = headerRowExcel` ngay khi tạo header row. Thêm bước dedup `scan.Warnings` cuối `Build()` (2 nhánh đệ quy độc lập có thể cùng cảnh báo 1 vòng lặp).
- `Utilities/BomSummaryExporter.cs`: đổi `Group` từ `TotalQty/TotalWeight/Drawings` phẳng sang `PerDrawing: Dictionary<bản vẽ, (Qty, Weight)>` (cần tách theo TỪNG bản vẽ vì mỗi bản vẽ có hệ số nhân khác nhau). Công thức TOTAL QTY/WEIGHT đổi từ 1 SUMIFS duy nhất sang NỐI NHIỀU SUMIFS (1 số hạng/bản vẽ item xuất hiện, lọc thêm theo DRAWING NO) nhân với `BuildMultiplierFormula(drawing, rollup)` — hàm mới dựng công thức hệ số (tham chiếu `'Assembly List'!$E$hàng` CHÉO SHEET, hằng số "1" nếu bản vẽ không thuộc assembly nào). OCCURRENCES giữ nguyên `COUNTIFS` không đổi (theo đúng quyết định trước đó).
- `Utilities/BomExcelExporter.cs`: đổi thứ tự TÍNH TOÁN — `BomTemplateExporter.Build` (dựng layout Assembly List, biết Excel row header) phải chạy TRƯỚC `BomSummaryExporter.Build` (cần đọc `rollup` đã điền). Thứ tự TAB hiển thị (Summary trước Material List/Assembly List) không đổi.
- Build 0 lỗi. Verify bằng scratch project OpenXml SDK (dữ liệu 1215→4202→4205 + cặp cycle 9001↔9002 dùng lại từ trước): mọi công thức Summary đúng cấu trúc (`SUMIFS(...)*'Assembly List'!$E$13*6` cho PLATE/4205, `*'Assembly List'!$E$13*3` cho ANGLE/4202, v.v.), cached value khớp tay — ĐẶC BIỆT: TOTAL WEIGHT của PLATE (4205) = 60 và ANGLE (4202) = 15 trong Summary trùng khớp CHÍNH XÁC với TOTAL/KG đã verify ở Assembly List session trước (60+15=75) — 2 đường tính độc lập (closure theo cột vật liệu vs. rollup theo từng bản vẽ) cho cùng kết quả, tăng độ tin cậy đáng kể. 0 lỗi OpenXml validation.

### Trạng thái

- Summary giờ phản ánh đúng tổng NVL/số lượng THẬT CẦN cho cả dự án — sửa Qty ở bất kỳ bản vẽ TOP nào trong Assembly List sẽ lan đúng tới TOTAL QTY/WEIGHT của MỌI item liên quan (kể cả qua nhiều cấp tham chiếu), không chỉ Assembly List như trước.
- OCCURRENCES không đổi (đúng quyết định đã xác nhận) — vẫn là con số cấu trúc, độc lập với Assembly Qty.
- Kiến trúc "one true source" được mở rộng: Summary giờ tham chiếu CHÉO 2 sheet (ExportedData + Assembly List), không chỉ 1 sheet như trước — vẫn không có circular reference (Assembly List không tham chiếu ngược lại Summary).

### Bước tiếp theo

- NETLOAD bản build mới nhất, test tay trong Excel thật: đổi Qty ở 1 bản vẽ Assembly List, xác nhận TOTAL QTY/WEIGHT của các item liên quan trong Summary tự cập nhật đúng tỉ lệ.
- Nếu sau này cần thêm sheet mới cũng cần "hệ số dự án" này (vd 1 sheet order-vật-tư riêng), tái dùng `AssemblyRollupContext`/`BuildMultiplierFormula` — đã tách thành khối logic dùng chung, không cần viết lại.

## Session 2026-07-07 17:00

### Đã làm

- User đưa 2 ví dụ cần phân tích:
  1. Trong Assembly List, đổi Qty của bản vẽ (vd 1235) từ 1 → 2, dòng TOTAL/KG không đổi.
  2. Nếu đổi Qty của Assembly List thì cột OCCURRENCES (I) ở Summary nên đổi ra sao?
- **Phân tích ví dụ 1 (bug thật)**: `BuildAssemblyListSheet` (Utilities/BomTemplateExporter.cs) có ô Qty tô vàng ở đầu mỗi section (đại diện "cần build bao nhiêu bản vẽ này") — nhưng biến `sectionQtyRef` tính ra CHỈ nằm trong comment giải thích ý định, KHÔNG hề được ghép vào công thức tra cứu chéo của item nào (dead code) → sửa ô Qty đó không có tác dụng gì, đúng khớp triệu chứng user báo.
- **Fix**: chuyển khai báo `sectionQtyRef = $"$E${headerRowExcel}"` lên TRƯỚC vòng lặp item (thay vì sau, nơi nó không dùng được), rồi nhân trực tiếp vào công thức mỗi item: `=({SUMIFS...})*{qtyRef}*{sectionQtyRef}` — vừa nhân Qty của chính item (đã có từ trước) vừa nhân Qty của bản vẽ section (mới thêm). Dòng TOTAL/KG cuối sheet không cần sửa gì — nó chỉ `SUM()` các ô item đã có sẵn công thức đúng, tự động kế thừa.
- Build 0 lỗi. Verify lại bằng scratch project OpenXml SDK (kịch bản 1215→4202→4205 ở session trước): formula giờ có thêm `*$E$13` (đúng ô header Qty của section 1215, row 13 — kiểm chứng bằng cách đếm số dòng title-block+header cố định), cached value không đổi (do Qty mặc định vẫn =1 lúc export) — khớp đúng, không phá vỡ kết quả cũ.
- **Phân tích ví dụ 2**: OCCURRENCES (`COUNTIFS` trên ExportedData theo Description+Size+Material+Type) là con số CẤU TRÚC — đếm item xuất hiện trong bao nhiêu dòng BOM đã quét — hoàn toàn không đọc cột Qty nào, kể cả Qty của Assembly List (ô đó thậm chí không tồn tại trong ExportedData). Về ý nghĩa, KHÔNG nên đổi theo Qty Assembly. Ghi chú thêm cho user: TOTAL QTY/TOTAL WEIGHT (cột F/G) ở Summary CŨNG có cùng đặc điểm này (không nhân hệ số Assembly Qty) — nếu cần "tổng NVL thật cần cho cả dự án" sẽ là 1 tính năng lớn hơn, cần mở rộng closure sang từng dòng ExportedData.
- Đã hỏi user qua AskUserQuestion — xác nhận **giữ nguyên OCCURRENCES như hiện tại**, không triển khai tính năng "tổng theo dự án" ở session này.

### Trạng thái

- Assembly List giờ có ĐỦ 3 lớp hệ số nhân sống: NVL gốc (SUMIFS ExportedData) × hệ số dồn nhiều cấp tham chiếu (closure) × Qty item × Qty bản vẽ section — tất cả LIVE, user sửa ô nào cũng lan đúng tới TOTAL/KG.
- Summary giữ nguyên thiết kế "theo 1 lần build gốc" — không bị ảnh hưởng bởi Assembly List Qty, đúng như xác nhận của user.

### Bước tiếp theo

- NETLOAD bản build mới nhất, test tay trong Excel thật: đổi Qty ở đầu 1 section Assembly List, xác nhận dòng TOTAL/KG cuối sheet tăng đúng tỉ lệ.
- Nếu sau này user cần "tổng NVL thật cho cả dự án" (tính cả Assembly Qty): cần thiết kế riêng — mở rộng closure hiện có để tính "hệ số hiệu dụng" cho TỪNG DÒNG ExportedData (không chỉ từng cột vật tư như hiện tại), cộng dồn theo MỌI đường dẫn từ các assembly cấp cao nhất — việc này phức tạp hơn đáng kể vì 1 item lá có thể được nhiều assembly khác nhau dùng với hệ số khác nhau.

## Session 2026-07-07 16:10

### Đã làm

- User đưa ví dụ cụ thể: bản vẽ 4205 thuộc 4202, 4202 lại thuộc 1215 (tham chiếu lồng 3 cấp qua cột Drw) — yêu cầu phân tích tại sao vật tư của 4205 không được tổng hợp lên 1215, và đề xuất phương án fix.
- **Phân tích root cause**: `BuildAssemblyListSheet` (Utilities/BomTemplateExporter.cs) trước đây chỉ tra cứu chéo **ĐÚNG 1 CẤP** — với dòng 1215 có `Drw="4202"`, công thức SUMIFS lọc `DRAWING NO="4202"` trực tiếp, nhưng bản thân dòng của 4202 lại KHÔNG có MATERIAL COLUMN KEY (vì nó tham chiếu tiếp 4205 qua Drw, không phải NVL gốc) → luôn ra 0, dù NVL thật nằm ở 4205 (đã được quét, có trong ExportedData). Kiến trúc cũ coi quan hệ là 2 tầng cố định ("1*" ↔ còn lại), trong khi dữ liệu thực chất là 1 ĐỒ THỊ tham chiếu Drw nhiều cấp, độ sâu tuỳ ý. Đã trình bày phân tích + phương án cho user qua AskUserQuestion, được xác nhận triển khai ngay.
- Sửa `Utilities/BomTemplateExporter.cs`:
  1. `ComputeOwnColumnSums`: tính NVL GỐC trực tiếp (không qua Drw) của MỖI bản vẽ theo từng cột Grade+Thickness — cho TOÀN BỘ bản vẽ (không chỉ non-1*) để làm base case cho đệ quy.
  2. `BuildReferenceGraph`: dựng đồ thị cạnh `bản vẽ chứa item -> Drw được tham chiếu` kèm hệ số Qty của chính dòng đó, từ TOÀN BỘ `scan.Items`.
  3. `ComputeClosure` (đệ quy có nhớ — memoized, dùng `cache` dict theo tên bản vẽ): với mỗi bản vẽ, trả về `Dictionary<cột Grade+Thickness, Dictionary<bản vẽ LÁ, hệ số nhân dồn>>` — hệ số nhân dồn = tích các Qty dọc đường từ bản vẽ gốc tới lá. Có **cycle-guard** bằng `visiting` HashSet theo nhánh đệ quy: phát hiện vòng lặp tham chiếu (lỗi dữ liệu CAD) thì dừng nhánh đó + ghi `scan.Warnings`, không treo chương trình.
  4. `BuildAssemblyListSheet`: công thức tra cứu chéo mỗi item đổi từ 1 SUMIFS duy nhất sang **nối nhiều SUMIFS** (1 số hạng/bản vẽ lá trong closure, nhân hệ số nếu ≠1) bằng dấu "+", rồi nhân với ô Qty của chính dòng đó (vẫn LIVE, cột E, người dùng sửa được). Nếu Drw tham chiếu 1 bản vẽ KHÔNG có trong danh sách đã quét → ghi Warning thay vì âm thầm bỏ qua.
- **Đánh đổi đã chấp nhận**: Qty ở tầng TRÊN CÙNG (dòng của 1215) vẫn sống/sửa được; Qty ở các tầng SÂU HƠN (bên trong 4202) là HẰNG SỐ tính sẵn lúc export — vì số đó đến từ chính bản vẽ CAD, không phải thứ user sửa tay trong Excel, và sheet cũng chưa có ô nào đại diện sống cho nó.
- Build thành công, 0 lỗi.
- Verify bằng project console .NET 8 + OpenXml SDK (cập nhật lại scratch project đã dùng ở session trước) với đúng kịch bản 1215→4202(Qty=2 tới 4205)→4205(AH36,10kg), cộng thêm NVL riêng của 4202 (SM490B,5kg), VÀ 1 cặp bản vẽ vòng lặp giả lập (9001↔9002) để test cycle-guard:
  - `Assembly List!G14` (AH36) = `(SUMIFS(...,"4205",...)*2)*E14` = 60 — khớp tay (10kg × 2 × Qty=3 của 1215).
  - `Assembly List!H14` (SM490B) = `(SUMIFS(...,"4202",...))*E14` = 15 — khớp tay (5kg × Qty=3, không nhân thêm vì mult=1).
  - Dòng TOTAL/KG cuối sheet = 75 (60+15) — khớp tay.
  - Cycle 9001↔9002: cảnh báo được ghi đúng vào Warnings, KHÔNG treo chương trình, export vẫn hoàn tất bình thường (93ms).
  - 0 lỗi OpenXml validation.

### Trạng thái

- Assembly List giờ tổng hợp đúng NVL qua BẤT KỲ số cấp tham chiếu Drw nào (không giới hạn 2 tầng "1*"/khác như trước) — đúng yêu cầu ví dụ 1215←4202←4205 của user.
- Material List KHÔNG cần sửa — đã đúng từ trước vì mọi bản vẽ non-1* (leaf hay trung gian) đều được liệt kê phẳng và cộng đúng 1 lần vào tổng chung, không phân biệt cấp.

### Bước tiếp theo

- NETLOAD bản build mới nhất, chạy lại export trên bộ file thật có tham chiếu lồng nhau thật (không chỉ dữ liệu giả lập), xác nhận trong Excel thật: Assembly List không còn 0 ở các mục có tham chiếu nhiều cấp, sheet Warnings hiển thị đúng nếu có bản vẽ tham chiếu bị thiếu/vòng lặp.
- Ghi nhớ: nếu sau này cần Qty ở TẦNG TRUNG GIAN cũng "sống" (thay vì hằng số bake lúc export), sẽ cần thêm 1 bước: đổi cột Qty của Material List từ text sang NumberCell + xây bảng tra "item -> (sheet, hàng Excel)" cho MỌI item ở cả 2 sheet, rồi build công thức lồng nhau tham chiếu chéo sheet thay vì hệ số hằng — việc này KHÔNG cần thiết trừ khi có yêu cầu cụ thể.

## Session 2026-07-07 15:30

### Đã làm

- User yêu cầu tổng quát hóa 2 điều sau khi đã fix xong bug QTY/WEIGHT text-vs-number (session trước):
  1. Tách hoàn toàn bước "đọc + xử lý dữ liệu" khỏi bước "ghi Excel" — đọc toàn bộ vào 1 List trước, ghi ĐỒNG LOẠT 1 lần duy nhất, không ghi rải rác từng dòng trong lúc duyệt.
  2. Chuẩn hóa kiểu dữ liệu (Number/String) + validate Null/Empty ngay tại bước đọc, không hardcode theo index cột nữa.
- Tạo `Models/ExportedDataRow.cs`: dòng dữ liệu ĐÃ CHUẨN HÓA KIỂU cho ExportedData — `Qty`/`Weight` là `double?` THẬT SỰ (không phải string), giữ `Source` (BomItem gốc) để tra cứu ngược `RowOf`.
- Tạo `Utilities/ExportedDataBuilder.cs`: `Build(List<BomItem>) -> List<ExportedDataRow>` — duyệt 1 lần, `NormalizeText` (null → "") và `TryParseDouble` (parse an toàn, không bao giờ throw, null/rỗng/không phải số → null) cho Qty/Weight. Đây là bước "đọc toàn bộ + validate" tách biệt hoàn toàn khỏi ghi file.
- Sửa `Utilities/MinimalXlsxWriter.cs`: đổi `SheetData.Rows` từ `List<string[]>` sang `List<object[]>`, **bỏ hẳn `NumericColumns`** (cơ chế khai báo trước cột nào là số theo index — dễ quên khi thêm cột mới). Thay bằng `IsNumeric(object)` tự nhận diện kiểu THẬT tại runtime (`double`/`int`/`float`/`long`/`decimal` → ghi ô Số; `null`/chuỗi rỗng → ô trống; còn lại → `inlineStr`).
- Sửa `Utilities/BomExcelExporter.cs`: gọi `ExportedDataBuilder.Build(scan.Items)` TRƯỚC để có `List<ExportedDataRow>` đầy đủ, sau đó mới duyệt List này dựng `object[]` và `layout.RowOf[row.Source] = excelRow` (dùng `Source` — BomItem gốc — làm key, không đọc/parse lại `BomItem` ở bước ghi).
- Build thành công, 0 lỗi (`CSSCADTools_20260707_1419.dll`).
- Verify bằng project console .NET 8 độc lập + OpenXml SDK (copy các file không phụ thuộc AutoCAD vào scratch project) với dữ liệu cố ý có: Qty/Weight hợp lệ, Qty null, Weight rỗng, Qty/Weight chuỗi rác ("abc", "not-a-number") — kết quả: mọi ô Qty/Weight hợp lệ ghi ra `DataType=null` (Số thật); mọi ô null/rỗng/rác ghi ra ô TRỐNG (không crash, không còn `inlineStr` với nội dung rác); mọi công thức SUMIFS/COUNTIFS/SUMPRODUCT/tham chiếu trực tiếp ở Summary/Material List/Assembly List tính đúng khớp tay (item có Qty null/rác đóng góp 0 vào tổng — đúng như kỳ vọng, không làm sai lệch các item khác); **0 lỗi OpenXml validation**.

### Trạng thái

- Kiến trúc export giờ đúng 2 pha rõ ràng: (1) đọc + validate + ép kiểu toàn bộ `BomItem` → `List<ExportedDataRow>`, (2) ghi đồng loạt từ List đã chuẩn hóa xuống Excel — không còn xen kẽ đọc/ghi.
- Việc "ghi đồng loạt xuống đĩa" thực ra ĐÃ là 1 lần duy nhất từ trước (MinimalXlsxWriter dựng toàn bộ XML qua StringBuilder rồi ghi 1 lần), nên phần thời gian "vài phút" mà user quan sát được nếu có, nhiều khả năng đến từ bước gọi AutoCAD `Database.ReadDwgFile`/`DxfIn` trong `BomFileScanner.ScanFiles` (1 lệnh AutoCAD API nặng mỗi file nguồn, có retry 3 lần) — refactor lần này KHÔNG đụng tới phần đó.
- Nhận diện kiểu Number/String giờ tự động theo kiểu runtime của object, không còn phụ thuộc khai báo `NumericColumns` theo index cột — an toàn hơn khi sau này thêm cột mới vào ExportedData.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260707_1419.dll`, chạy export trên bộ file thật, xác nhận không có gì thay đổi hành vi so với trước (đây là refactor nội bộ, không đổi output).
- Nếu user thực sự cần giảm thời gian export từ "vài phút" xuống "vài giây", bước tiếp theo nên là profile `BomFileScanner.ScanFiles` (đặc biệt là DxfIn/ReadDwgFile + retry loop) — đây mới là phần I/O nặng thật sự, chưa được đo hay tối ưu trong session này.

### Ghi chú API

- OpenXml SDK: `Cell.DataType == null` là số (Number) trong OOXML mặc định — không phải chỉ `t="inlineStr"` là text, cần nhớ khi tự review file trong tương lai.
- Array covariance C#: `string[]` gán được vào tham số `object[]` (ví dụ `Rows.Add(new[] { "a warning" })` khi `Rows` là `List<object[]>`) — không cần ép kiểu tường minh cho sheet Warnings (chỉ có 1 cột text).

## Session 2026-07-07 14:02

### Đã làm

- User báo file thật (`BOM_Export_20260707_1350.xlsx`): sheet Summary cột TOTAL QTY/WEIGHT ra 0, ô khối lượng tra cứu chéo ở Assembly List ra 0.
- Quy trình chẩn đoán:
  1. Kiểm tra bằng OpenXml SDK — mọi công thức + giá trị CACHE đều đúng, không phát hiện lỗi (vì SDK chỉ đọc cache do code tự tính, không thực sự tính lại công thức như Excel).
  2. Hỏi user ép Excel tính lại toàn bộ (Ctrl+Alt+Shift+F9) — **vẫn ra 0** → loại trừ giả thuyết "chưa recalc", xác nhận là lỗi THẬT trong công thức/dữ liệu.
  3. So sánh: DIRECT REFERENCE (`=ExportedData!I15`, dùng ở Material List) vẫn đúng, chỉ CÔNG THỨC SUMIFS/SUMPRODUCT (Summary, Assembly List cross-ref) mới ra 0 → thu hẹp phạm vi nghi vấn về chính bản chất phép SUM.
- **Tìm ra nguyên nhân gốc**: cột QTY và WEIGHT trong sheet ExportedData đang được ghi dưới dạng **văn bản** (`t="inlineStr"`) thay vì số thực — vì `BuildSheet` (chế độ Headers+Rows dùng cho ExportedData) trước giờ ghi MỌI giá trị không rỗng thành `inlineStr` đồng loạt, không phân biệt cột nào cần là số. Excel's `SUM`/`SUMIFS`/`SUMPRODUCT` **bỏ qua hoàn toàn ô kiểu text** khi cộng (dù nội dung "nhìn giống số" như "6.2"), nên mọi công thức tham chiếu 2 cột này luôn ra 0 — trong khi tham chiếu trực tiếp (relay giá trị, không cộng) vẫn hiển thị đúng, khớp chính xác với triệu chứng user báo.
- Fix:
  1. `Utilities/MinimalXlsxWriter.cs`: thêm `SheetData.NumericColumns` (HashSet cột 0-based cần ghi số thực thay vì text) cho chế độ Headers+Rows; `BuildSheet` parse giá trị thành số thật (`<v>`, không `t="inlineStr"`) khi cột nằm trong danh sách này.
  2. `Utilities/BomExcelExporter.cs`: đánh dấu cột QTY (index 1) và WEIGHT (index 8) của ExportedData là `NumericColumns`.
- Build thành công → `CSSCADTools_20260707_1402.dll`.
- Verify lại bằng OpenXml SDK: `ExportedData!B2/I2` giờ có `DataType=null` (mặc định number, không còn InlineString); toàn bộ SUMIFS/SUMPRODUCT tính đúng khớp tay (Summary Qty/Weight đúng, Assembly List cross-ref = 12.4, Material List tổng = 6.2).

### Trạng thái

- Đã xác định và fix đúng root cause (không phải đoán) — bug có tính hệ thống, ảnh hưởng TOÀN BỘ công thức SUMIFS/SUMPRODUCT trong cả 3 sheet (Summary, Material List, Assembly List), không chỉ vài chỗ lẻ tẻ. Fix 1 chỗ (cột nguồn) giải quyết triệt để cho mọi công thức phụ thuộc.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260707_1402.dll`, chạy lại export trên toàn bộ 13 file, xác nhận trong Excel thật: Summary TOTAL QTY/WEIGHT không còn 0, Assembly List cross-reference không còn 0, Material List TOTAL/KG cuối sheet không còn 0.
- Ghi nhớ nguyên tắc chung cho các cột số trong tương lai (nếu thêm sheet/cột mới cần SUM/SUMIFS tham chiếu): PHẢI đánh dấu qua `NumericColumns` (Headers+Rows) hoặc dùng `NumberCells`/`Formulas` (GridRows) — không được để ở dạng text nếu có công thức cộng dồn tham chiếu tới.

## Session 2026-07-07 13:47

### Đã làm

- User yêu cầu thay đổi kiến trúc lớn: đổi tên sheet "BOM" → "ExportedData", biến sheet này thành **"one true source"** — các sheet khác (Summary, Material List, Assembly List) phải dùng CÔNG THỨC Excel để lấy dữ liệu từ ExportedData thay vì copy giá trị tĩnh, để sửa 1 dòng trong ExportedData thì mọi nơi khác tự cập nhật.
- Thống nhất phạm vi qua AskUserQuestion: (1) Excel tester chắc chắn 365/2019+; (2) phần con số Weight ở Material List/Assembly List cũng chuyển thành công thức tham chiếu (không chỉ Summary).
- Thiết kế: phần PHÂN LOẠI (item thuộc cột Grade+Thickness nào, thuật toán SteelSpecClassifier) vẫn do code quyết định — không thể diễn đạt bằng công thức Excel thường; chỉ phần GIÁ TRỊ HIỂN THỊ trở thành công thức sống.
- Tạo `Utilities/BomIdentity.cs` (helper dùng chung: `ExtractDrawingNumber`, `MaterialColumnKey`) và `Utilities/ExportedDataLayout.cs` (khai báo tập trung cột A-M của ExportedData + `Dictionary<BomItem,int> RowOf` + helper `Ref()`/`Range()` dựng chuỗi tham chiếu/vùng) — dùng chung giữa mọi exporter, tránh lệch cột.
- Mở rộng `MinimalXlsxWriter.cs`: thêm `GridRow.TextFormulas` (công thức trả về text), thêm Freeze/AutoFilter cho chế độ GridRows (để Summary dùng chung mode với Material/Assembly List thay vì Headers+Rows).
- `BomExcelExporter.cs`: đổi tên sheet → "ExportedData", thêm 2 cột helper **MATERIAL COLUMN KEY** và **DRAWING NO** (tính sẵn bằng code, vì bản thân việc phân loại là thuật toán không thể viết bằng công thức Excel thường), xây `RowOf` mapping (item → hàng Excel) khi ghi dữ liệu.
- `BomSummaryExporter.cs`: TOTAL QTY/WEIGHT/OCCURRENCES chuyển thành công thức `SUMIFS`/`COUNTIFS` sống tham chiếu ExportedData. **Quan trọng**: cân nhắc dùng `TEXTJOIN`+`UNIQUE` cho cột USED IN nhưng đã HỦY — các hàm "hiện đại" này khi Excel lưu vào OOXML cần tiền tố đặc biệt (`_xlfn.`, hàm mảng động như UNIQUE là `_xlfn._xlws.`) mà không có Excel thật để kiểm chứng chắc chắn sẽ viết đúng, viết sai sẽ ra lỗi `#NAME?` không thể phát hiện qua OpenXml SDK validator (chỉ kiểm tra schema, không kiểm tra hàm tồn tại đúng không) → giữ USED IN là text tĩnh (an toàn), chỉ SUMIFS/COUNTIFS (hàm cổ điển ổn định, không cần tiền tố) là công thức sống.
- `BomTemplateExporter.cs`: Material List — ô Weight (cả cột phân loại lẫn TOTAL/KG cùng dòng) là công thức **tham chiếu trực tiếp** `=ExportedData!I{row}`; dòng tổng cuối sheet dùng `SUMIFS` (theo cột MATERIAL COLUMN KEY) + `SUMPRODUCT` (theo điều kiện DRAWING NO không bắt đầu bằng "1"). Assembly List — tra cứu chéo chuyển từ số tính sẵn sang công thức `SUMIFS` thật (lọc theo DRAWING NO + MATERIAL COLUMN KEY) nhân với ô Qty của chính item (nay là NumberCell thay vì text, để nhân được trong công thức); dòng tổng cuối sheet giữ nguyên logic gộp số hạng theo section.
- Build thành công → `CSSCADTools_20260707_1347.dll`.
- Sanity-check ngoài AutoCAD bằng OpenXml SDK (chính thức, không phải công cụ tự chế) với dữ liệu thật (LOCATOR→4050, BRACKET→5800, TRIANGLE PIECE xuất hiện 2 lần): **0 lỗi validation**, mọi công thức tính đúng khớp tay — Summary gộp đúng Qty/Weight, Material List tham chiếu trực tiếp đúng dòng nguồn, Assembly List cross-reference SUMIFS tính đúng 12.4 và 27, TOTAL/KG cuối cùng = 39.4 khớp chính xác.

### Trạng thái

- Code xong, build xong, đã xác minh cấu trúc + giá trị công thức bằng OpenXml SDK. Chưa test lại full 13 file trong AutoCAD, và CHƯA thể xác nhận 100% mọi công thức SUMIFS/SUMPRODUCT tính đúng khi MỞ THẬT bằng Excel (chỉ xác nhận cached value đúng — Excel sẽ tự tính lại nhờ `fullCalcOnLoad`, nhưng cần user xác nhận không có ô nào hiện `#REF!`/`#VALUE!`).

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260707_1347.dll`, chạy lại export trên toàn bộ 13 file, mở bằng Excel thật:
  - Kiểm tra sheet ExportedData có đúng tên, đủ 13 cột.
  - Thử SỬA 1 dòng trong ExportedData (vd đổi Weight), xác nhận Summary/Material List/Assembly List tự cập nhật đúng.
  - Kiểm tra không có ô nào hiện lỗi `#REF!`, `#VALUE!`, `#NAME?`.
- Nếu sau này cần "USED IN" cũng thành công thức sống, cần user xác nhận đã test `_xlfn.TEXTJOIN`/`_xlfn._xlws.UNIQUE` hoạt động đúng trong Excel thật trước khi áp dụng lại.

## Session 2026-07-07 08:29

### Đã làm

- User gặp lỗi NETLOAD trên máy tester khác: `FileLoadException ... An attempt was made to load an assembly from a network location...` — do Windows đánh dấu "Mark of the Web" lên DLL khi truyền sang máy khác (qua mạng/email/cloud), .NET Framework chặn `Assembly.LoadFrom` với file bị đánh dấu này. Đã giải thích đây là giới hạn ở tầng CLR/Windows, không phải lỗi trong code CSSCADTools — không thể fix bằng code bên trong DLL vì lỗi xảy ra TRƯỚC khi assembly được nạp.
- Hướng dẫn 2 cách unblock thủ công (Properties > Unblock, hoặc `Unblock-File` qua PowerShell/CMD).
- User yêu cầu giải pháp triệt để hơn: tham khảo file `Install_AutoLoadCadAddin.bat` (dùng cho project MCG khác, cùng chỗ) để tạo installer tương tự cho CSSCADTools — cài vào `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (bundle, tự động load khi AutoCAD khởi động, không cần NETLOAD thủ công nữa) kèm bước tự động unblock.
- Tạo `Install_CSSCADTools.bat` ở gốc project — double-click là cài xong:
  1. Tạo cấu trúc bundle `%PROGRAMDATA%\Autodesk\ApplicationPlugins\CSSCADTools.bundle\Contents`.
  2. Tự động dò và CHỈ lấy đúng 1 file `CSSCADTools_*.dll` MỚI NHẤT theo ngày sửa đổi (vì thư mục build tích lũy nhiều bản DLL cũ theo timestamp — nếu copy hết sẽ đăng ký nhầm nhiều bản xung đột nhau).
  3. Dọn DLL/PDB cũ trong Contents, copy DLL+PDB mới nhất vào.
  4. **Tự động unblock** bằng PowerShell (`Unblock-File`) — bước then chốt giải quyết đúng lỗi user gặp phải.
  5. Sinh `PackageContents.xml` với `LoadOnAutoCADStartup="True"` — từ nay AutoCAD tự load plugin, không cần NETLOAD thủ công.
- Test dry-run logic (KHÔNG đụng vào `%PROGRAMDATA%` thật): tạo 3 file DLL giả lập với timestamp khác nhau, xác nhận script chọn đúng file MỚI NHẤT, copy đúng, sinh XML đúng cấu trúc.

### Trạng thái

- Script đã tạo và test logic thành công. Chưa test THẬT trên máy tester (cần user tự chạy để xác nhận unblock + auto-load hoạt động đúng với AutoCAD thật).

### Bước tiếp theo

- Copy `Install_CSSCADTools.bat` + file `CSSCADTools_*.dll` (+ `.pdb` nếu muốn debug) mới nhất vào CÙNG 1 thư mục, gửi cho tester, double-click file .bat để cài.
- Từ lần cài này trở đi, AutoCAD sẽ tự động load plugin khi khởi động (không cần NETLOAD) — mỗi lần có build mới, chỉ cần copy DLL mới + chạy lại .bat.

## Session 2026-07-07 07:59

### Đã làm

- User yêu cầu thêm sheet "Summary" — bản gộp của sheet BOM, tổng hợp Qty của cùng 1 item xuất hiện ở nhiều bản vẽ khác nhau.
- Thống nhất thiết kế qua AskUserQuestion: (1) nếu Delivery khác nhau giữa các lần xuất hiện của cùng 1 item → hiển thị "Mixed", vẫn gộp chung 1 dòng; (2) tổng hợp từ TOÀN BỘ `scan.Items` (giống hệt phạm vi sheet BOM), không giới hạn theo 1*/không phải 1*.
- Tạo `Utilities/BomSummaryExporter.cs`: gom nhóm theo khóa **Description + Size + Material + Type** (không phân biệt hoa/thường, trim khoảng trắng) — vì không có mã định danh duy nhất xuyên suốt các bản vẽ. Mỗi nhóm: cộng dồn Qty và Weight (parse số), gom danh sách bản vẽ chứa item ("USED IN"), đếm số lần xuất hiện ("OCCURRENCES"), Delivery = giá trị chung nếu đồng nhất, "Mixed" nếu khác nhau.
- Nối vào `BomExcelExporter.cs`: thêm sheet "Summary" ngay sau sheet "BOM" (trước Material List/Assembly List).
- Build thành công → `CSSCADTools_20260707_0759.dll`.
- Sanity-check ngoài AutoCAD: TRIANGLE PIECE xuất hiện ở 2 bản vẽ (1215 Qty=2, 1225 Qty=3, cùng Delivery=M) → gộp đúng thành 1 dòng TotalQty=5, TotalWeight=45.75, UsedIn="1215, 1225"; BRACKET xuất hiện 2 bản vẽ với Delivery khác nhau (Y/M) → Delivery hiển thị đúng "Mixed"; item PLATE hoàn toàn khác không bị gộp nhầm.

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ. Chưa test lại full 13 file trong AutoCAD.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260707_0759.dll`, chạy lại export trên toàn bộ 13 file, kiểm tra sheet "Summary" — đối chiếu vài item xuất hiện ở nhiều bản vẽ (nếu có) để xác nhận Qty cộng đúng.

## Session 2026-07-06 17:06

### Đã làm

- User yêu cầu 2 điểm: (1) All-border nhưng nét mỏng nhất, (2) chỉ áp All-border trong đúng phạm vi dữ liệu xuất ra (vd sheet Assembly List chỉ A10:Z139), không phải toàn sheet.
- Kiểm tra file thật (`BOM_Export_20260706_1659.xlsx`) bằng OpenXml SDK: quét toàn bộ cell, xác nhận vùng lưới full-border (all-border liên tục) đã đúng đang nằm gọn trong **A10:Z139** — khớp chính xác với con số user đưa ra. Phần cell có border NGOÀI phạm vi đó (A1, A4, A5, A6, A8) chỉ là các Ô NHÃN ĐƠN LẺ ở title block (in đậm, "MacGregor"/"YARD/SHIP:"...) đã có từ thiết kế ban đầu, không phải lưới lan ra — không phải lỗi, không cần sửa gì cho yêu cầu #2.
- Xử lý yêu cầu #1: `Utilities/MinimalXlsxWriter.cs` — đổi border style từ `medium` → **`hair`** (nét mỏng nhất trong OOXML, mỏng hơn cả `thin`), giữ nguyên màu đen tuyệt đối `rgb="FF000000"`.
- Build thành công → `CSSCADTools_20260706_1706.dll`.
- Verify lại bằng OpenXml SDK: validate 0 lỗi, border style xác nhận đã đổi đúng sang `hair`.

### Trạng thái

- Code xong, build xong, đã xác nhận cấu trúc bằng OpenXml SDK. Yêu cầu #2 xác nhận ĐÃ đúng sẵn từ trước (không cần sửa code), chỉ có #1 là thay đổi thực sự.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260706_1706.dll`, kiểm tra lại trong Excel thật — border giờ phải mảnh hơn hẳn so với bản "medium" trước, và vẫn chỉ giới hạn đúng trong vùng dữ liệu (A10 trở đi đến hàng cuối, không tràn ra ngoài).

## Session 2026-07-06 16:56

### Đã làm

- User gửi file mới (`BOM_Export_20260706_1641.xlsx`), báo vẫn không thấy All Border ở sheet Assembly List trong phạm vi A10:Z127.
- Quét TOÀN BỘ 3068 ô trong đúng phạm vi A10:Z127 bằng OpenXml SDK: 0 ô thiếu, 0 ô không có border theo khai báo trong file — mâu thuẫn với báo cáo của user.
- Hỏi lại chi tiết: user xác nhận **chỉ hàng Header cột và hàng Drawing Number (in đậm) có viền, các hàng Item (không đậm) thì không** — đây là manh mối quan trọng: phân biệt chính xác theo Bold (s=1) vs Normal (s=0).
- Tìm ra nguyên nhân: `cellStyleXfs` (base style "Normal") khai báo `borderId="0"` (không viền), trong khi TẤT CẢ cellXfs (kể cả s=0 "thường") dựa vào cơ chế **override** (`applyBorder="1"` trỏ sang `borderId="1"`) để có viền. Đúng chuẩn OOXML thì override này phải được tôn trọng, nhưng khớp chính xác với triệu chứng user mô tả — nghi ngờ Excel không tôn trọng override nhất quán khi file thiếu `<cellStyles>` (mà file Excel thật luôn có).
- Fix `Utilities/MinimalXlsxWriter.cs`: đổi `cellStyleXfs` sang khai báo SẴN `borderId="1"` (khớp luôn từ base, không cần override), đồng thời thêm `<cellStyles>` (`name="Normal" xfId="0" builtinId="0"`) cho đầy đủ như file Excel thật tạo ra — loại bỏ hoàn toàn sự phụ thuộc vào cơ chế override có thể không ổn định.
- Build thành công → `CSSCADTools_20260706_1656.dll`.
- Verify lại bằng OpenXml SDK: validate 0 lỗi; `cellStyleXfs` giờ `borderId=1`; `cellStyles` có "Normal" đúng chuẩn; kiểm tra cụ thể `Assembly List!B14` (item row, không đậm, styleIndex=0) và `A13` (header đậm, styleIndex=1) đều `borderId=1`.

### Trạng thái

- Code xong, build xong, đã xác minh cấu trúc file bằng OpenXml SDK. Đây là chẩn đoán hợp lý nhất dựa trên manh mối user cung cấp (bold vs non-bold), nhưng CHƯA thể xác nhận 100% đây là nguyên nhân gốc vì không có Excel thật để render trực tiếp — cần user xác nhận sau khi test.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260706_1656.dll`, đóng HẲN Excel (không chỉ đóng file) trước khi mở file export mới để loại trừ cache hiển thị cũ, kiểm tra lại xem các hàng Item (không đậm) đã có viền chưa.
- Nếu VẪN không thấy viền ở dòng Item sau fix này, đây sẽ là bằng chứng cho thấy giả thuyết "thiếu cellStyles" sai — cần gửi lại file để điều tra hướng khác (có thể liên quan đến version Excel cụ thể hoặc cách Excel cache styles.xml).

## Session 2026-07-06 16:33

### Đã làm

- User gửi file report thật (`BOM_Export_20260706_1616.xlsx`) kèm 3 câu hỏi: (1) tại sao border không hiện ở các dòng không tô vàng, (2) bỏ tô vàng sheet Assy List, (3) tại sao Material List cũng không có border.
- Chẩn đoán bằng `DocumentFormat.OpenXml` (SDK chính thức của Microsoft, KHÔNG phải ClosedXML — không dính lỗi SixLabors.Fonts vì chạy trong console app riêng ngoài AutoCAD): validate toàn file = **0 lỗi**; kiểm tra trực tiếp các cell KHÔNG tô vàng (`Material List!A14`, `G14`, `Assembly List!A18`) đều có `borderId=1` với border "thin" áp dụng đầy đủ 4 cạnh — **file không có lỗi cấu trúc, border đã đúng chuẩn OOXML trên mọi cell kể cả Material List**. Kết luận: nhiều khả năng là vấn đề cảm quan — viền "thin" màu đen tự động (`indexed=64`) dễ bị nhầm với gridline mặc định màu xám nhạt của Excel, trong khi các ô tô vàng nổi bật hẳn do có màu nền.
- Phát hiện nguyên nhân THẬT của việc gần như toàn bộ sheet Assembly List bị tô vàng trong dữ liệu thật: nhiều item có `Drw` trỏ sang **1 bản vẽ "1*" KHÁC** (vd "INTERFACE"→Drw=1215, "FITTING ARRANGEMENT"→Drw=1216/1219) — không phải bản vẽ chi tiết 4xxx/5xxx. Logic tra cứu chéo hiện tại chỉ hỗ trợ 1 cấp (1* → không phải 1*) nên các trường hợp lồng nhau này luôn "không giải quyết được", khiến gần hết sheet bị tô vàng — không hữu ích.
- Xử lý:
  1. `Utilities/MinimalXlsxWriter.cs`: đổi border từ `style="thin"` + `color indexed="64"` → **`style="medium"` + `color rgb="FF000000"`** (đen tuyệt đối) để loại trừ hoàn toàn khả năng nhầm lẫn với gridline mặc định, bất kể nguyên nhân thật là gì.
  2. `Utilities/BomTemplateExporter.cs` (`BuildAssemblyListSheet`): **bỏ tô vàng theo hàng item** (logic tra cứu chéo vẫn giữ nguyên, chỉ bỏ phần `itemRow.Highlight = !resolved`) — vẫn giữ tô vàng riêng cho ô Qty=1 ở hàng đầu section (mục đích khác: đánh dấu ô cần điền tay, không phải cảnh báo dữ liệu thiếu).
- Build thành công → `CSSCADTools_20260706_1633.dll`.
- Re-verify bằng OpenXml SDK sau khi sửa: `Assembly List!G14` (item trước đây bị tô vàng) giờ `styleIndex=0` (hết vàng) ✓; `Assembly List!E13` (ô Qty) vẫn `styleIndex=2` (còn vàng, đúng ý đồ) ✓; border xác nhận đã đổi thành `style="medium"` `color rgb="FF000000"` ✓; validate vẫn 0 lỗi.

### Trạng thái

- Code xong, build xong, đã xác minh bằng SDK chính thức (không phải suy đoán). Chưa xác nhận cảm quan thực tế trong Excel liệu border "medium" đã đủ rõ ràng theo mắt người dùng hay chưa.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260706_1633.dll`, mở lại file export trong Excel thật, xác nhận border giờ rõ ràng trên mọi dòng (kể cả dòng không tô vàng) ở cả Material List và Assembly List.
- Nếu vẫn thấy thiếu border ở đâu đó dù đã đổi sang "medium" — đây sẽ là bằng chứng RÕ RÀNG cho thấy có vấn đề khác (không phải cảm quan), cần gửi lại file để tôi soi tiếp bằng OpenXml SDK.
- Cân nhắc về lâu dài: nếu muốn Assembly List tra cứu chéo được cả trường hợp 1* tham chiếu sang 1* khác (assembly lồng assembly), cần nâng cấp logic tra cứu lên nhiều cấp (đệ quy) — hiện chưa làm vì chưa được yêu cầu.

## Session 2026-07-06 16:07

### Đã làm

- User điều chỉnh 2 điểm so với session trước:
  1. Bỏ tổng theo từng bản vẽ (section) ở sheet "Assembly List" — chỉ giữ **1 dòng TOTAL/KG duy nhất ở cuối toàn bộ sheet** (đúng bố cục file mẫu .xlsm — chỉ có 1 dòng tổng ở cuối, không có tổng phụ theo từng bản vẽ).
  2. Kẻ All-border cho **toàn bộ phạm vi bảng dữ liệu**, kể cả ô trống (không chỉ ô có giá trị) — để bảng có viền lưới liền mạch giống bảng tính thật.
- `Utilities/MinimalXlsxWriter.cs`: thêm `SheetData.FullBorderStartRow`/`FullBorderColumnCount` — từ hàng này trở đi (đến hết dữ liệu), MỌI ô trong phạm vi cột đều được ghi ra (kể cả rỗng) để có viền. Áp dụng cho cả chế độ `Headers+Rows` (BOM/Warnings — bỏ điều kiện skip ô rỗng) và `GridRows` (Material List/Assembly List).
- `Utilities/BomTemplateExporter.cs` — `BuildAssemblyListSheet`: thay vì thêm dòng tổng NGAY sau mỗi section, giờ MỖI section chỉ gom 1 "số hạng công thức" (`SUM(itemRange)*$E$sectionHeader`) vào danh sách theo từng cột (`termsPerColumn`). Sau khi duyệt hết TẤT CẢ section, mới thêm 1 dòng TOTAL/KG DUY NHẤT ở cuối sheet, mỗi cột = nối các số hạng bằng dấu `+` (vd `SUM(G14:G14)*$E$13+SUM(G17:G17)*$E$16`) — vẫn giữ đúng việc mỗi bản vẽ nhân với Qty riêng của nó trước khi cộng vào tổng chung.
- Build thành công → `CSSCADTools_20260706_1607.dll`.
- Sanity-check ngoài AutoCAD với 2 bản vẽ 1* (1215 Drw→4050 Qty=2, 1225 Drw→5800 Qty=3) CÙNG đóng góp vào 1 cột AH36/15.0: kết quả dòng tổng cuối sheet = `SUM(G14:G14)*$E$13+SUM(G17:G17)*$E$16` = 39.4 (= 12.4 + 27.0, khớp chính xác tính tay). Xác nhận mọi ô trong phạm vi bảng (kể cả ô trống) đều có `s=` (border), không còn khoảng trống không viền.

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ cả 2 điều chỉnh bằng dữ liệu mô phỏng nhiều section. Chưa test lại full 13 file trong AutoCAD, và chưa tự mở Excel thật để xác nhận công thức tính đúng khi sửa Qty.

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260706_1607.dll`, chạy lại export trên toàn bộ 13 file, mở bằng Excel thật:
  - Xác nhận sheet Assembly List chỉ có 1 dòng TOTAL/KG duy nhất ở cuối (không còn tổng phụ theo từng bản vẽ).
  - Kiểm tra viền bảng phủ kín toàn bộ vùng dữ liệu (kể cả ô trống) trên cả 2 sheet Material List và Assembly List.
  - Thử sửa 1 ô Qty tô vàng, xác nhận dòng TOTAL/KG cuối cùng tự động cập nhật đúng.

## Session 2026-07-06 15:55

### Đã làm

- User yêu cầu 5 thay đổi lớn cho tính năng Export BOM/Material List:
  1. Tách bản vẽ đầu "1*" (fitting/tổng lắp chính, vd 1215/1225/1235) ra sheet riêng, tách biệt với bản vẽ chi tiết/vật tư thô (4xxx/5xxx).
  2. Tổng hợp vật tư cho sheet "1*" bằng TRA CỨU CHÉO: item nào có Drw trỏ sang 1 bản vẽ khác trong lô thì lấy TỔNG khối lượng theo từng cột Grade+Thickness của bản vẽ đó, NHÂN với Qty của chính item đó (vd LOCATOR Drw=4050, Qty=2, bản vẽ 4050 có AH36/15.0=6.2 → LOCATOR nhận 6.2*2=12.4 ở cột AH36/15.0).
  3. Ở sheet "1*", giao giữa cột Q'ty (E) và hàng Drawing No: tô vàng, mặc định =1 (người dùng tự sửa số bản vẽ cần dùng thực tế).
  4. Cột TOTAL/KG dời xuống HÀNG CUỐI mỗi section, dùng CÔNG THỨC Excel thật `=SUM(cột vật liệu các item)*Qty đầu section` — tự tính lại khi người dùng sửa Qty.
  5. All-border cho mọi ô có giá trị ở TẤT CẢ sheet.
- Mở rộng `Utilities/MinimalXlsxWriter.cs`: thêm `borders` (viền mảnh 4 cạnh, áp dụng cho toàn bộ 3 cellXfs hiện có), thêm `GridRow.NumberCells` (ô số thực, cần thiết để tham chiếu trong công thức — ô text `inlineStr` KHÔNG dùng được trong phép tính Excel), `GridRow.Formulas` (ghi `<f>` + `<v>` cache), `GridRow.HighlightedCols` (tô vàng RIÊNG 1 ô cụ thể, ghi đè Bold/Highlight của cả hàng — dùng cho ô Qty=1). Thêm `calcPr fullCalcOnLoad="1"` vào workbook.xml để Excel luôn tính lại công thức khi mở file (không có calcChain.xml).
- Viết lại hoàn toàn `Utilities/BomTemplateExporter.cs`: `Build()` giờ trả về `List<SheetData>` thay vì 1 sheet — tách "Material List" (bản vẽ không phải 1*, giữ nguyên hành vi cũ) và "Assembly List" (bản vẽ 1*, logic tra cứu chéo + Qty header + total công thức ở cuối section). Cả 2 sheet dùng CHUNG 1 danh sách cột Grade+Thickness để cùng ý nghĩa cột giữa 2 sheet.
- `BomExcelExporter.cs`: đổi `sheets.Add(...)` → `sheets.AddRange(...)` do `Build()` trả về list.
- Build thành công → `CSSCADTools_20260706_1555.dll`.
- Sanity-check ngoài AutoCAD bằng ĐÚNG ví dụ user đưa ra: drawing 4050 (PLATE 15.0x500x193, AH36, Qty=2, Weight=6.2) + drawing 1215 (LOCATOR, Drw=4050, Qty=2; TRIANGLE PIECE, Drw=5800 không có trong lô). Kết quả:
  - Sheet "Assembly List" row 13: A13="1215"(đậm), E13=1 (numeric, RIÊNG ô này tô vàng, không đậm) — đúng yêu cầu #3.
  - Row 14 (LOCATOR): cột AH36/15.0 = **12.4** — khớp CHÍNH XÁC ví dụ user (6.2*2=12.4).
  - Row 15 (TRIANGLE PIECE, Drw=5800 không khớp file nào) → tô vàng cả hàng (đúng, không tra cứu chéo được).
  - Row 16 (TOTAL, cuối section): công thức thật `SUM(G14:G15)*$E$13` = 12.4, `SUM(H14:H15)*$E$13`=32.8, tổng `SUM(G16:I16)`=54.6 — đúng yêu cầu #4.
  - Toàn bộ 6 XML part hợp lệ; `styles.xml` xác nhận cả 3 cellXfs đều dùng `borderId="1"` (all-border) — đúng yêu cầu #5.

### Trạng thái

- Code xong, build xong, đã tự-test đầy đủ cả 5 yêu cầu bằng dữ liệu mô phỏng đúng ví dụ thật. Chưa test lại full 13 file trong AutoCAD, và CHƯA tự mở bằng Excel thật để xác nhận công thức tính đúng khi người dùng sửa ô Qty (chỉ xác nhận cấu trúc XML formula đúng cú pháp).

### Bước tiếp theo

- NETLOAD `CSSCADTools_20260706_1555.dll`, chạy lại export trên toàn bộ 13 file, mở file `.xlsx` bằng Excel thật:
  - Kiểm tra 2 sheet "Material List" + "Assembly List" tách đúng theo tiền tố "1".
  - Thử sửa ô Qty tô vàng ở sheet Assembly List (vd đổi 1215 từ 1 → 2), xác nhận dòng TOTAL/KG cuối section tự động nhân đôi.
  - Kiểm tra viền bảng (all-border) hiển thị đúng trên tất cả sheet.

## Session 2026-07-06 08:58

### Đã làm

- User yêu cầu mở rộng chức năng Export BOM để đọc được cả file `.dwg` (trước đây chỉ đọc `.dxf`).
- Đổi tên `Utilities/BomDxfScanner.cs` → `Utilities/BomFileScanner.cs` (xóa file cũ), thêm dispatch theo đuôi file: `.dwg` → `db.ReadDwgFile(...)` (cùng pattern đã dùng ổn định trong `DwgDatabase.cs`), `.dxf` → `db.DxfIn(...)` như cũ. Cơ chế retry 3 lần vẫn áp dụng cho cả 2 loại.
- Cập nhật `Views/MainPaletteControl.xaml.cs` (`BtnExportBom_Click`): quét cả `*.dxf` và `*.dwg` trong thư mục (gộp 2 danh sách), đổi text thông báo/prompt cho đúng phạm vi DWG/DXF.
- Build thành công → `CSSCADTools_20260706_0858.dll`.
- Không có sẵn file `.dwg` mẫu chứa block `SW_TABLEANNOTATION_*` trong bộ dữ liệu test hiện tại nên KHÔNG tự kiểm chứng end-to-end được cho đường dẫn DWG — chỉ dựa vào việc tái sử dụng đúng pattern `ReadDwgFile` đã chạy ổn định ở tính năng "CHECK DETAILS".

### Trạng thái

- Code xong, build xong. Đường dẫn đọc DXF không đổi logic, không cần test lại. Đường dẫn đọc DWG CHƯA được test thật với file mẫu.

### Bước tiếp theo

- User cần tự test với ít nhất 1 file `.dwg` thật có chứa block `SW_TABLEANNOTATION_*` (NETLOAD `CSSCADTools_20260706_0858.dll`, để chung 1 thư mục với các file dxf hoặc thư mục riêng, bấm "EXPORT BOM TO EXCEL") để xác nhận đường dẫn ReadDwgFile hoạt động đúng như DxfIn.

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
