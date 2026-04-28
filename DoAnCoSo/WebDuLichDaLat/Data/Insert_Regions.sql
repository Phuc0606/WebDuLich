-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG REGIONS
-- Khu vực địa lý trong Đà Lạt
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM Regions;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('Regions', RESEED, 0);

-- ============================================
-- INSERT CÁC KHU VỰC CHÍNH CỦA ĐÀ LẠT
-- ============================================

-- 1. Trung tâm thành phố
INSERT INTO Regions (RegionName) VALUES (N'Trung tâm thành phố');

-- 2. Langbiang (Núi Langbiang và khu vực xung quanh)
INSERT INTO Regions (RegionName) VALUES (N'Langbiang');

-- 3. Hồ Tuyền Lâm (Khu vực hồ Tuyền Lâm và các địa điểm gần đó)
INSERT INTO Regions (RegionName) VALUES (N'Hồ Tuyền Lâm');

-- 4. Xã Tà Nung (Khu vực Tà Nung, thác Datanla)
INSERT INTO Regions (RegionName) VALUES (N'Xã Tà Nung');

-- 5. Hồ Xuân Hương (Khu vực hồ Xuân Hương, trung tâm)
INSERT INTO Regions (RegionName) VALUES (N'Hồ Xuân Hương');

-- 6. Đồi Cù (Khu vực đồi Cù, trung tâm)
INSERT INTO Regions (RegionName) VALUES (N'Đồi Cù');

-- 7. Thung lũng Tình Yêu (Khu vực thung lũng Tình Yêu)
INSERT INTO Regions (RegionName) VALUES (N'Thung lũng Tình Yêu');

-- 8. Đà Lạt - Cam Ly (Khu vực Cam Ly, thác Cam Ly)
INSERT INTO Regions (RegionName) VALUES (N'Cam Ly');

-- 9. Đà Lạt - Trại Mát (Khu vực Trại Mát, chùa Linh Phước)
INSERT INTO Regions (RegionName) VALUES (N'Trại Mát');

-- 10. Đà Lạt - Cầu Đất (Khu vực Cầu Đất, đồi chè)
INSERT INTO Regions (RegionName) VALUES (N'Cầu Đất');

-- 11. Đà Lạt - D'ran (Khu vực D'ran, Đơn Dương)
INSERT INTO Regions (RegionName) VALUES (N'D''ran');

-- 12. Đà Lạt - Lạc Dương (Khu vực Lạc Dương)
INSERT INTO Regions (RegionName) VALUES (N'Lạc Dương');

-- 13. Đà Lạt - Đức Trọng (Khu vực Đức Trọng)
INSERT INTO Regions (RegionName) VALUES (N'Đức Trọng');

-- 14. Đà Lạt - Đơn Dương (Khu vực Đơn Dương)
INSERT INTO Regions (RegionName) VALUES (N'Đơn Dương');

-- 15. Đà Lạt - Đam Rông (Khu vực Đam Rông)
INSERT INTO Regions (RegionName) VALUES (N'Đam Rông');

-- 16. Khu vực ngoại thành (Các địa điểm ngoài thành phố)
INSERT INTO Regions (RegionName) VALUES (N'Ngoại thành');

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT * FROM Regions ORDER BY RegionId;













































