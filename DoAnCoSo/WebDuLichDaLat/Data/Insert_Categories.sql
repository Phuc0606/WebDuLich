-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG CATEGORIES
-- Danh mục phân loại địa điểm du lịch
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM Categories;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('Categories', RESEED, 0);

-- ============================================
-- INSERT CÁC DANH MỤC
-- ============================================

INSERT INTO Categories (CategoryId, CategoryName) VALUES (1, N'Khách sạn');
INSERT INTO Categories (CategoryId, CategoryName) VALUES (2, N'Nhà hàng/Quán ăn');
INSERT INTO Categories (CategoryId, CategoryName) VALUES (3, N'Địa điểm du lịch');

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT * FROM Categories ORDER BY CategoryId;













































