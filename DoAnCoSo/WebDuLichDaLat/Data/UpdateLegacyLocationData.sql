-- Script SQL để cập nhật dữ liệu LegacyLocation với thông tin sáp nhập
-- Chạy script này sau khi migration thành công

-- ===== CẬP NHẬT DỮ LIỆU CŨ (nếu đã tồn tại) =====

-- Cập nhật Long An (ID = 1)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 1)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 1,
        MergeDate = '2025-07-01',
        MergeNote = 'Long An sáp nhập vào Tây Ninh từ 01/07/2025',
        IsActive = 1
    WHERE Id = 1;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, MergeDate, MergeNote, IsActive)
    VALUES (1, 'Long An', 'Tây Ninh', 10.5368, 106.4149, 1, '2025-07-01', 'Long An sáp nhập vào Tây Ninh từ 01/07/2025', 1);
END

-- Cập nhật Đắk Nông (ID = 2)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 2)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 1,
        MergeDate = '2025-07-01',
        MergeNote = 'Đắk Nông sáp nhập vào Lâm Đồng từ 01/07/2025',
        IsActive = 1
    WHERE Id = 2;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, MergeDate, MergeNote, IsActive)
    VALUES (2, 'Đắk Nông', 'Lâm Đồng', 12.2646, 107.6098, 1, '2025-07-01', 'Đắk Nông sáp nhập vào Lâm Đồng từ 01/07/2025', 1);
END

-- Cập nhật Bình Thuận (ID = 3)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 3)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 1,
        MergeDate = '2025-07-01',
        MergeNote = 'Bình Thuận sáp nhập vào Lâm Đồng từ 01/07/2025',
        IsActive = 1
    WHERE Id = 3;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, MergeDate, MergeNote, IsActive)
    VALUES (3, 'Bình Thuận', 'Lâm Đồng', 11.0904, 108.0721, 1, '2025-07-01', 'Bình Thuận sáp nhập vào Lâm Đồng từ 01/07/2025', 1);
END

-- Cập nhật Tây Ninh (ID = 4) - Không bị sáp nhập
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 4)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 4;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (4, 'Tây Ninh', 'Tây Ninh', 11.3100, 106.0983, 0, 1);
END

-- Cập nhật TP. Hồ Chí Minh (ID = 5)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 5)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 5;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (5, 'TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 10.7769, 106.7009, 0, 1);
END

-- Cập nhật Đồng Nai (ID = 6)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 6)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 6;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (6, 'Đồng Nai', 'Đồng Nai', 10.9472, 106.8446, 0, 1);
END

-- Cập nhật Bà Rịa - Vũng Tàu (ID = 7)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 7)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 7;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (7, 'Bà Rịa - Vũng Tàu', 'Bà Rịa - Vũng Tàu', 10.5417, 107.2429, 0, 1);
END

-- Cập nhật Khánh Hòa (ID = 8)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 8)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 8;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (8, 'Khánh Hòa', 'Khánh Hòa', 12.2388, 109.1967, 0, 1);
END

-- Cập nhật Ninh Thuận (ID = 9)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 9)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 9;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (9, 'Ninh Thuận', 'Ninh Thuận', 11.6739, 108.8629, 0, 1);
END

-- Cập nhật Bình Phước (ID = 10)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 10)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 10;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (10, 'Bình Phước', 'Bình Phước', 11.7511, 106.7234, 0, 1);
END

-- Cập nhật Lâm Đồng (ID = 11)
IF EXISTS (SELECT 1 FROM LegacyLocations WHERE Id = 11)
BEGIN
    UPDATE LegacyLocations
    SET 
        IsMergedLocation = 0,
        IsActive = 1
    WHERE Id = 11;
END
ELSE
BEGIN
    INSERT INTO LegacyLocations (Id, OldName, CurrentName, Latitude, Longitude, IsMergedLocation, IsActive)
    VALUES (11, 'Lâm Đồng', 'Lâm Đồng', 11.9404, 108.4583, 0, 1);
END

-- ===== CẬP NHẬT DỮ LIỆU TRANSPORTPRICEHISTORY =====

-- Xóa dữ liệu cũ nếu cần (tùy chọn)
-- DELETE FROM TransportPriceHistories WHERE Id BETWEEN 1 AND 20;

-- Insert hoặc update TransportPriceHistory
-- Lưu ý: Nếu đã có dữ liệu, bạn có thể cần xóa trước hoặc update

-- Long An → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 1)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (1, 1, 1, 350000);
END

-- Đắk Nông → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 2)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (2, 2, 1, 180000);
END

-- Bình Thuận → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 3)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (3, 3, 1, 200000);
END

-- Tây Ninh → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 4)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (4, 4, 1, 400000);
END

-- TP. HCM → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 5)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (5, 5, 1, 300000);
END

-- Đồng Nai → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 6)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (6, 6, 1, 280000);
END

-- Vũng Tàu → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 7)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (7, 7, 1, 320000);
END

-- Nha Trang → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 8)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (8, 8, 1, 200000);
END

-- Ninh Thuận → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 9)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (9, 9, 1, 180000);
END

-- Bình Phước → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 10)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (10, 10, 1, 250000);
END

-- Long An → Đà Lạt (Xe limousine)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 11)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (11, 1, 2, 600000);
END

-- TP. HCM → Đà Lạt (Xe limousine)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 12)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (12, 5, 2, 550000);
END

-- Tây Ninh → Đà Lạt (Xe limousine)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 13)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (13, 4, 2, 700000);
END

-- Lâm Đồng → Đà Lạt (Xe khách)
IF NOT EXISTS (SELECT 1 FROM TransportPriceHistories WHERE Id = 14)
BEGIN
    INSERT INTO TransportPriceHistories (Id, LegacyLocationId, TransportOptionId, Price)
    VALUES (14, 11, 1, 50000);
END

PRINT '✅ Đã cập nhật dữ liệu LegacyLocation và TransportPriceHistory thành công!';

