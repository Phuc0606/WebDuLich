-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG TRANSPORTOPTIONS
-- Phương tiện vận chuyển đến Đà Lạt
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM TransportOptions;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('TransportOptions', RESEED, 0);

-- ============================================
-- INSERT CÁC PHƯƠNG TIỆN
-- ============================================

-- 1. Xe khách (Public Transport)
INSERT INTO TransportOptions (Name, Type, Price, FixedPrice, IsSelfDrive, FuelConsumption, FuelPrice)
VALUES (N'Xe khách', N'Public', 300000.00, 300000.00, 0, 0.00, 0.00);

-- 2. Xe limousine (Public Transport)
INSERT INTO TransportOptions (Name, Type, Price, FixedPrice, IsSelfDrive, FuelConsumption, FuelPrice)
VALUES (N'Xe limousine', N'Public', 550000.00, 550000.00, 0, 0.00, 0.00);

-- 3. Xe máy cá nhân (Private - Self Drive)
INSERT INTO TransportOptions (Name, Type, Price, FixedPrice, IsSelfDrive, FuelConsumption, FuelPrice)
VALUES (N'Xe máy cá nhân', N'Private', 0.00, 0.00, 1, 2.50, 23000.00);
-- Tiêu thụ: 2.5 lít/100km, Giá xăng: 23,000 VNĐ/lít

-- 4. Ô tô cá nhân (Private - Self Drive)
INSERT INTO TransportOptions (Name, Type, Price, FixedPrice, IsSelfDrive, FuelConsumption, FuelPrice)
VALUES (N'Ô tô cá nhân', N'Private', 0.00, 0.00, 1, 7.50, 23000.00);
-- Tiêu thụ: 7.5 lít/100km, Giá xăng: 23,000 VNĐ/lít

-- 5. Máy bay (Public Transport)
INSERT INTO TransportOptions (Name, Type, Price, FixedPrice, IsSelfDrive, FuelConsumption, FuelPrice)
VALUES (N'Máy bay', N'Public', 2000000.00, 2000000.00, 0, 0.00, 0.00);

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT * FROM TransportOptions ORDER BY Id;













































