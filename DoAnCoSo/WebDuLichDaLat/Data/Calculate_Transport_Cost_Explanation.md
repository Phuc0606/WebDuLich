# 📊 GIẢI THÍCH TÍNH CHI PHÍ XE MÁY CÁ NHÂN: 786.586đ

## 🗺️ Điểm xuất phát
- **Địa điểm**: Công Ty TNHH Nước Giải Khát Coca-Cola
- **Địa chỉ**: Phường Linh Xuân, Thủ Đức, TP.HCM
- **Tọa độ GPS**: Khoảng 10.8600°N, 106.7700°E

## 🎯 Điểm đến
- **Địa điểm**: Đà Lạt
- **Tọa độ GPS**: 11.9404°N, 108.4583°E

## 📏 Khoảng cách
- **Khoảng cách từ TP.HCM đến Đà Lạt**: ~300 km (theo đường bộ)

---

## 💰 CÁCH TÍNH CHI PHÍ 786.586đ

### Phương án 1: Giá cố định từ Database (Ưu tiên)

Nếu trong bảng `TransportPriceHistories` có giá cố định cho:
- **LegacyLocation**: Thủ Đức hoặc TP.HCM
- **TransportOption**: Xe máy cá nhân (Private)
- **Giá**: 786.586đ

→ Hệ thống sẽ lấy giá này trực tiếp từ database (giá từ nhà xe thực tế)

---

### Phương án 2: Tính theo khoảng cách (Fallback)

Nếu **KHÔNG có** giá cố định trong database, hệ thống sẽ tính theo công thức:

#### Công thức tính:
```
Giá = Khoảng cách (km) × Giá/km × Hệ số giảm giá
```

#### Bảng giá/km theo loại phương tiện:
- **Xe khách (Public)**: 2.500đ/km
- **Xe limousine/Xe máy cá nhân (Private)**: 4.000đ/km
- **Mặc định**: 3.000đ/km

#### Hệ số giảm giá:
- **Khoảng cách > 300km**: Giảm 15% → Giá/km = 3.400đ/km
- **Khoảng cách > 200km**: Giảm 10% → Giá/km = 3.600đ/km
- **Khoảng cách ≤ 200km**: Không giảm → Giá/km = 4.000đ/km

#### Tính toán với khoảng cách 300km:

**Trường hợp 1: Không giảm giá**
```
300km × 4.000đ/km = 1.200.000đ ❌ (Không khớp với 786.586đ)
```

**Trường hợp 2: Giảm 10% (> 200km)**
```
300km × 3.600đ/km = 1.080.000đ ❌ (Không khớp với 786.586đ)
```

**Trường hợp 3: Giảm 15% (> 300km)**
```
300km × 3.400đ/km = 1.020.000đ ❌ (Không khớp với 786.586đ)
```

**Trường hợp 4: Khoảng cách thực tế khác**
```
786.586đ ÷ 4.000đ/km = 196,65 km
786.586đ ÷ 3.600đ/km = 218,50 km
786.586đ ÷ 3.400đ/km = 231,64 km
```

---

## ✅ KẾT LUẬN

**Chi phí 786.586đ** có thể được tính theo các cách sau:

### 1. **Giá cố định từ Database** (Khả năng cao nhất)
- Giá này được lưu trong bảng `TransportPriceHistories`
- Đây là giá thực tế từ nhà xe hoặc đã được nhập vào hệ thống
- Hệ thống ưu tiên sử dụng giá này thay vì tính toán

### 2. **Khoảng cách thực tế nhỏ hơn**
- Nếu khoảng cách thực tế là ~196-232 km (không phải 300km)
- Có thể do:
  - Điểm xuất phát gần Đà Lạt hơn
  - Hoặc tính theo đường chim bay thay vì đường bộ

### 3. **Công thức tính khác**
- Có thể hệ thống đang sử dụng công thức tính khác cho xe máy cá nhân
- Hoặc có điều chỉnh đặc biệt cho phương tiện này

---

## 🔍 CÁCH KIỂM TRA

Để xác định chính xác cách tính, bạn có thể:

1. **Kiểm tra Database**:
   ```sql
   SELECT * FROM TransportPriceHistories 
   WHERE LegacyLocationId IN (
       SELECT Id FROM LegacyLocations 
       WHERE CurrentName LIKE '%Thủ Đức%' 
       OR CurrentName LIKE '%TP.HCM%'
       OR CurrentName LIKE '%Ho Chi Minh%'
   )
   AND TransportOptionId = [ID của Xe máy cá nhân]
   ```

2. **Kiểm tra Log/Code**:
   - Xem trong code có logic đặc biệt nào cho xe máy cá nhân không
   - Kiểm tra xem `PriceType` là "Fixed" hay "Calculated"

3. **Tính toán lại**:
   - Sử dụng công cụ tính khoảng cách GPS chính xác
   - Áp dụng công thức trong code để tính lại

---

## 📝 LƯU Ý

- **Chi phí di chuyển nội thành (28.818đ)** được tính riêng và không bao gồm trong 786.586đ
- **Tổng chi phí vận chuyển** = 786.586đ (chính) + 28.818đ (nội thành) = **815.404đ**

















































