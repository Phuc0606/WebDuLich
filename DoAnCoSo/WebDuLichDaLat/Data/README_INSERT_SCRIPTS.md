# 📚 HƯỚNG DẪN SỬ DỤNG CÁC FILE SQL INSERT

## 📋 Tổng quan

Thư mục này chứa các file SQL script để insert dữ liệu mẫu vào các bảng trong database.

## 📁 Danh sách các file SQL

### 1. **Insert_Categories.sql**
- **Mục đích:** Insert dữ liệu cho bảng `Categories` (Danh mục địa điểm)
- **Nội dung:** 3 danh mục chính: Khách sạn, Nhà hàng/Quán ăn, Địa điểm du lịch
- **Thứ tự chạy:** Nên chạy đầu tiên (bảng cơ bản)

### 2. **Insert_Regions.sql**
- **Mục đích:** Insert dữ liệu cho bảng `Regions` (Khu vực địa lý)
- **Nội dung:** 16 khu vực trong Đà Lạt
- **Thứ tự chạy:** Nên chạy sớm (bảng cơ bản)

### 3. **Insert_TransportOptions.sql**
- **Mục đích:** Insert dữ liệu cho bảng `TransportOptions` (Phương tiện vận chuyển)
- **Nội dung:** 5 phương tiện: Xe khách, Xe limousine, Xe máy cá nhân, Ô tô cá nhân, Máy bay
- **Thứ tự chạy:** Chạy trước `Insert_TransportPriceHistories.sql`

### 4. **Insert_TransportPriceHistories.sql**
- **Mục đích:** Insert dữ liệu cho bảng `TransportPriceHistories` (Giá vận chuyển theo tuyến)
- **Nội dung:** Giá vận chuyển từ các tỉnh/thành đến Đà Lạt
- **Lưu ý:** Cần chạy `Insert_TransportOptions.sql` và `UpdateLegacyLocationData.sql` trước
- **Thứ tự chạy:** Sau khi có dữ liệu `TransportOptions` và `LegacyLocations`

### 5. **Insert_LocalTransports.sql**
- **Mục đích:** Insert dữ liệu cho bảng `LocalTransports` (Phương tiện nội thành)
- **Nội dung:** Taxi, xe máy thuê, xe điện trung chuyển
- **Thứ tự chạy:** Có thể chạy độc lập

### 6. **Insert_Ticket_Prices.sql** (Đã có sẵn)
- **Mục đích:** Insert dữ liệu cho bảng `Attractions` (Giá vé tham quan)
- **Nội dung:** Giá vé các điểm tham quan tại các địa điểm du lịch
- **Lưu ý:** Cần có dữ liệu `TouristPlaces` trước
- **Thứ tự chạy:** Sau khi có dữ liệu `TouristPlaces`

### 7. **Insert_Festivals.sql**
- **Mục đích:** Insert dữ liệu cho bảng `Festivals` (Lễ hội và sự kiện)
- **Nội dung:** 5 lễ hội/sự kiện tại Đà Lạt
- **Thứ tự chạy:** Có thể chạy độc lập

### 8. **Insert_BlogPosts.sql**
- **Mục đích:** Insert dữ liệu cho bảng `BlogPosts` (Bài viết blog)
- **Nội dung:** 5 bài viết mẫu về du lịch Đà Lạt
- **Thứ tự chạy:** Có thể chạy độc lập

### 9. **Insert_Reviews.sql**
- **Mục đích:** Insert dữ liệu cho bảng `Reviews` (Đánh giá địa điểm)
- **Nội dung:** Các đánh giá mẫu (hiện tại đang comment)
- **Lưu ý:** Cần có dữ liệu `TouristPlaces` trước
- **Thứ tự chạy:** Sau khi có dữ liệu `TouristPlaces`

### 10. **UpdateLegacyLocationData.sql** (Đã có sẵn)
- **Mục đích:** Insert/Update dữ liệu cho bảng `LegacyLocations` (Địa điểm xuất phát)
- **Nội dung:** Các tỉnh/thành phố (bao gồm tỉnh sáp nhập)
- **Thứ tự chạy:** Chạy trước `Insert_TransportPriceHistories.sql`

## 🔄 Thứ tự chạy các script (khuyến nghị)

### Bước 1: Các bảng cơ bản
```sql
1. Insert_Categories.sql
2. Insert_Regions.sql
3. UpdateLegacyLocationData.sql
4. Insert_TransportOptions.sql
```

### Bước 2: Các bảng phụ thuộc
```sql
5. Insert_TransportPriceHistories.sql (cần TransportOptions và LegacyLocations)
6. Insert_LocalTransports.sql
```

### Bước 3: Dữ liệu địa điểm và liên quan
```sql
7. [Cần insert TouristPlaces, Hotels, Restaurants trước]
8. Insert_Ticket_Prices.sql (cần TouristPlaces)
9. Insert_Reviews.sql (cần TouristPlaces)
```

### Bước 4: Dữ liệu nội dung
```sql
10. Insert_Festivals.sql
11. Insert_BlogPosts.sql
```

## ⚠️ Lưu ý quan trọng

1. **Foreign Key Constraints:**
   - Đảm bảo các bảng cha (parent tables) đã có dữ liệu trước khi insert vào bảng con (child tables)
   - Ví dụ: Cần có `TouristPlaces` trước khi insert `Attractions`, `Hotels`, `Restaurants`

2. **Identity Columns:**
   - Một số bảng có Identity column (tự động tăng), không cần chỉ định ID
   - Một số bảng cần chỉ định ID cụ thể (như `Categories`, `Regions`)

3. **Reset Identity:**
   - Nếu muốn reset và insert lại từ đầu, bỏ comment các dòng:
     ```sql
     DELETE FROM TableName;
     DBCC CHECKIDENT ('TableName', RESEED, 0);
     ```

4. **Kiểm tra dữ liệu:**
   - Mỗi file đều có câu lệnh SELECT ở cuối để kiểm tra dữ liệu đã insert
   - Chạy các câu lệnh này để verify

5. **Dữ liệu mẫu:**
   - Các file này chứa dữ liệu mẫu để test và demo
   - Có thể chỉnh sửa theo nhu cầu thực tế

## 🚀 Cách sử dụng

### Cách 1: Chạy từng file trong SQL Server Management Studio
1. Mở SQL Server Management Studio
2. Kết nối đến database
3. Mở từng file `.sql`
4. Chạy (F5 hoặc Execute)

### Cách 2: Chạy tất cả bằng script PowerShell
```powershell
# Tạo script chạy tất cả các file SQL
$files = @(
    "Insert_Categories.sql",
    "Insert_Regions.sql",
    "UpdateLegacyLocationData.sql",
    "Insert_TransportOptions.sql",
    "Insert_TransportPriceHistories.sql",
    "Insert_LocalTransports.sql",
    "Insert_Festivals.sql",
    "Insert_BlogPosts.sql"
)

foreach ($file in $files) {
    Write-Host "Running $file..."
    sqlcmd -S "ServerName" -d "DatabaseName" -i $file
}
```

### Cách 3: Sử dụng Entity Framework Migration
- Có thể tích hợp vào `SeedData.cs` và chạy qua migration

## 📝 Các bảng chưa có file INSERT

Các bảng sau chưa có file INSERT (cần tạo thêm nếu cần):
- `TouristPlaces` - Địa điểm du lịch (dữ liệu lớn, cần file riêng)
- `Hotels` - Khách sạn (phụ thuộc TouristPlaces)
- `Restaurants` - Nhà hàng (phụ thuộc TouristPlaces)
- `Favorites` - Yêu thích (phụ thuộc Users và TouristPlaces)
- `Contacts` - Liên hệ (thường để trống hoặc insert khi có form submit)
- `Vehicles` - Xe trong hệ thống carpooling
- `Passengers` - Hành khách carpooling
- `PassengerGroups` - Nhóm hành khách
- `PendingCarpoolRequests` - Yêu cầu carpool đang chờ
- `CompletedTrips` - Chuyến đi đã hoàn thành
- `VehiclePricingConfigs` - Cấu hình giá xe

## ✅ Checklist

Trước khi chạy scripts, đảm bảo:
- [ ] Database đã được tạo
- [ ] Tất cả migrations đã được apply
- [ ] Các bảng đã được tạo trong database
- [ ] Đã backup database (nếu cần)
- [ ] Đã kiểm tra connection string

## 📞 Hỗ trợ

Nếu gặp lỗi khi chạy scripts:
1. Kiểm tra Foreign Key constraints
2. Kiểm tra dữ liệu đã tồn tại (tránh duplicate)
3. Kiểm tra format dữ liệu (NVARCHAR cho tiếng Việt)
4. Kiểm tra Identity seed values













































