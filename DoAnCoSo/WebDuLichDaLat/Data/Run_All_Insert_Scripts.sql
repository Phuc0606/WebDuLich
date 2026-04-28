-- ============================================
-- SCRIPT TỔNG HỢP - CHẠY TẤT CẢ INSERT SCRIPTS
-- Chạy file này để insert tất cả dữ liệu mẫu vào database
-- ============================================

-- ⚠️ LƯU Ý: 
-- 1. Đảm bảo database đã được tạo và migrations đã được apply
-- 2. Backup database trước khi chạy (nếu cần)
-- 3. Kiểm tra Foreign Key constraints trước khi chạy
-- 4. Chạy các script theo thứ tự để tránh lỗi Foreign Key

PRINT '========================================';
PRINT 'BẮT ĐẦU INSERT DỮ LIỆU MẪU';
PRINT '========================================';
PRINT '';

-- ============================================
-- BƯỚC 1: CÁC BẢNG CƠ BẢN
-- ============================================

PRINT 'Bước 1: Insert Categories...';
:r Insert_Categories.sql
PRINT '✓ Categories đã được insert';
PRINT '';

PRINT 'Bước 2: Insert Regions...';
:r Insert_Regions.sql
PRINT '✓ Regions đã được insert';
PRINT '';

PRINT 'Bước 3: Insert LegacyLocations...';
:r UpdateLegacyLocationData.sql
PRINT '✓ LegacyLocations đã được insert';
PRINT '';

PRINT 'Bước 4: Insert TransportOptions...';
:r Insert_TransportOptions.sql
PRINT '✓ TransportOptions đã được insert';
PRINT '';

-- ============================================
-- BƯỚC 2: CÁC BẢNG PHỤ THUỘC
-- ============================================

PRINT 'Bước 5: Insert TransportPriceHistories...';
:r Insert_TransportPriceHistories.sql
PRINT '✓ TransportPriceHistories đã được insert';
PRINT '';

PRINT 'Bước 6: Insert LocalTransports...';
:r Insert_LocalTransports.sql
PRINT '✓ LocalTransports đã được insert';
PRINT '';

-- ============================================
-- BƯỚC 3: DỮ LIỆU NỘI DUNG
-- ============================================

PRINT 'Bước 7: Insert Festivals...';
:r Insert_Festivals.sql
PRINT '✓ Festivals đã được insert';
PRINT '';

PRINT 'Bước 8: Insert BlogPosts...';
:r Insert_BlogPosts.sql
PRINT '✓ BlogPosts đã được insert';
PRINT '';

-- ============================================
-- BƯỚC 4: DỮ LIỆU PHỤ THUỘC ĐỊA ĐIỂM
-- ============================================
-- ⚠️ LƯU Ý: Các script sau cần có dữ liệu TouristPlaces trước

-- PRINT 'Bước 9: Insert Attractions (Ticket Prices)...';
-- :r Insert_Ticket_Prices.sql
-- PRINT '✓ Attractions đã được insert';
-- PRINT '';

-- PRINT 'Bước 10: Insert Reviews...';
-- :r Insert_Reviews.sql
-- PRINT '✓ Reviews đã được insert';
-- PRINT '';

PRINT '========================================';
PRINT 'HOÀN THÀNH INSERT DỮ LIỆU MẪU';
PRINT '========================================';
PRINT '';
PRINT 'Các bảng đã được insert:';
PRINT '  ✓ Categories';
PRINT '  ✓ Regions';
PRINT '  ✓ LegacyLocations';
PRINT '  ✓ TransportOptions';
PRINT '  ✓ TransportPriceHistories';
PRINT '  ✓ LocalTransports';
PRINT '  ✓ Festivals';
PRINT '  ✓ BlogPosts';
PRINT '';
PRINT 'Các bảng cần insert thủ công (phụ thuộc TouristPlaces):';
PRINT '  - Attractions (chạy Insert_Ticket_Prices.sql sau khi có TouristPlaces)';
PRINT '  - Reviews (chạy Insert_Reviews.sql sau khi có TouristPlaces)';
PRINT '  - Hotels (cần TouristPlaces)';
PRINT '  - Restaurants (cần TouristPlaces)';
PRINT '';

-- ============================================
-- KIỂM TRA TỔNG QUAN DỮ LIỆU
-- ============================================

PRINT '========================================';
PRINT 'KIỂM TRA DỮ LIỆU ĐÃ INSERT';
PRINT '========================================';
PRINT '';

PRINT 'Số lượng Categories:';
SELECT COUNT(*) AS TotalCategories FROM Categories;
PRINT '';

PRINT 'Số lượng Regions:';
SELECT COUNT(*) AS TotalRegions FROM Regions;
PRINT '';

PRINT 'Số lượng LegacyLocations:';
SELECT COUNT(*) AS TotalLegacyLocations FROM LegacyLocations;
PRINT '';

PRINT 'Số lượng TransportOptions:';
SELECT COUNT(*) AS TotalTransportOptions FROM TransportOptions;
PRINT '';

PRINT 'Số lượng TransportPriceHistories:';
SELECT COUNT(*) AS TotalTransportPriceHistories FROM TransportPriceHistories;
PRINT '';

PRINT 'Số lượng LocalTransports:';
SELECT COUNT(*) AS TotalLocalTransports FROM LocalTransports;
PRINT '';

PRINT 'Số lượng Festivals:';
SELECT COUNT(*) AS TotalFestivals FROM Festivals;
PRINT '';

PRINT 'Số lượng BlogPosts:';
SELECT COUNT(*) AS TotalBlogPosts FROM BlogPosts;
PRINT '';

PRINT '========================================';
PRINT 'HOÀN TẤT';
PRINT '========================================';













































