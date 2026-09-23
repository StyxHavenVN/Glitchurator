# Glitchurator

**Công cụ mở rộng các chức năng Slider Picturator và Sliderball của Mapping Tools cho osu!, phát triển bởi Styx (StyxHavenVN).**

Glitchurator giúp ghép slider và ảnh thành một hình, tạo hiệu ứng glitch, chỉnh đường đi của sliderball và xuất kết quả vào beatmap `.osu`.

Giao diện ứng dụng sử dụng **tiếng Anh**. Hướng dẫn này giữ nguyên tên các nút để bạn dễ tìm.

## Tính năng

- Nhập một hoặc nhiều slider từ osu! editor bằng clipboard.
- Nhập PNG, ghép nhiều lớp và quản lý bằng Show / Hide, Duplicate, Remove.
- Tạo thân chuyển sắc, viền native và glitch scanline nhiều tầng.
- Xoay glitch riêng Body, Border hoặc Both.
- Cắt hình bằng chữ nhật, vuông, ellipse, tam giác, sao, trái tim và vùng vẽ tự do.
- Chỉnh chuyển động sliderball bằng graph thời gian/vị trí.
- Tạo multi-ball illusion và xuất slider ẩn thân.
- Dùng chung beatmap giữa các tab.

## Cài đặt

### Yêu cầu

- Windows 64-bit.
- osu! và beatmap `.osu` cần chỉnh sửa. Các kỹ thuật hiển thị đặc biệt hướng tới **osu! Stable**.
- Bộ cài **Glitchurator-Setup.exe**.

Bộ cài chứa sẵn runtime .NET và thư viện. Không cần cài từng thư viện riêng.

### Các bước

1. Mở `Glitchurator-Setup.exe`.
2. Làm theo cửa sổ cài đặt và bấm **Install**.
3. Bấm **Finish**, mở **Glitchurator** từ Desktop hoặc Start Menu.

## Bắt đầu nhanh

1. Lưu beatmap trong osu! editor.
2. Mở Glitchurator, bấm **Choose map…** tại **Shared beatmap .osu**, chọn đúng difficulty.
3. Trong osu! editor, chọn slider rồi nhấn **Ctrl+C**.
4. Quay lại ứng dụng, bấm **Load copied sliders**.
5. Chỉnh hình, glitch hoặc đường bóng trong preview.
6. Kiểm tra thời điểm bắt đầu, thời lượng và độ phân giải.
7. Bấm **INJECT SLIDER**.
8. Tải lại beatmap trong osu! để kiểm tra kết quả.

> **INJECT SLIDER ghi vào file `.osu` đã chọn.** Ứng dụng tạo bản sao `.bak` trước khi ghi. Nên thử trên một difficulty riêng.

## Nhập slider và ảnh

### Slider từ osu! editor

Chọn đúng file `.osu`, lưu map trong editor, chọn một hoặc nhiều slider và nhấn **Ctrl+C**. Sau đó bấm **Load copied sliders** hoặc **Import clipboard**.
### Ảnh PNG

- Bấm **Import PNG / images…** hoặc **Import images…** để chọn ảnh.
- Có thể nhập nhiều ảnh hoặc thả PNG vào danh sách thư viện.
- Ảnh không có timing riêng; kiểm tra thời điểm bắt đầu và thời lượng trước khi xuất.
- Giữ file ảnh nguồn tại đường dẫn đã nhập để mở lại phiên làm việc.

## Thư viện và preview

Danh sách **SLIDER & IMAGE LIBRARY** nằm bên phải.

| Thao tác | Tác dụng |
| --- | --- |
| Chọn một mục | Chỉnh lớp đó |
| Show / Hide | Đưa lớp vào hoặc loại khỏi preview và kết quả ghép |
| Item name | Đổi tên lớp |
| Duplicate | Nhân bản lớp và thiết lập |
| Remove | Xóa lớp khỏi thư viện |
| Kéo hình | Di chuyển lớp |
| Kéo ô vuông xanh / lăn chuột | Chỉnh tỷ lệ |
| Ctrl + lăn chuột | Chỉnh độ dày CS của hình |
| Play | Xem chuyển động |
| Fit view | Đưa nội dung về vừa vùng xem |

Các lớp đang hiện được ghép thành **một slider đầu ra**. Timing và thời lượng lấy từ lớp đang chọn. Đường bóng mặc định thuộc lớp đang chọn; multi-ball sử dụng nhiều đường hợp lệ đang hiện.

**Match osu! resolution automatically** hỗ trợ lấy độ phân giải osu!. Nếu không lấy được, nhập đúng độ phân giải dọc. Preview mô phỏng bố cục và sắc độ; skin và renderer trong osu! có thể cho kết quả khác.

## Native và glitch

**Native gradient body and border** tạo thân chuyển sắc và viền native. **Enable glitch** điều khiển glitch thông thường. **Layered scanline glitch** là chế độ riêng cho các tầng scanline trên thân và viền.

| Thiết lập | Tác dụng |
| --- | --- |
| Direction | Hướng vệt glitch; 0° là ngang |
| Displacement | Cường độ dịch chuyển |
| Overall density | Mật độ glitch tổng |
| Spike thickness | Độ dày vệt |
| Randomize glitch | Đổi mẫu ngẫu nhiên |
| Random rows | Bật hàng ngẫu nhiên; tắt để xếp hàng đều |
| Row spacing | Khoảng cách hàng |
| Tiers | Số tầng |
| Outer density / Middle density | Mật độ phần ngoài / giữa |
| Solid inner width | Điều chỉnh vùng lõi và ảnh hưởng lên thân |

### Xoay riêng thân và viền

Trong **Rotate layered glitch**, chọn **Body**, **Border** hoặc **Both**, rồi chỉnh **Direction**. Mỗi phần lưu góc riêng; xoay một phần sẽ giữ thiết lập của phần còn lại. Chọn Both sẽ đặt cùng góc cho cả hai.

## Cắt hình

1. Chuột phải vào lớp đang chọn trong preview → **Cut…**.
2. Cửa sổ **Cut editor** có vùng ảnh lớn ở trên, các công cụ ở dưới.
3. Chọn Rectangle, Square, Ellipse, Triangle, Star, Heart hoặc Freehand.
4. Kéo để tạo vùng chọn; kéo bên trong để di chuyển, kéo các góc để đổi kích thước.
5. Bấm **Cut selection** hoặc **Delete** để xóa phần **bên trong** vùng chọn.
6. Tiếp tục cắt nếu cần, rồi bấm **Done** để lưu.

**Freehand** tự nối điểm cuối với điểm đầu thành vùng kín.

- **Ctrl+Z / Undo:** bỏ vùng đang chọn; nếu không có vùng chọn, hoàn tác vết cắt gần nhất trong cửa sổ.
- **Reset:** bỏ các vết cắt tạm của lần chỉnh sửa này.
- **Esc:** bỏ vùng chọn.
- **Cancel:** bỏ thay đổi của lần mở cửa sổ này.
- Sau khi đóng cửa sổ, dùng **Undo cut** trong menu chuột phải preview để hoàn tác vết cắt đã lưu.

Cắt chỉ ảnh hưởng hình của lớp; đường đi sliderball không bị cắt theo.

## Sliderball và graph

### Đường bóng

1. Bật **Sliderball follows shape**.
2. Dùng bảng **Sliderball path** để chỉnh offset X/Y và tỷ lệ đường đi.
3. Kéo tiêu đề để di chuyển bảng.
4. **Shift + kéo**, kéo bóng hoặc cạnh khung vàng để dịch đường đi.
5. **Reset path alignment** đưa đường bóng về khớp hình.
6. Muốn dùng đường khác, sao chép một slider rồi chọn **Import separate path (Ctrl+C)**.

### Graph và segment

Mở **GRAPH — sliderball time / position…** hoặc **GRAPH / segment estimate…**.

- Trục ngang là thời gian; trục dọc là vị trí dọc đường đi.
- Thêm hoặc kéo mốc để thay đổi chuyển động.
- Kéo nút giữa đoạn để uốn đường cong; chuột phải để chọn kiểu đường cong.
- **Play** xem thử chuyển động.
- **Calculate segments** dùng cùng bộ tạo đường với phần xuất.
- Tăng **Minimum tumour length** thường giảm segment; giảm giá trị sẽ tăng segment. Đây không phải cam kết FPS.

### Multi-ball illusion

1. Nhập ít nhất hai slider hoặc chuẩn bị nhiều lớp có đường bóng hợp lệ.
2. Bật hiển thị và sliderball cho các lớp muốn dùng.
3. Bật **Multi-ball illusion**.
4. Chỉnh **Switch interval**, bắt đầu thử khoảng **1–4 ms**.
5. Xuất và kiểm tra trong osu!.

Đầu ra vẫn có **một sliderball thật**, luân phiên nhanh giữa các đường để tạo ảo ảnh nhiều bóng. Các vòng hướng dẫn trong preview không phải bóng thật được thêm vào game.

Tab Sliderball cho phép thử **0.1–32 ms**. Dưới 1 ms là thử nghiệm: số điểm tăng, có thể tăng lag hoặc bị FPS lấy mẫu bỏ qua. Giá trị nhỏ hơn không nhất thiết mượt hơn.

### Slider ẩn thân

Trong tab **Sliderball**, bấm **Export hidden-body slider**. Chế độ này không tạo scanline hình Picturator. Kiểm tra trong osu! Stable; đầu, đuôi hoặc follow-circle có thể vẫn hiện tùy skin.

## Xuất vào beatmap

Trước khi xuất, kiểm tra file `.osu`, các lớp hiện, lớp đang chọn, timing, thời lượng, độ phân giải và số segment.

**INJECT SLIDER** có thể thay thế slider ở thời điểm xuất, cập nhật timing cần thiết và gỡ các màu override `SliderTrackOverride` / `SliderBorder`. File `.bak` được cập nhật trước mỗi lần ghi, không phải lịch sử vô hạn.

Sau khi xuất, tải lại file trong osu!. Tránh lưu đè từ cửa sổ editor còn giữ nội dung cũ chưa được tải lại.

## Lưu và sao lưu dữ liệu

Dán đường dẫn sau vào thanh địa chỉ File Explorer:

```text
%LocalAppData%\StyxHavenVN\SliderPicturator
```

- `picturator_library.json`: thư viện lớp.
- `picturator_session.json`: phiên làm việc.
- File `.bak`: bản lưu trước đó.

Bấm **Save session** để lưu thủ công. Ảnh nguồn vẫn cần được giữ tại vị trí ban đầu.

Nếu cập nhật thấy thư viện trống, kiểm tra thư mục này và các bản sao lưu. Trong workspace phát triển, dữ liệu build cũ đã được giữ tại `artifacts/legacy-user-data`.

## Xử lý sự cố

| Hiện tượng | Cách kiểm tra |
| --- | --- |
| Không tìm được slider | Lưu map, chọn đúng difficulty, sao chép lại slider |
| Ảnh mất sau khi mở lại | Kiểm tra đường dẫn ảnh nguồn |
| Cập nhật thấy thư viện trống | Kiểm tra LocalAppData và bản sao lưu |
| Preview khác osu! | Kiểm tra độ phân giải, skin, renderer; xuất và tải lại map |
| Glitch quá rối | Giảm Displacement, mật độ hoặc độ dày; thử Direction 0° |
| Multi-ball nhấp nháy hoặc lag | Tăng switch interval, giảm số đường, tính lại segment |
| Không thấy file chạy | Dùng bộ cài; build kiểm thử có thể chỉ tạo DLL |
| Cắt không lưu | Bấm Done; Cancel bỏ thay đổi tạm |


## Tác giả và chia sẻ

**Styx — StyxHavenVN**

Glitchurator mở rộng các chức năng liên quan đến Slider Picturator và Sliderball của **Mapping Tools**. Mọi người được phép sao chép và chia sẻ phiên bản này trong phạm vi quyền đối với các đóng góp của Styx.

Mã nguồn, thư viện và tài nguyên bên thứ ba vẫn thuộc tác giả tương ứng và chịu giấy phép riêng. Glitchurator không đại diện cho sự chứng thực của nhóm phát triển Mapping Tools.
