-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG REVIEWS
-- Đánh giá của người dùng về các địa điểm du lịch
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM Reviews;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('Reviews', RESEED, 0);

-- ============================================
-- INSERT CÁC ĐÁNH GIÁ MẪU
-- Lưu ý: TouristPlaceId phải tồn tại trong bảng TouristPlaces
-- ============================================

-- Đánh giá cho Hồ Xuân Hương (ví dụ: DL0001)
-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0001',
--     5,
--     N'Hồ Xuân Hương rất đẹp, không khí trong lành, là nơi lý tưởng để đi dạo và chụp ảnh. Rất đáng để ghé thăm!',
--     GETDATE()
-- );

-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0001',
--     4,
--     N'Cảnh đẹp nhưng hơi đông người vào cuối tuần. Nên đi vào buổi sáng sớm để tránh đông.',
--     DATEADD(day, -5, GETDATE())
-- );

-- Đánh giá cho Thung lũng Tình Yêu (ví dụ: DL0012)
-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0012',
--     5,
--     N'Thung lũng Tình Yêu là một trong những địa điểm đẹp nhất Đà Lạt. Cảnh quan thiên nhiên tuyệt vời, có nhiều hoạt động vui chơi. Giá vé hợp lý.',
--     DATEADD(day, -10, GETDATE())
-- );

-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0012',
--     4,
--     N'Đẹp nhưng hơi xa trung tâm. Nên đi bằng xe máy hoặc taxi. Cáp treo rất thú vị!',
--     DATEADD(day, -15, GETDATE())
-- );

-- Đánh giá cho Thác Datanla (ví dụ: DL0013)
-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0013',
--     5,
--     N'Thác Datanla rất đẹp và hùng vĩ. Máng trượt rất vui và thú vị. Cảnh quan xung quanh rất đẹp, có nhiều cây xanh.',
--     DATEADD(day, -20, GETDATE())
-- );

-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0013',
--     4,
--     N'Thác đẹp nhưng đường đi hơi khó. Nên đi giày thể thao. Máng trượt rất vui!',
--     DATEADD(day, -25, GETDATE())
-- );

-- Đánh giá cho Langbiang (ví dụ: DL0020)
-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0020',
--     5,
--     N'Langbiang là đỉnh núi cao nhất Đà Lạt, view từ trên xuống rất đẹp. Có thể đi xe jeep hoặc đi bộ. Không khí trong lành, cảnh quan tuyệt vời!',
--     DATEADD(day, -30, GETDATE())
-- );

-- INSERT INTO Reviews (TouristPlaceId, Rating, Comment, CreatedAt)
-- VALUES (
--     N'DL0020',
--     4,
--     N'Đẹp nhưng đường đi hơi khó và dốc. Nên đi vào buổi sáng để tránh sương mù. View từ đỉnh núi rất đáng giá!',
--     DATEADD(day, -35, GETDATE())
-- );

-- ============================================
-- HƯỚNG DẪN SỬ DỤNG
-- ============================================
-- 1. Thay thế TouristPlaceId bằng ID thực tế từ bảng TouristPlaces
-- 2. Rating: 1-5 sao
-- 3. Comment: Nhận xét của người dùng (có thể NULL)
-- 4. CreatedAt: Ngày tạo đánh giá

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT 
--     r.Id,
--     r.TouristPlaceId,
--     tp.Name AS PlaceName,
--     r.Rating,
--     r.Comment,
--     r.CreatedAt
-- FROM Reviews r
-- INNER JOIN TouristPlaces tp ON r.TouristPlaceId = tp.Id
-- ORDER BY r.CreatedAt DESC;













































