# 🏔️ Web Du Lịch Đà Lạt - Da Lat Tourism Platform

Một nền tảng web toàn diện cho lý lịch, kế hoạch chuyến đi và chia sẻ xe cho du khách tại Đà Lạt, Việt Nam. Ứng dụng được xây dựng bằng **ASP.NET Core 8** với các tính năng thông minh về tối ưu hóa lộ trình, so sánh giá vé máy bay và tính toán chi phí vận chuyển.

---

## ✨ Tính Năng Chính

### 🎯 Quản Lý Địa Điểm Du Lịch
- Danh sách các điểm du lịch nổi tiếng tại Đà Lạt
- Phân loại theo danh mục (Núi, Hồ, Vườn, v.v.)
- Xem chi tiết, bình luận và đánh giá
- Lưu yêu thích

### ✈️ Tìm Kiếm Chuyến Bay
- Tích hợp **Amadeus Flight API** để tìm và so sánh giá vé máy bay
- Hiển thị các tùy chọn từ nhiều hãng hàng không
- Lọc theo giá, thời gian và tiện ích

### 🚗 Chia Sẻ Xe (Carpool)
- **Thuật toán K-Means Clustering** để nhóm yêu cầu
- **Min-Cost Max-Flow** để tối ưu chi phí
- **PDPTW (Pickup and Delivery Problem with Time Windows)** cho lộ trình hiệu quả
- Tích hợp **OSRM** cho tính toán đường đi

### 📋 Lập Kế Hoạch Chuyến Đi
- Gợi ý điểm du lịch thông minh
- Tính toán chi phí chuyến đi
- Đề xuất lịch trình tối ưu
- Tích hợp vận chuyển địa phương

### 🏨 Du Lịch & Duy Trì
- Danh sách khách sạn, nhà hàng
- Khu cắm trại (Camping Sites)
- Lễ hội địa phương
- Thông tin vận chuyển địa phương

### 📝 Blog & Bài Viết
- Chia sẻ trải nghiệm du lịch
- Quản lý bài viết (Admin)
- Nhân xét và thảo luận

### 📧 Liên Hệ & Hỗ Trợ
- Biểu mẫu liên hệ khách đặt
- Gửi email xác nhận
- Quản lý phản hồi người dùng

---

## 🛠️ Công Nghệ Sử Dụng

| Lớp | Công Nghệ |
|-----|-----------|
| **Framework** | ASP.NET Core 8.0 |
| **Database** | SQL Server / SQLite |
| **ORM** | Entity Framework Core 8.0 |
| **Authentication** | ASP.NET Core Identity |
| **APIs** | Amadeus Flight API, OSRM |
| **Algorithms** | K-Means, Min-Cost Max-Flow, PDPTW |
| **Frontend** | Razor Views, Bootstrap |

---

## 📦 Cấu Trúc Dự Án

```
WebDuLichDaLat/
├── Controllers/
│   ├── HomeController.cs
│   ├── TouristPlaceController.cs
│   ├── FlightController.cs
│   ├── CarpoolController.cs
│   ├── TripPlannerController.cs
│   ├── HotelController.cs
│   ├── RestaurantController.cs
│   ├── CampingController.cs
│   ├── FestivalController.cs
│   ├── BlogController.cs
│   ├── FavoriteController.cs
│   └── ContactController.cs
├── Models/
│   ├── ApplicationDbContext.cs
│   ├── TouristPlace.cs
│   ├── CarpoolRequest.cs
│   ├── Flight.cs
│   ├── Hotel.cs
│   ├── Restaurant.cs
│   ├── BlogPost.cs
│   └── ...
├── Services/
│   ├── CarpoolMatchingService.cs
│   ├── TransportPriceCalculator.cs
│   ├── AmadeusFlightService.cs
│   ├── RecommendationService.cs
│   └── ...
├── Areas/
│   ├── Admin/          # Quản trị viên
│   └── Identity/       # Xác thực người dùng
├── Data/
│   └── Insert_*.sql    # Scripts khởi tạo dữ liệu
└── Views/              # Razor Templates

```

---

## 🚀 Hướng Dẫn Cài Đặt

### Yêu Cầu
- ✅ .NET SDK 8.0 trở lên
- ✅ SQL Server 2019+ hoặc LocalDB
- ✅ Visual Studio 2022 hoặc VS Code
- ✅ Git

### Các Bước Cài Đặt

#### 1. Clone Repository
```bash
git clone https://github.com/yourusername/WebDuLichDaLat.git
cd DoAnCoSo
```

#### 2. Restore Dependencies
```bash
cd WebDuLichDaLat
dotnet restore
```

#### 3. Cấu Hình Database
Cập nhật **appsettings.json** với chuỗi kết nối của bạn:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=WebDuLichDaLat;Trusted_Connection=true;"
  }
}
```

#### 4. Chạy Database Migrations
```bash
dotnet ef database update
```

#### 5. Khởi tạo Dữ Liệu (Tùy chọn)
Chạy các SQL scripts trong thư mục `Data/`:
- `Insert_Regions.sql` - Các vùng
- `Insert_Categories.sql` - Danh mục
- `Insert_TouristPlaces.sql` - Địa điểm du lịch
- `Insert_Hotels.sql` - Khách sạn
- `Insert_Restaurants.sql` - Nhà hàng
- Run `Data/Run_All_Insert_Scripts.sql` để chạy tất cả

#### 6. Chạy Ứng Dụng

**Visual Studio:**
- Nhấn `F5` hoặc `Debug > Start Debugging`

**Terminal:**
```bash
dotnet run
```

#### 7. Truy Cập Ứng Dụng
- **HTTP:** http://localhost:5155
- **HTTPS:** https://localhost:7071

---

## 🔐 Tài Khoản Admin (Sau khi chạy migrations)

Tạo tài khoản admin qua:
1. Đăng ký một tài khoản mới
2. Cập nhật role trong database:
```sql
UPDATE AspNetUserRoles 
SET RoleId = (SELECT Id FROM AspNetRoles WHERE Name = 'Admin')
WHERE UserId = 'YOUR_USER_ID'
```

---

## 🔧 Cấu Hình Thêm

### API Keys Cần Thiết

#### Amadeus Flight API
1. Đăng ký tại [Amadeus for Developers](https://developers.amadeus.com)
2. Lấy `Client ID` và `Client Secret`
3. Thêm vào `appsettings.json`:

```json
{
  "AmadeusSettings": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "IsProduction": false
  }
}
```

#### OSRM (Nếu sử dụng on-premise)
Cấu hình OSRM server URL trong `appsettings.json`:
```json
{
  "OsrmSettings": {
    "BaseUrl": "http://router.project-osrm.org"
  }
}
```

#### Email Configuration
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-app-password"
  }
}
```

---

## 📊 Cơ Sở Dữ Liệu

### Bảng Chính
- **TouristPlaces** - Địa điểm du lịch
- **Categories** - Danh mục
- **Regions** - Vùng/Địa phương
- **CarpoolRequests** - Yêu cầu chia sẻ xe
- **Hotels** - Khách sạn
- **Restaurants** - Nhà hàng
- **CampingSites** - Khu cắm trại
- **Festivals** - Lễ hội
- **BlogPosts** - Bài viết blog
- **Reviews** - Bình luận/Đánh giá
- **TransportOptions** - Loại vận chuyển
- **TransportPrices** - Giá vận chuyển

---

## 🧮 Các Thuật Toán Sử Dụng

### 1. **K-Means Clustering**
- Nhóm yêu cầu carpool theo vị trí địa lý
- Giảm số lượng tuyến đường cần tối ưu hóa

### 2. **Min-Cost Max-Flow**
- Tối ưu hóa chi phí chia sẻ xe
- Đảm bảo mỗi hành khách được phục vụ

### 3. **PDPTW (Pickup and Delivery Problem with Time Windows)**
- Lập lịch trình hiệu quả cho nhiều điểm đón/trả
- Tuân thủ khung thời gian của hành khách

### 4. **Transport Price Calculation**
- Tính toán chi phí dựa trên:
  - Khoảng cách (Km)
  - Loại phương tiện
  - Thời gian đặt chỗ
  - Giá cơ bản và hệ số điều chỉnh


---

## 👥 Đóng Góp

Chúng tôi rất hoan nghênh các đóng góp! Hãy:

1. Fork repository
2. Tạo nhánh tính năng (`git checkout -b feature/AmazingFeature`)
3. Commit thay đổi (`git commit -m 'Add some AmazingFeature'`)
4. Push nhánh (`git push origin feature/AmazingFeature`)
5. Tạo Pull Request


