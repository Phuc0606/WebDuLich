-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG LOCALTRANSPORTS
-- Phương tiện di chuyển nội thành Đà Lạt
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM LocalTransports;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('LocalTransports', RESEED, 0);

-- ============================================
-- INSERT CÁC PHƯƠNG TIỆN NỘI THÀNH
-- TransportType: 1=HotelShuttle, 2=ElectricShuttle, 3=LocalTaxi, 4=MotorbikeRental
-- ============================================

-- ===== TAXI NỘI THÀNH (TransportType = 3) =====
-- Giá taxi: ~15,000 - 20,000 VNĐ/km

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Taxi Mai Linh', 3, 15000.00, NULL, NULL, NULL, NULL, N'Taxi nội thành Đà Lạt - Giá theo km');

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Taxi Vinasun', 3, 15000.00, NULL, NULL, NULL, NULL, N'Taxi nội thành Đà Lạt - Giá theo km');

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Taxi Grab', 3, 18000.00, NULL, NULL, NULL, NULL, N'Taxi công nghệ - Giá theo km, có thể dao động');

-- ===== XE MÁY THUÊ (TransportType = 4) =====
-- Giá thuê xe máy: ~100,000 - 200,000 VNĐ/ngày

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Xe máy thuê - Thành Đạt', 4, NULL, 150000.00, NULL, NULL, NULL, N'Thuê xe máy tự lái - Giá theo ngày');

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Xe máy thuê - Đà Lạt Motorbike', 4, NULL, 120000.00, NULL, NULL, NULL, N'Thuê xe máy tự lái - Giá theo ngày');

-- ===== XE ĐIỆN TRUNG CHUYỂN (TransportType = 2) =====
-- Giá xe điện: ~30,000 - 50,000 VNĐ/chuyến

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Xe điện trung chuyển - Hồ Xuân Hương', 2, NULL, NULL, 30000.00, NULL, NULL, N'Xe điện du lịch quanh hồ Xuân Hương');

INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
VALUES (N'Xe điện trung chuyển - Trung tâm', 2, NULL, NULL, 50000.00, NULL, NULL, N'Xe điện du lịch trung tâm thành phố');

-- ===== XE BUÝT KHÁCH SẠN (TransportType = 1) =====
-- Lưu ý: HotelId phải tồn tại trong bảng Hotels
-- Ví dụ: Nếu có Hotel với Id = 1

-- INSERT INTO LocalTransports (Name, TransportType, PricePerKm, PricePerDay, PricePerTrip, HotelId, TouristPlaceId, Note)
-- VALUES (N'Xe buýt khách sạn - A Villa In Dalat', 1, NULL, NULL, 0.00, 1, NULL, N'Xe buýt miễn phí cho khách của khách sạn');

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT 
--     Id,
--     Name,
--     CASE TransportType
--         WHEN 1 THEN N'Xe buýt khách sạn'
--         WHEN 2 THEN N'Xe điện trung chuyển'
--         WHEN 3 THEN N'Taxi nội thành'
--         WHEN 4 THEN N'Xe máy thuê'
--     END AS TransportTypeName,
--     PricePerKm,
--     PricePerDay,
--     PricePerTrip,
--     Note
-- FROM LocalTransports
-- ORDER BY TransportType, Id;













































