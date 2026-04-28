-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG BLOGPOSTS
-- Bài viết blog về du lịch Đà Lạt
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM BlogPosts;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('BlogPosts', RESEED, 0);

-- ============================================
-- INSERT CÁC BÀI VIẾT BLOG
-- ============================================

-- 1. Khám phá Đà Lạt - Thành phố ngàn hoa
INSERT INTO BlogPosts (Title, Content, ImageUrl, PostedDate, Author)
VALUES (
    N'Khám phá Đà Lạt - Thành phố ngàn hoa',
    N'Đà Lạt, thành phố ngàn hoa nằm trên cao nguyên Lâm Viên, là điểm đến lý tưởng cho những ai yêu thích thiên nhiên và khí hậu mát mẻ. Với độ cao 1.500m so với mực nước biển, Đà Lạt có khí hậu ôn đới quanh năm, nhiệt độ trung bình từ 15-24°C.

Thành phố này nổi tiếng với những vườn hoa rực rỡ, những hồ nước thơ mộng và những ngọn đồi xanh mướt. Du khách có thể tham quan các địa điểm nổi tiếng như Hồ Xuân Hương, Thung lũng Tình Yêu, Dinh III Bảo Đại, hay thưởng thức cà phê tại các quán cà phê đẹp như tranh vẽ.

Đà Lạt cũng là nơi lý tưởng để thưởng thức các món đặc sản như dâu tây, atiso, rau củ tươi ngon và các món ăn địa phương độc đáo.',
    N'/images/blog/dalat-overview.jpg',
    '2025-01-15',
    N'Admin'
);

-- 2. Hướng dẫn du lịch Đà Lạt 3 ngày 2 đêm
INSERT INTO BlogPosts (Title, Content, ImageUrl, PostedDate, Author)
VALUES (
    N'Hướng dẫn du lịch Đà Lạt 3 ngày 2 đêm',
    N'Lịch trình du lịch Đà Lạt 3 ngày 2 đêm hoàn hảo cho những ai lần đầu đến với thành phố này:

**Ngày 1:**
- Sáng: Tham quan Hồ Xuân Hương, Chợ Đà Lạt
- Chiều: Dinh III Bảo Đại, Nhà thờ Con Gà
- Tối: Thưởng thức cà phê tại các quán cà phê đẹp

**Ngày 2:**
- Sáng: Thung lũng Tình Yêu, Hồ Tuyền Lâm
- Chiều: Thác Datanla, Vườn hoa Đà Lạt
- Tối: Đi dạo phố đêm, mua sắm đặc sản

**Ngày 3:**
- Sáng: Langbiang, Thác Prenn
- Chiều: Vườn dâu tây, mua quà lưu niệm
- Tối: Khởi hành về

**Lưu ý:** Nên đặt khách sạn trước, mang theo áo ấm và giày thể thao để đi bộ nhiều.',
    N'/images/blog/dalat-3days.jpg',
    '2025-01-20',
    N'Travel Guide'
);

-- 3. Top 10 địa điểm check-in đẹp nhất Đà Lạt
INSERT INTO BlogPosts (Title, Content, ImageUrl, PostedDate, Author)
VALUES (
    N'Top 10 địa điểm check-in đẹp nhất Đà Lạt',
    N'Đà Lạt có vô số địa điểm đẹp để check-in và chụp ảnh. Dưới đây là top 10 địa điểm không thể bỏ qua:

1. **Hồ Xuân Hương** - Hồ nước thơ mộng giữa lòng thành phố
2. **Thung lũng Tình Yêu** - Cảnh quan thiên nhiên tuyệt đẹp
3. **Dinh III Bảo Đại** - Kiến trúc Pháp cổ kính
4. **Nhà thờ Con Gà** - Công trình kiến trúc độc đáo
5. **Thác Datanla** - Thác nước hùng vĩ
7. **Langbiang** - Đỉnh núi cao nhất Đà Lạt
8. **Vườn hoa Đà Lạt** - Rực rỡ sắc màu
9. **Hồ Tuyền Lâm** - Hồ nước trong xanh
10. **Đường hầm điêu khắc** - Tác phẩm nghệ thuật độc đáo

Mỗi địa điểm đều có vẻ đẹp riêng và là nơi lý tưởng để lưu lại những khoảnh khắc đẹp.',
    N'/images/blog/dalat-checkin.jpg',
    '2025-02-01',
    N'Photo Guide'
);

-- 4. Ẩm thực Đà Lạt - Những món ngon không thể bỏ qua
INSERT INTO BlogPosts (Title, Content, ImageUrl, PostedDate, Author)
VALUES (
    N'Ẩm thực Đà Lạt - Những món ngon không thể bỏ qua',
    N'Đà Lạt không chỉ nổi tiếng với cảnh đẹp mà còn với nền ẩm thực phong phú và độc đáo. Dưới đây là những món ăn bạn nhất định phải thử khi đến Đà Lạt:

**1. Bánh mì xíu mại Đà Lạt**
- Bánh mì giòn với nhân xíu mại thơm ngon, đặc trưng của Đà Lạt

**2. Bánh căn Đà Lạt**
- Món bánh nhỏ xinh được nướng trong khuôn đất nung, ăn kèm với trứng và chả cá

**3. Bánh tráng nướng**
- Bánh tráng nướng giòn với các loại topping đa dạng

**4. Dâu tây tươi**
- Dâu tây Đà Lạt ngọt ngào, tươi ngon, có thể hái trực tiếp tại vườn

**5. Atiso Đà Lạt**
- Trà atiso thơm mát, tốt cho sức khỏe

**6. Rau củ tươi**
- Các loại rau củ tươi ngon, được trồng tại địa phương

**7. Cà phê Đà Lạt**
- Cà phê đặc sản với hương vị đậm đà, thơm ngon

Hãy thử những món ăn này để cảm nhận hương vị đặc trưng của Đà Lạt!',
    N'/images/blog/dalat-food.jpg',
    '2025-02-10',
    N'Food Blogger'
);

-- 5. Mùa nào đẹp nhất để du lịch Đà Lạt?
INSERT INTO BlogPosts (Title, Content, ImageUrl, PostedDate, Author)
VALUES (
    N'Mùa nào đẹp nhất để du lịch Đà Lạt?',
    N'Đà Lạt có khí hậu ôn đới quanh năm, nhưng mỗi mùa lại có vẻ đẹp riêng:

**Mùa khô (Tháng 11 - Tháng 4):**
- Thời tiết khô ráo, nắng đẹp, lý tưởng cho các hoạt động ngoài trời
- Nhiều hoa nở rộ, đặc biệt là hoa mai anh đào vào tháng 1-2
- Đây là mùa cao điểm du lịch

**Mùa mưa (Tháng 5 - Tháng 10):**
- Mưa nhiều nhưng không quá lạnh
- Cảnh quan xanh tươi, thác nước đầy nước
- Giá dịch vụ thường rẻ hơn
- Phù hợp cho những ai thích không gian yên tĩnh

**Mùa đẹp nhất:** Tháng 12 - Tháng 3 khi hoa nở rộ và thời tiết đẹp nhất.

**Lưu ý:** Nên đặt phòng khách sạn trước khi đi, đặc biệt vào dịp lễ tết và cuối tuần.',
    N'/images/blog/dalat-season.jpg',
    '2025-02-15',
    N'Travel Expert'
);

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT 
--     Id,
--     Title,
--     Author,
--     PostedDate,
--     LEFT(Content, 100) AS ContentPreview
-- FROM BlogPosts
-- ORDER BY PostedDate DESC;













































