# Kết quả kiểm chứng — 11/11/2026

## Đã xử lý tham chiếu API và build chạy thử Revit 2025.5 — 12/11/2026

- Nguyên nhân CS1705: csproj đặt .NET 8 trong khi Revit cài trên máy là **2025.5 / 25.5.0.57**, đã chuyển sang .NET 10. Đã xác minh FileVersion của Revit.exe/RevitAPI/RevitAPIUI và runtimeconfig RevitNET/RevitAPI/AddInManager. [Release notes Autodesk](https://help.autodesk.com/cloudhelp/2025/ENU/RevitReleaseNotes/files/2025updates/RevitReleaseNotes_2025updates_2025_5_html.html) xác nhận cùng build và .NET 10.
- Đã đổi TargetFramework sản phẩm và Verification sang net10.0-windows. Giữ API tham chiếu từ bản cài Revit với Private=false; không thay DLL Autodesk hoặc sửa bản cài.
- Build Release x64 chính thức: **PASS, 0 warnings/errors**. DLL: `bin/x64/Release/net10.0-windows/MEP_Compare_Project_ver1.dll`. Không kèm Autodesk API trong output.
- Build Verification .NET 10: **PASS, 0 warnings/errors**. Chạy --headless-ui: **70/70 PASS**, không mở cửa sổ. QA artifacts: `Verification/bin/qa/Verification/artifacts`.
- Gói chạy thử: `bin/MEP_Compare_Revit2025.5_Mapping.zip`, gồm DLL, deps.json, PDB và hướng dẫn. SHA256 DLL: `5F7D30A415844756B3E82E6C52DB3573E766690FFD452AD5A9279AF434CD9A96`.
- Chưa thử command trong Revit 2025.5 thật. Lúc kiểm tra chỉ thấy hai tiến trình Revit 2023; không khởi động, đóng hay điều khiển các phiên đó.
- Các nhận định bên dưới về việc phải tìm API .NET 8 hoặc không được load .NET 10 vào Revit 2025 đã được thay thế: cần phân biệt Revit 2025.0–2025.4 (.NET 8) và 2025.5 (.NET 10). Dùng bản Release mới ở trên.

## Xác nhận cặp thủ công và mapping JSON — 12/11/2026

- Build Verification Release .NET 8: PASS, 0 warnings/errors.
- Chạy bộ kiểm tra với --headless-ui: **70 kiểm tra PASS** (64 logic/CSV và 6 WPF). Có thêm 16 kiểm tra mapping: ưu tiên thủ công trước GUID, dành IFC duy nhất, vẫn đo sai số, chặn tranh chấp một–một, lưu/nạp JSON, giữ mapping khi nạp file lỗi, đổi ElementId số nhưng giữ UniqueId, sai model/link, phần tử đã mất, bỏ cặp và hình học không hỗ trợ.
- Render và xem hai ảnh giao diện mới tại Verification/bin/qa/Verification/artifacts/results-manual-mapping.png và manual-pair-picker.png. Giao diện kết quả có đủ nút xác nhận/bỏ/lưu/nạp; hộp chọn IFC hiển thị danh sách cùng loại. Không mở cửa sổ, không điều khiển chuột/bàn phím.
- Lệnh tái chạy từ thư mục project: dotnet Verification/bin/Release/net8.0-windows/Verification.dll --headless-ui. Lần QA này chạy với working directory Verification/bin/qa để không ghi đè các artifact cũ đã commit.
- Build sản phẩm theo cấu hình gốc net8.0-windows vẫn bị CS1705: bộ RevitAPI/RevitAPIUI hiện tại (25.5.0.0) tham chiếu System.Runtime 10.0.0.0. Đây là lỗi có sẵn trước thay đổi mapping; chưa tạo được DLL .NET 8 dùng cho Revit 2025 từ bộ tham chiếu hiện tại.
- Để kiểm tra biên dịch cả Command và ModelReader, đã override TargetFramework=net10.0-windows chỉ trong lệnh restore/build: PASS, 0 warnings/errors. Output ở bin/CompatibilityCheck; **đây chỉ là bản kiểm tra biên dịch, không dùng để load vào Revit 2025**. TargetFramework trong csproj vẫn giữ net8.0-windows. Cần bộ DLL API tương thích .NET 8 để build bản triển khai.
- Chưa chạy xác nhận/lưu/nạp trong Revit thật; chưa xác minh độ ổn định UniqueId của IFC sau reload từ exporter CAD cụ thể. Mapping từ chối phần tử bị thay UniqueId, không đoán lại cặp. Không thay đổi model, parameter, view hoặc bản cài Revit.


## Ghép hình học dự phòng — 12/11/2026

- Build Debug mới sang `bin\Debug\GeometryFallback\MEP_Compare_Project_ver1.dll`: 0 errors, 0 warnings. DLL Debug ở đường dẫn cũ bị Revit khóa; không ghi đè, không dừng Revit.
- 48 kiểm tra logic/CSV: PASS, chạy với `--logic-only`, không mở cửa sổ. Bao gồm GUID ưu tiên, giữ cặp GUID ngoài bán kính tìm, chặn GUID trùng, ứng viên hai chiều duy nhất, không chọn gần nhất, chuỗi ứng viên mơ hồ, độc lập thứ tự, ngưỡng khoảng cách/size/length/angle, ống đứng, đổi chiều đầu–cuối, không hỗ trợ/NaN, tắt fallback và cạnh tranh ngoài phạm vi báo cáo.
- Tổng **52 kiểm tra đạt** khi thêm `--headless-ui`: dựng/render WPF trong bộ nhớ, kiểm tra checkbox mặc định, cột cách ghép/ứng viên, lọc theo danh sách ứng viên và dòng chưa xác nhận không có Linked ID. Không mở cửa sổ. Ảnh tại `Verification/artifacts/configuration-fallback*.png` và `results-fallback.png`.
- MCP pyRevit `run_query`, run id **2b2d628661c2**, status ok: đã gọi **DLL mới** với dữ liệu thật. 927298/927300/927304 vẫn Match bằng GUID; 927319/927364 đều Cần xác nhận cặp vì DuplicateGuid, ứng viên 323434. Bản cập nhật không dùng hình học để bỏ qua trùng GUID.
- Đã mô phỏng không có GUID bằng cách tạo bản sao **ElementData độc lập trong bộ nhớ**, xóa chuỗi GUID trên DTO, không xóa hay Set parameter model. Thuật toán ghép hình học nhận đúng 927298→323421, 927300→323424, 927304→323448 (Match), 927319→323434 (Different: Position, ΔX −618,4968686722359 mm). 927364 không có ứng viên trong giới hạn. Đây là kiểm tra trên hình học Revit thật với ID bị lược bỏ trong dữ liệu trung gian, chưa phải file xuất CAD MEP riêng.
- Các linked pipe mẫu 323407/323438/323440/323446 bị bộ đọc hiện có báo GeometryUnsupported (solid trụ không qua kiểm tra hình dạng/thể tích); không đoán đường tâm hoặc ghép bằng bounding box. Chưa khẳng định hỗ trợ hình học pipe từ mọi exporter CAD MEP.
- Trạng thái host IsModified=true có sẵn trước truy vấn; linked IsModified=false. Không transaction hay sửa/lưu model, không UI automation. Không thử cửa sổ IExternalCommand hoặc Zoom/select trong Revit cho bản mới.
- Kết quả thô: `Verification/artifacts/revit-geometry-fallback-test.json`.

## Cập nhật qua MCP pyRevit — 12/11/2026

Đã kết nối đúng MCP pyRevit bằng `pyrevit.exe mcp` qua stdio; connector `revit` cũ là server khác, không kết nối với pyRevit Agent Host. Không dùng điều khiển chuột/bàn phím. Model active `Project_IFC_2025`, Revit 2025 build 25.4.60.9, IFC link instance 927237 (`Project3.ifc : 1`).

Đã xác nhận tên parameter thực: host `test`, linked `IfcGUID`/`IfcTag`. Bản đầu dùng nhãn có tiền tố nhóm như tên parameter nên đọc trống. Đã thêm alias tường minh có ưu tiên tên chính xác và đổi mặc định UI thành `test`.

Đã nạp DLL Debug mới và gọi trực tiếp các hàm sản phẩm ModelReader.Read → GuidMatcher.Match → ComparisonService.Compare trong ngữ cảnh Revit thông qua MCP run_query. Không chạy IExternalCommand/modal UI hoặc Zoom/select trong lần thử này.

Kết quả lần đọc cuối cùng (dung sai 1 mm / 0,1°):

| Host | Linked | IfcTag | Kết quả |
|---|---|---|---|
| 927298 | 323421 | 924417 | Match; max endpoint khoảng 4,55e-13 mm |
| 927300 | 323424 | 924456 | Match; max endpoint khoảng 4,55e-13 mm |
| 927304 | 323448 | 924466 | Match; max endpoint khoảng 1,11e-12 mm |
| 927319 | không tự chọn | — | DuplicateGuid, trùng với host 927364; ứng viên linked 323434 |
| 927364 | không tự chọn | — | DuplicateGuid, trùng với host 927319; ứng viên linked 323434 |

Có thêm 4 dòng OnlyInIfc cho Pipes, không có host tương ứng trong nhóm native được đọc. Không bỏ phân biệt hoa/thường để ép khớp GUID. Snapshot GUID của 927304 ở lần truy vấn trước khác lần kiểm tra cuối; kết luận trên dựa vào lần cuối, không suy đoán nguyên nhân thay đổi snapshot.

Kiểm tra hình học riêng, tách khỏi quyết định ghép tự động: 927319 ↔ 323434, IfcTag 924409, ΔX = **−618,4968686722359 mm** theo IFC − Host; ΔY/ΔZ xấp xỉ 0. Đây là xác nhận hình học của cặp mẫu, không bỏ qua lỗi DuplicateGuid trong báo cáo sản phẩm.

Revit host đã có `IsModified=true` ngay trước kiểm tra; chỉ truy vấn, không tạo transaction hay sửa/saved model. IFC linked document ở snapshot đầu `modified=false`. MCP run_query báo `status: ok`. Hai lần trả kết quả trước gặp lỗi encode tiếng Việt của IronPython; script kiểm chứng đã đổi ghi chú trả về sang ASCII escaped, không ảnh hưởng DLL hay model.

Kết quả thô: `Verification/artifacts/revit-product-test.json`, MCP run id cuối `dad34895b03a`.

Các mục chưa thử dưới đây mô tả trạng thái **trước khi có kết nối pyRevit**. Hiện phần đọc/ghép/hình học đã được thử thực; UI Add-In Manager của DLL mới và Zoom/select vẫn cần người dùng thử.

## Kiểm chứng ban đầu

- Build Release x64 .NET 8 Windows: **PASS — 0 warnings, 0 errors**.
- Output: `bin\x64\Release\net8.0-windows\MEP_Compare_Project_ver1.dll`.
- Output không có RevitAPI.dll hoặc RevitAPIUI.dll; chỉ DLL sản phẩm, PDB và deps.json.
- 22 kiểm tra độc lập bằng dữ liệu giả: **PASS**. Bao gồm hai chiều đầu-cuối, translation có dấu, chiều dài/hướng/tiết diện/roll, GUID trùng/trống/phân biệt hoa-thường, phạm vi, UTF-8 BOM/CSV escaping, WPF configuration modal, bảng kết quả, lọc và capture yêu cầu Select trước khi modal kết thúc.
- Đã render và xem ảnh cấu hình/bảng kết quả bằng dữ liệu giả. Cột trạng thái đã được nới rộng sau khi xem ảnh để đọc đầy đủ trạng thái thông dụng.
- Không có ID mẫu model trong mã sản phẩm. Fixture translation của bộ kiểm tra độc lập chỉ kiểm tra công thức; không chứng minh dữ liệu model đúng.

## Những bước thực tế trong Revit

- Đã gọi thử connector với chế độ `transactionMode: none`, chỉ yêu cầu đọc document/link/ID mẫu. Connector trả `Connect to Revit client failed`; không thu được dữ liệu model.
- Kiểm tra tiến trình không thấy Revit đang chạy tại thời điểm triển khai. Không khởi chạy Revit, không mở/sửa/lưu model.
- **Chưa chạy command qua Add-In Manager.** Chưa xác minh extraction hình học IFC, transform link, chọn link thực, Compare thực, lọc dữ liệu thực hoặc Zoom/select trong Revit.

## Các bước người dùng cần thử trong Revit 2025

1. Mở model và link IFC đã loaded; dùng view 3D có sẵn phù hợp. Không cần sửa hoặc lưu model.
2. Add-In Manager → Manual → Load DLL → chọn `MEP_Compare_Project_ver1.Command.CompareCommand` → Run.
3. Chọn link, Ducts/Pipes và `Revit.test`; nhập dung sai mong muốn → Compare.
4. Kiểm tra host 927298, 927300, 927304 có còn khớp IFC hay không.
5. Kiểm tra host 927319 ghép linked ID 323434 và IfcTag 924409, độ lệch X có độ lớn khoảng 618,50 mm. Dấu trong bảng theo IFC − Host; nếu host lệch +X so với IFC thì ΔX trong bảng là âm.
6. Kiểm tra Notes chỉ rõ cách lấy hình học; xem các dòng GeometryUnsupported/duplicate/missing thay vì diễn giải ô trống là khớp.
7. Thử lọc theo ID/trạng thái, xuất CSV, Select host/linked và Zoom cặp. Cửa sổ phải đóng trước thao tác Revit.

Không tạo `.addin`, không cài add-in chính thức và không thay đổi model để kiểm thử.
