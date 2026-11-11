# Compare Project Ver1 — Revit 2025

## Cập nhật ghép hình học dự phòng (12/11/2026)

Bật mặc định **Ghép hình học khi không có GUID dùng chung**. Có thể tắt để chỉ ghép bằng GUID. Có thể để trống parameter nguồn khi bật dự phòng. Ưu tiên GUID chính xác; cặp GUID đã xác định được giữ trước kể cả hình học khác nhiều. GUID trùng báo **Cần xác nhận cặp**, không bị bỏ qua bằng ghép hình học.

Các host chưa có cặp GUID mới được tìm ứng viên hình học trong những IFC chưa được dành cho cặp GUID. Nếu GUID hai bên không dùng chung (ví dụ exporter khác tạo ID riêng) cũng có thể dùng dự phòng. So cùng Duct/Pipe, cùng dạng tiết diện tròn/chữ nhật, khoảng cách hai tâm 3D, chênh chiều dài, tiết diện và góc hai trục. Giới hạn mặc định: **1000 mm khoảng cách tâm, 10 mm tiết diện, 100 mm chiều dài, 5° góc trục**. Có thể sửa ở mục mở rộng trong cấu hình. Các giới hạn này độc lập với dung sai kết luận lệch.

Chỉ tự nhận khi host có đúng một ứng viên và ứng viên đó cũng chỉ có đúng một host. Nếu nhiều ứng viên hoặc hai host cùng cạnh tranh IFC, báo **Cần xác nhận cặp**, để Linked ID và sai số trống; ghi danh sách Linked ứng viên và các host cạnh tranh. Không tự chọn phần tử gần nhất, không phá hòa bằng điểm số, không loại ứng viên lần lượt để ép thành cặp duy nhất. Quyết định không phụ thuộc thứ tự duyệt.

Bảng/CSV có thêm Cách ghép (`GUID` / `Geometry`), Linked ứng viên, lý do và giới hạn tìm cặp. Dòng mơ hồ chỉ là báo cáo để người dùng xem xét; bản này chưa có thao tác xác nhận thủ công và lưu cặp. Nếu không có ứng viên trong giới hạn thì `NoGeometryCandidate`; không đủ hình học thì `GeometryUnsupported`. Không hỗ trợ ghép một–nhiều, fittings, mesh hoặc suy luận hệ thống/tầng chưa có dữ liệu. Một cặp duy nhất trong vùng tìm kiếm vẫn là suy luận hình học, cần xem cách ghép khi đánh giá kết quả.

Trong view/selection, việc tìm cặp xét toàn bộ host để giữ các cặp GUID/ứng viên ngoài phạm vi; bảng chỉ trả host trong phạm vi. Cách này tránh tự ghép nhầm khi host cạnh tranh đang ngoài selection/view.

DLL Debug đang chạy trong Revit bị khóa nên bản cập nhật được build sang **`bin\Debug\GeometryFallback\MEP_Compare_Project_ver1.dll`**. Load bản này trong Add-In Manager → Manual. Command vẫn là `MEP_Compare_Project_ver1.Command.CompareCommand`.

```powershell
dotnet build MEP_Compare_Project_ver1.csproj -c Debug --no-restore -p:OutputPath=bin\Debug\GeometryFallback\
dotnet Verification\bin\Release\net8.0-windows\Verification.dll --logic-only
dotnet Verification\bin\Release\net8.0-windows\Verification.dll --headless-ui
```

`--logic-only` không mở cửa sổ. `--headless-ui` kiểm tra và render WPF trong bộ nhớ, không mở cửa sổ và không điều khiển chuột/bàn phím. Quy tắc ghép/kiểm chứng ban đầu dưới đây được bổ sung bởi phần cập nhật này.

Thư viện C# .NET 8 Windows x64, chạy thủ công qua Add-In Manager. Entry point:

`MEP_Compare_Project_ver1.Command.CompareCommand`

## Build và chạy

```powershell
dotnet build MEP_Compare_Project_ver1.csproj -c Release -p:Platform=x64 -p:RestoreConfigFile=NuGet.Config
```

DLL tương đối với thư mục project:

`bin\x64\Release\net8.0-windows\MEP_Compare_Project_ver1.dll`

Project tham chiếu `RevitAPI.dll` và `RevitAPIUI.dll` tại `C:\Program Files\Autodesk\Revit 2025`. Cả hai có `Private=false`, không copy vào output. Có thể truyền `-p:RevitInstallDir="đường dẫn cài đặt"`. Project không dùng package bên ngoài. `NuGet.Config` không khai báo nguồn package; cần có sẵn targeting pack .NET 8 và Windows Desktop trên máy build.

1. Mở project trong Revit 2025; IFC đã được link và loaded.
2. Add-In Manager → Manual → Load DLL ở đường dẫn trên.
3. Chọn `MEP_Compare_Project_ver1.Command.CompareCommand` → Run.
4. Chọn đúng **link instance** (mỗi instance có transform riêng), Ducts/Pipes, phạm vi và parameter chứa GUID nguồn. Mặc định `test` (tên thực tế đã xác nhận qua MCP); có thể nhập shared parameter GUID để tránh trùng tên. Nhãn `Revit.test` vẫn được chấp nhận làm alias khi không có parameter tên đầy đủ đó.
5. Nhập dung sai theo mm và độ rồi Compare. Mặc định 1 mm / 0,1° là giá trị khởi đầu, có thể thay đổi.
6. Bảng có tìm theo ID/GUID/Tag, lọc trạng thái, sắp xếp cột và thanh cuộn ngang. CSV xuất những dòng đang lọc, kèm cấu hình và đơn vị.
7. Chọn dòng rồi Select host, Select linked hoặc Zoom cặp. Cửa sổ đóng trước khi thực hiện trong `Execute`. Chạy lại command để xem bảng lần nữa. Zoom không mở view mới, không đổi section box/crop; phần tử đang bị ẩn hoặc ngoài phạm vi view có thể vẫn không nhìn thấy. Nên thử trong view 3D hiện có phù hợp.

Không có file `.addin`, ribbon, bộ cài, pyRevit, modeless hoặc ExternalEvent.

## Ghép cặp và phạm vi

- Đọc instance parameter nguồn của native MEPCurve, ghép với instance `IfcGUID` trong link (nhãn nhóm `IFC.IfcGUID`). Ưu tiên tên chính xác; nếu không tồn tại thì ba alias tường minh `Revit.test`, `IFC.IfcGUID`, `IFC.IfcTag` lần lượt tra `test`, `IfcGUID`, `IfcTag`. Không tự bỏ tiền tố bất kỳ hoặc tự dùng host IfcGUID thay test. Trim khoảng trắng; so sánh chính xác, phân biệt hoa/thường. Không chuyển đổi GUID 36 ký tự thành IFC compressed GUID và không ghép gần đúng theo hình học/ID/IfcTag.
- GUID trống, không tìm thấy, GUID trùng ở host hoặc IFC đều có trạng thái riêng. Trùng GUID không tự chọn ứng viên; danh sách ID nằm trong ghi chú.
- Native chỉ lấy duct/pipe curves, không fittings, accessories, insulation hoặc equipment. Linked nhận category DuctCurves/PipeCurves hoặc `IFC.IfcExportAs` / `IfcEntity` bắt đầu bằng IfcDuctSegment/IfcPipeSegment.
- WholeModel: tất cả native mục tiêu trong host và các dòng chỉ có ở IFC thuộc nhóm được chọn. ActiveView/Selection: giới hạn host, vẫn tra toàn bộ link để tìm GUID; không liệt kê phần tử chỉ có ở IFC ngoài phạm vi host. Selection được chụp lúc bắt đầu command; phải chọn native trước khi Run.
- Link chưa loaded không chạy được. Tên có `.ifc` chỉ là gợi ý nhận diện trong danh sách, không được dùng thay cho việc đọc dữ liệu IFC.
- Nếu parameter trùng tên, dừng và yêu cầu shared parameter GUID để tránh đọc nhầm. Parameter nguồn phải là kiểu String.

## Hình học và phân loại

- Tất cả Revit API chạy đồng bộ trên ngữ cảnh command. Models/Matching/Comparison/Reporting không phụ thuộc Revit API.
- Native: `LocationCurve` phải là Line; tiết diện và hướng X lấy từ end connector. Phải có hai end connectors với tiết diện đồng nhất.
- IFC có MEPCurve: dùng cùng cách trên. IFC dạng solid: duyệt GeometryInstance với transform tích lũy, hỗ trợ một solid hình trụ tròn đồng trục (đặc hoặc rỗng) hoặc lăng trụ chữ nhật đặc có sáu mặt phẳng trực giao. Kiểm tra thể tích để loại khối bị cắt/biến dạng.
- **Giới hạn suy luận:** lăng trụ chữ nhật không có đường tâm có nghĩa về mặt ngữ nghĩa. Ver1 dùng trục dài nhất duy nhất và ghi `unique longest axis inferred` trong Notes. Đoạn ngắn hơn bề rộng/bề cao có thể bị suy ra sai trục; cần kiểm tra Notes. Khối có hai cạnh dài tương đương bị đánh dấu không hỗ trợ.
- Mesh, đường cong, oval, nhiều solid, khối phức hợp, rectangular hollow solid hoặc hình không được xác nhận không được so sánh bằng bounding box: trả `GeometryUnsupported`. Ô sai số trống là chưa tính được, không phải 0.
- GeometryInstance transform áp dụng trước; `RevitLinkInstance.GetTotalTransform()` áp dụng đúng một lần để sang internal coordinates của host. Không cộng thêm survey/shared offset. Link có scale khác 1 không hỗ trợ.
- Chuyển feet → mm. ΔX/Y/Z = **tâm IFC đã transform − tâm native**, theo trục internal host. Đây không nhất thiết là trục project north hoặc survey.
- MaxEndpoint = giá trị nhỏ hơn giữa hai phương án ghép đầu–cuối thuận/ngược; mỗi phương án lấy sai số lớn nhất trong hai đầu. So chiều dài, góc trục không phân biệt chiều, tiết diện và xoay tiết diện chữ nhật. Đổi width/height đồng thời xoay 90° được xem là tương đương.
- Chỉ `Match` khi mọi sai số trong dung sai; sai số vượt dung sai được liệt kê cùng nhau. Khác loại Duct/Pipe hoặc khác dạng tiết diện có trạng thái riêng.
- Bounding box chỉ dùng để Zoom và đã xét transform cả tám góc; không dùng làm số đo so sánh.

## Chỉ đọc model

Entry point có `[Transaction(TransactionMode.Manual)]` cho Add-In Manager. Không có Transaction/TransactionGroup/SubTransaction, không gọi Set parameter, tạo/xóa/move element, đổi view hoặc Save. Chỉ thay đổi lựa chọn và mức zoom của UI khi người dùng yêu cầu. CSV ghi ra file người dùng chọn.

## Kiểm chứng

```powershell
dotnet build Verification\Verification.csproj -c Release -p:RestoreConfigFile=NuGet.Config
dotnet Verification\bin\Release\net8.0-windows\Verification.dll
```

Verification liên kết trực tiếp Models/Matching/Comparison/Reporting/UI; không tham chiếu Revit API và không mở model. Tạo dữ liệu giả để kiểm tra ghép cặp, sai số, CSV và WPF modal, lưu ảnh tại `Verification\artifacts`. Dữ liệu giả không được dùng trong DLL sản phẩm.

Kết quả thực hiện xem `VERIFICATION.md`. Geometry extraction, link transform trong Revit, Add-In Manager và lựa chọn/zoom thực cần kiểm chứng trên Revit; bộ kiểm tra độc lập không thay thế được các bước đó.
