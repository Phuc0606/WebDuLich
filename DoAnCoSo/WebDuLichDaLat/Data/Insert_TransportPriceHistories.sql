-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG TRANSPORTPRICEHISTORIES
-- Giá vận chuyển theo từng tuyến (Phương tiện + Địa điểm xuất phát)
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM TransportPriceHistories;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('TransportPriceHistories', RESEED, 0);

-- ============================================
-- INSERT GIÁ VẬN CHUYỂN
-- Lưu ý: TransportOptionId và LegacyLocationId phải tồn tại trước
-- ============================================

-- ===== XE KHÁCH (TransportOptionId = 1) =====

-- Long An (cũ) → Đà Lạt (qua Tây Ninh mới)
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (1, 1, 350000.00);

-- Đắk Nông (cũ) → Đà Lạt (qua Lâm Đồng mới)
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (2, 1, 180000.00);

-- Bình Thuận (cũ) → Đà Lạt (qua Lâm Đồng mới)
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (3, 1, 200000.00);

-- Tây Ninh → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (4, 1, 400000.00);

-- TP. HCM → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (5, 1, 300000.00);

-- Đồng Nai → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (6, 1, 280000.00);

-- Bà Rịa - Vũng Tàu → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (7, 1, 320000.00);

-- Khánh Hòa (Nha Trang) → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (8, 1, 200000.00);

-- Ninh Thuận → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (9, 1, 180000.00);

-- Bình Phước → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (10, 1, 250000.00);

-- Lâm Đồng → Đà Lạt (nội tỉnh)
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (11, 1, 50000.00);

-- ===== XE LIMOUSINE (TransportOptionId = 2) =====

-- Long An (cũ) → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (1, 2, 600000.00);

-- TP. HCM → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (5, 2, 550000.00);

-- Tây Ninh → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (4, 2, 700000.00);

-- Đồng Nai → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (6, 2, 500000.00);

-- ===== MÁY BAY (TransportOptionId = 5) =====

-- TP. HCM → Đà Lạt
INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
VALUES (5, 5, 2000000.00);

-- Hà Nội → Đà Lạt (nếu có LegacyLocationId = 12)
-- INSERT INTO TransportPriceHistories (LegacyLocationId, TransportOptionId, Price)
-- VALUES (12, 5, 3500000.00);

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT 
--     tph.Id,
--     tph.Price,
--     lo.CurrentName AS LocationName,
--     to.Name AS TransportName
-- FROM TransportPriceHistories tph
-- INNER JOIN LegacyLocations lo ON tph.LegacyLocationId = lo.Id
-- INNER JOIN TransportOptions to ON tph.TransportOptionId = to.Id
-- ORDER BY tph.TransportOptionId, tph.LegacyLocationId;













































