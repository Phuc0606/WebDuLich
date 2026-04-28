-- ============================================
-- SCRIPT INSERT DỮ LIỆU CHO BẢNG FESTIVALS
-- Lễ hội và sự kiện tại Đà Lạt
-- ============================================

-- Xóa dữ liệu cũ (nếu cần)
-- DELETE FROM Festivals;

-- Reset Identity (nếu cần)
-- DBCC CHECKIDENT ('Festivals', RESEED, 0);

-- ============================================
-- INSERT CÁC LỄ HỘI VÀ SỰ KIỆN
-- ============================================

-- 1. Lễ hội Hoa Đà Lạt
INSERT INTO Festivals (Title, Description, ImageUrl, StartDate, EndDate, Location, EventType, Time, TicketPrice, ContactInfo, Website, Content, CreatedDate, UpdatedDate, IsActive)
VALUES (
    N'Lễ hội Hoa Đà Lạt',
    N'Lễ hội hoa lớn nhất tại Đà Lạt với hàng trăm loài hoa đẹp mắt, các hoạt động văn hóa và nghệ thuật.',
    N'/images/festivals/hoa-dalat.jpg',
    '2025-12-20',
    '2026-01-05',
    N'Trung tâm thành phố Đà Lạt',
    N'Lễ hội',
    N'08:00 - 22:00',
    N'Miễn phí',
    N'Ban tổ chức Lễ hội Hoa Đà Lạt - ĐT: 0263.3821.234',
    N'https://www.dalat.gov.vn',
    N'Lễ hội Hoa Đà Lạt là sự kiện văn hóa lớn nhất trong năm tại thành phố ngàn hoa. Lễ hội quy tụ hàng trăm loài hoa đẹp mắt, các màn trình diễn nghệ thuật, triển lãm và nhiều hoạt động vui chơi giải trí.',
    GETDATE(),
    NULL,
    1
);

-- 2. Festival Cà Phê Đà Lạt
INSERT INTO Festivals (Title, Description, ImageUrl, StartDate, EndDate, Location, EventType, Time, TicketPrice, ContactInfo, Website, Content, CreatedDate, UpdatedDate, IsActive)
VALUES (
    N'Festival Cà Phê Đà Lạt',
    N'Sự kiện tôn vinh văn hóa cà phê Đà Lạt với các gian hàng cà phê đặc sản, workshop và cuộc thi pha chế.',
    N'/images/festivals/ca-phe-dalat.jpg',
    '2025-11-15',
    '2025-11-17',
    N'Khu vực Hồ Xuân Hương',
    N'Sự kiện văn hóa',
    N'09:00 - 21:00',
    N'50,000 VNĐ',
    N'Hiệp hội Cà phê Đà Lạt - ĐT: 0263.3825.678',
    NULL,
    N'Festival Cà Phê Đà Lạt là nơi quy tụ các thương hiệu cà phê nổi tiếng, các nghệ nhân pha chế và những người yêu thích cà phê. Sự kiện bao gồm triển lãm, workshop và các hoạt động trải nghiệm.',
    GETDATE(),
    NULL,
    1
);

-- 3. Lễ hội Đèn lồng Đà Lạt
INSERT INTO Festivals (Title, Description, ImageUrl, StartDate, EndDate, Location, EventType, Time, TicketPrice, ContactInfo, Website, Content, CreatedDate, UpdatedDate, IsActive)
VALUES (
    N'Lễ hội Đèn lồng Đà Lạt',
    N'Lễ hội đèn lồng rực rỡ với hàng ngàn chiếc đèn lồng được trang trí khắp thành phố.',
    N'/images/festivals/den-long.jpg',
    '2025-12-25',
    '2026-01-10',
    N'Toàn thành phố Đà Lạt',
    N'Lễ hội',
    N'18:00 - 23:00',
    N'Miễn phí',
    N'UBND thành phố Đà Lạt',
    NULL,
    N'Lễ hội Đèn lồng Đà Lạt tạo nên một không gian lung linh, huyền ảo với hàng ngàn chiếc đèn lồng được trang trí khắp các con phố, tạo nên một khung cảnh đẹp mắt và thu hút du khách.',
    GETDATE(),
    NULL,
    1
);

-- 4. Sự kiện Marathon Đà Lạt
INSERT INTO Festivals (Title, Description, ImageUrl, StartDate, EndDate, Location, EventType, Time, TicketPrice, ContactInfo, Website, Content, CreatedDate, UpdatedDate, IsActive)
VALUES (
    N'Marathon Đà Lạt 2025',
    N'Giải chạy marathon quốc tế tại Đà Lạt với các cự ly 5km, 10km, 21km và 42km.',
    N'/images/festivals/marathon.jpg',
    '2025-10-20',
    '2025-10-20',
    N'Trung tâm thành phố Đà Lạt',
    N'Sự kiện thể thao',
    N'05:00 - 12:00',
    N'500,000 - 1,500,000 VNĐ',
    N'Ban tổ chức Marathon Đà Lạt - Email: marathon@dalat.vn',
    N'https://marathon.dalat.vn',
    N'Marathon Đà Lạt là sự kiện thể thao lớn thu hút hàng ngàn vận động viên trong và ngoài nước. Giải chạy được tổ chức trên các tuyến đường đẹp nhất của thành phố với không khí trong lành và cảnh quan tuyệt đẹp.',
    GETDATE(),
    NULL,
    1
);

-- 5. Lễ hội Dâu tây Đà Lạt
INSERT INTO Festivals (Title, Description, ImageUrl, StartDate, EndDate, Location, EventType, Time, TicketPrice, ContactInfo, Website, Content, CreatedDate, UpdatedDate, IsActive)
VALUES (
    N'Lễ hội Dâu tây Đà Lạt',
    N'Lễ hội tôn vinh đặc sản dâu tây Đà Lạt với các hoạt động hái dâu, thưởng thức và mua sắm.',
    N'/images/festivals/dau-tay.jpg',
    '2025-12-01',
    '2025-12-31',
    N'Các vườn dâu tây Đà Lạt',
    N'Lễ hội nông nghiệp',
    N'07:00 - 17:00',
    N'100,000 - 200,000 VNĐ',
    N'Hiệp hội Nông dân Đà Lạt',
    NULL,
    N'Lễ hội Dâu tây Đà Lạt là dịp để du khách trải nghiệm hái dâu tây tươi ngon tại các vườn, thưởng thức các món ăn từ dâu tây và mua sắm các sản phẩm đặc sản.',
    GETDATE(),
    NULL,
    1
);

-- ============================================
-- KIỂM TRA DỮ LIỆU ĐÃ INSERT
-- ============================================
-- SELECT 
--     Id,
--     Title,
--     StartDate,
--     EndDate,
--     Location,
--     EventType,
--     IsActive
-- FROM Festivals
-- ORDER BY StartDate DESC;



