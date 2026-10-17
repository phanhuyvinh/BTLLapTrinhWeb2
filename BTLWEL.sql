-- ============================================================
-- MedCare Pharmacy - SQL Server Database Schema + Seed Data (Complete & Idempotent)
-- Bao gồm: schema, users, categories, suppliers, 299 products, inventory batches
-- Database : MedCarePharmacyDB
-- Fixed: không chèn NULL vào các cột string NON-nullable của Entity
-- ============================================================

IF DB_ID(N'MedCarePharmacyDB') IS NULL
BEGIN
    CREATE DATABASE MedCarePharmacyDB;
    PRINT N'Database MedCarePharmacyDB đã được tạo mới.';
END
ELSE
    PRINT N'Database MedCarePharmacyDB đã tồn tại - bỏ qua CREATE.';
GO

USE MedCarePharmacyDB;
GO

-- ==================== TABLES ====================

IF OBJECT_ID(N'[dbo].[User]', N'U') IS NULL
BEGIN
    CREATE TABLE [User] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        FullName        NVARCHAR(150)   NOT NULL,
        Phone           VARCHAR(20)     NOT NULL,
        Email           VARCHAR(150)    NOT NULL,
        PasswordHash    NVARCHAR(MAX)   NOT NULL,
        Role            VARCHAR(50)     NOT NULL CONSTRAINT DF_User_Role DEFAULT ('customer'),
        Status          VARCHAR(50)     NOT NULL CONSTRAINT DF_User_Status DEFAULT ('Active'),
        AvatarUrl       NVARCHAR(MAX)   NULL,
        CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_User_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_User PRIMARY KEY (Id),
        CONSTRAINT UQ_User_Email UNIQUE (Email)
    );
END
GO

IF OBJECT_ID(N'[dbo].[Category]', N'U') IS NULL
BEGIN
    CREATE TABLE [Category] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        [Description]   NVARCHAR(MAX)   NULL,
        CONSTRAINT PK_Category PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'[dbo].[Supplier]', N'U') IS NULL
BEGIN
    CREATE TABLE [Supplier] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Name            NVARCHAR(200)   NOT NULL,
        Phone           VARCHAR(20)     NULL,
        Email           VARCHAR(150)    NULL,
        Address         NVARCHAR(300)   NULL,
        Status          VARCHAR(50)     NOT NULL CONSTRAINT DF_Supplier_Status DEFAULT ('Active'),
        CONSTRAINT PK_Supplier PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'[dbo].[Product]', N'U') IS NULL
BEGIN
    CREATE TABLE [Product] (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        Name                NVARCHAR(200)   NOT NULL,
        ActiveIngredient    NVARCHAR(200)   NULL,          -- entity: string? → NULL OK
        Price               DECIMAL(18,2)   NOT NULL,
        Code                NVARCHAR(50)    NOT NULL CONSTRAINT DF_Product_Code DEFAULT (N''),
        StockQuantity       INT             NOT NULL CONSTRAINT DF_Product_StockQuantity DEFAULT (0),
        Unit                NVARCHAR(50)    NOT NULL CONSTRAINT DF_Product_Unit DEFAULT (N'Hộp'),
        [Description]       NVARCHAR(MAX)   NOT NULL CONSTRAINT DF_Product_Description DEFAULT (N''),  -- entity non-nullable
        ImageUrl            NVARCHAR(MAX)   NOT NULL CONSTRAINT DF_Product_ImageUrl DEFAULT (N''),     -- entity non-nullable
        CategoryId          INT             NULL,
        SupplierId          INT             NULL,
        CONSTRAINT PK_Product PRIMARY KEY (Id),
        CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES [Category](Id) ON DELETE SET NULL,
        CONSTRAINT FK_Product_Supplier FOREIGN KEY (SupplierId) REFERENCES [Supplier](Id) ON DELETE SET NULL
    );
END
GO

IF COL_LENGTH(N'dbo.Product', N'Code') IS NULL
    ALTER TABLE [dbo].[Product] ADD Code NVARCHAR(50) NOT NULL CONSTRAINT DF_Product_Code DEFAULT (N'');
GO

IF OBJECT_ID(N'[dbo].[InventoryBatch]', N'U') IS NULL
BEGIN
    CREATE TABLE [InventoryBatch] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        ProductId       INT             NOT NULL,
        LotNumber       VARCHAR(100)    NOT NULL,
        Quantity        INT             NOT NULL,
        MfgDate         DATETIME2       NOT NULL,
        ExpDate         DATETIME2       NOT NULL,
        ImportPrice     DECIMAL(18,2)   NOT NULL,
        CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_InventoryBatch_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_InventoryBatch PRIMARY KEY (Id),
        CONSTRAINT FK_InventoryBatch_Product FOREIGN KEY (ProductId) REFERENCES [Product](Id) ON DELETE NO ACTION
    );
END
GO

IF OBJECT_ID(N'[dbo].[Order]', N'U') IS NULL
BEGIN
    CREATE TABLE [Order] (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        UserId              INT             NULL,
        CustomerName        NVARCHAR(150)   NOT NULL,
        Phone               VARCHAR(20)     NOT NULL,
        Address             NVARCHAR(300)   NOT NULL,
        OrderType           VARCHAR(50)     NOT NULL CONSTRAINT DF_Order_OrderType DEFAULT ('Online'),
        Status              VARCHAR(50)     NOT NULL CONSTRAINT DF_Order_Status DEFAULT ('Pending'),
        PrescriptionUrl     NVARCHAR(MAX)   NULL,
        TotalAmount         DECIMAL(18,2)   NOT NULL,
        CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_Order_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_Order PRIMARY KEY (Id),
        CONSTRAINT FK_Order_User FOREIGN KEY (UserId) REFERENCES [User](Id) ON DELETE SET NULL
    );
END
GO

IF OBJECT_ID(N'[dbo].[OrderDetail]', N'U') IS NULL
BEGIN
    CREATE TABLE [OrderDetail] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        OrderId         INT             NOT NULL,
        ProductId       INT             NOT NULL,
        Quantity        INT             NOT NULL,
        UnitPrice       DECIMAL(18,2)   NOT NULL,
        CONSTRAINT PK_OrderDetail PRIMARY KEY (Id),
        CONSTRAINT FK_OrderDetail_Order FOREIGN KEY (OrderId) REFERENCES [Order](Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderDetail_Product FOREIGN KEY (ProductId) REFERENCES [Product](Id) ON DELETE NO ACTION
    );
END
GO

-- Keep product transaction and batch history when someone attempts to delete a product.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderDetail_Product' AND parent_object_id = OBJECT_ID(N'[dbo].[OrderDetail]'))
    ALTER TABLE [dbo].[OrderDetail] DROP CONSTRAINT FK_OrderDetail_Product;
ALTER TABLE [dbo].[OrderDetail] WITH CHECK ADD CONSTRAINT FK_OrderDetail_Product
    FOREIGN KEY (ProductId) REFERENCES [dbo].[Product](Id) ON DELETE NO ACTION;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryBatch_Product' AND parent_object_id = OBJECT_ID(N'[dbo].[InventoryBatch]'))
    ALTER TABLE [dbo].[InventoryBatch] DROP CONSTRAINT FK_InventoryBatch_Product;
ALTER TABLE [dbo].[InventoryBatch] WITH CHECK ADD CONSTRAINT FK_InventoryBatch_Product
    FOREIGN KEY (ProductId) REFERENCES [dbo].[Product](Id) ON DELETE NO ACTION;
GO

IF OBJECT_ID(N'[dbo].[ChatMessage]', N'U') IS NULL
BEGIN
    CREATE TABLE [ChatMessage] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        SenderId        INT             NOT NULL,
        ReceiverId      INT             NOT NULL,
        Content         NVARCHAR(MAX)   NOT NULL,
        ImageUrl        NVARCHAR(MAX)   NULL,
        Timestamp       DATETIME2       NOT NULL CONSTRAINT DF_ChatMessage_Timestamp DEFAULT (GETDATE()),
        IsRead          BIT             NOT NULL CONSTRAINT DF_ChatMessage_IsRead DEFAULT (0),
        CONSTRAINT PK_ChatMessage PRIMARY KEY (Id),
        CONSTRAINT FK_ChatMessage_Sender FOREIGN KEY (SenderId) REFERENCES [User](Id) ON DELETE NO ACTION,
        CONSTRAINT FK_ChatMessage_Receiver FOREIGN KEY (ReceiverId) REFERENCES [User](Id) ON DELETE NO ACTION
    );
END
GO

IF OBJECT_ID(N'[dbo].[Prescription]', N'U') IS NULL
BEGIN
    CREATE TABLE [Prescription] (
        Id              INT             IDENTITY(1,1) NOT NULL,
        UserId          INT             NOT NULL,
        FileName        NVARCHAR(255)   NOT NULL,
        FileUrl         NVARCHAR(MAX)   NOT NULL,
        FileType        VARCHAR(20)     NOT NULL CONSTRAINT DF_Prescription_FileType DEFAULT ('image'),
        UploadedAt      DATETIME2       NOT NULL CONSTRAINT DF_Prescription_UploadedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_Prescription PRIMARY KEY (Id),
        CONSTRAINT FK_Prescription_User FOREIGN KEY (UserId) REFERENCES [User](Id) ON DELETE CASCADE
    );
END
GO

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_CategoryId' AND object_id = OBJECT_ID(N'dbo.Product', N'U'))
    CREATE INDEX IX_Product_CategoryId ON dbo.Product(CategoryId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_SupplierId' AND object_id = OBJECT_ID(N'dbo.Product', N'U'))
    CREATE INDEX IX_Product_SupplierId ON dbo.Product(SupplierId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InventoryBatch_ProductId' AND object_id = OBJECT_ID(N'dbo.InventoryBatch', N'U'))
    CREATE INDEX IX_InventoryBatch_ProductId ON dbo.InventoryBatch(ProductId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Order_UserId' AND object_id = OBJECT_ID(N'dbo.[Order]', N'U'))
    CREATE INDEX IX_Order_UserId ON dbo.[Order](UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Order_Status' AND object_id = OBJECT_ID(N'dbo.[Order]', N'U'))
    CREATE INDEX IX_Order_Status ON dbo.[Order](Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderDetail_OrderId' AND object_id = OBJECT_ID(N'dbo.OrderDetail', N'U'))
    CREATE INDEX IX_OrderDetail_OrderId ON dbo.OrderDetail(OrderId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderDetail_ProductId' AND object_id = OBJECT_ID(N'dbo.OrderDetail', N'U'))
    CREATE INDEX IX_OrderDetail_ProductId ON dbo.OrderDetail(ProductId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessage_SenderId' AND object_id = OBJECT_ID(N'dbo.ChatMessage', N'U'))
    CREATE INDEX IX_ChatMessage_SenderId ON dbo.ChatMessage(SenderId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessage_ReceiverId' AND object_id = OBJECT_ID(N'dbo.ChatMessage', N'U'))
    CREATE INDEX IX_ChatMessage_ReceiverId ON dbo.ChatMessage(ReceiverId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Prescription_UserId' AND object_id = OBJECT_ID(N'dbo.Prescription', N'U'))
    CREATE INDEX IX_Prescription_UserId ON dbo.Prescription(UserId);
GO

-- ==================== FIX DATA NULL (nếu DB cũ) ====================
IF OBJECT_ID(N'[dbo].[Product]', N'U') IS NOT NULL
BEGIN
    -- Older installations may already have had a nullable Code column.
    UPDATE [Product] SET Code = N'' WHERE Code IS NULL;
    UPDATE [Product] SET ImageUrl = N'' WHERE ImageUrl IS NULL;
    UPDATE [Product] SET [Description] = N'' WHERE [Description] IS NULL;
    UPDATE [Product] SET Unit = N'Hộp' WHERE Unit IS NULL OR LTRIM(RTRIM(Unit)) = '';
    UPDATE [Product] SET Name = N'(Không tên)' WHERE Name IS NULL OR LTRIM(RTRIM(Name)) = '';

    -- Match the non-nullable Product entity even when upgrading an older schema.
    ALTER TABLE [dbo].[Product] ALTER COLUMN Code NVARCHAR(50) NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN ImageUrl NVARCHAR(MAX) NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN [Description] NVARCHAR(MAX) NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN Unit NVARCHAR(50) NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN Name NVARCHAR(200) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[Product]') AND c.name = N'Code')
        ALTER TABLE [dbo].[Product] ADD CONSTRAINT DF_Product_Code DEFAULT (N'') FOR Code;
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[Product]') AND c.name = N'ImageUrl')
        ALTER TABLE [dbo].[Product] ADD CONSTRAINT DF_Product_ImageUrl DEFAULT (N'') FOR ImageUrl;
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[Product]') AND c.name = N'Description')
        ALTER TABLE [dbo].[Product] ADD CONSTRAINT DF_Product_Description DEFAULT (N'') FOR [Description];
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[Product]') AND c.name = N'Unit')
        ALTER TABLE [dbo].[Product] ADD CONSTRAINT DF_Product_Unit DEFAULT (N'Hộp') FOR Unit;
END
GO

-- ==================== SEED DATA ====================

-- Không tạo sẵn user hoặc mật khẩu dùng chung. Tạo admin đầu tiên qua
-- POST /api/auth/bootstrap-admin với mật khẩu mạnh.

-- Đồng bộ Category: đảm bảo đúng 8 danh mục Id 1..8 (cập nhật nếu đã có, chèn nếu thiếu)
-- Lưu ý: không xóa Category đang được Product tham chiếu; chỉ MERGE theo Id.
SET IDENTITY_INSERT [dbo].[Category] ON;

MERGE [dbo].[Category] AS t
USING (VALUES
    (1, N'Sơ cứu',                     N'Dụng cụ và thuốc sơ cứu, băng gạc, sát khuẩn'),
    (2, N'Dị ứng',                     N'Thuốc kháng histamin, viêm mũi dị ứng, mày đay'),
    (3, N'Chăm sóc da',                N'Kem dưỡng, trị nấm, chống nắng, chăm sóc da'),
    (4, N'Chăm sóc sức khỏe',          N'Thực phẩm chức năng, collagen, hỗ trợ gan khớp'),
    (5, N'Vitamin và khoáng chất',     N'Vitamin, khoáng chất, bổ sung dinh dưỡng'),
    (6, N'Tiêu hóa',                   N'Thuốc tiêu hóa, dạ dày, men vi sinh, táo bón'),
    (7, N'Thuốc giảm đau',             N'Giảm đau, hạ sốt, kháng viêm'),
    (8, N'Tai mũi họng',               N'Thuốc tai mũi họng, xịt mũi, siro ho')
) AS s (Id, Name, [Description])
ON t.Id = s.Id
WHEN MATCHED THEN
    UPDATE SET Name = s.Name, [Description] = s.[Description]
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, [Description]) VALUES (s.Id, s.Name, s.[Description]);

SET IDENTITY_INSERT [dbo].[Category] OFF;
GO

-- Đồng bộ Supplier: đảm bảo đúng 8 nhà cung cấp Id 1..8
SET IDENTITY_INSERT [dbo].[Supplier] ON;

MERGE [dbo].[Supplier] AS t
USING (VALUES
    (1, N'DHG Pharma',          '02923891000', 'contact@dhgpharma.com.vn', N'Cần Thơ',              'Active'),
    (2, N'Long Châu',           '19006868',    'contact@longchau.com',     N'TP. Hồ Chí Minh',      'Active'),
    (3, N'Nhà thuốc An Khang',  '18006928',    'contact@ankhang.vn',       N'TP. Hồ Chí Minh',      'Active'),
    (4, N'Pharmacity',          '18006821',    'contact@pharmacity.vn',    N'TP. Hồ Chí Minh',      'Active'),
    (5, N'Sanofi Việt Nam',     '02838214100', 'info@sanofi.com.vn',       N'TP. Hồ Chí Minh',      'Active'),
    (6, N'Traphaco',            '02438641909', 'info@traphaco.com.vn',     N'Hà Nội',               'Active'),
    (7, N'Vinmec',              '02439743556', 'info@vinmec.com',          N'Hà Nội',               'Active'),
    (8, N'Zuellig Pharma',      '02838233333', 'info@zuelligpharma.com',   N'TP. Hồ Chí Minh',      'Active')
) AS s (Id, Name, Phone, Email, Address, Status)
ON t.Id = s.Id
WHEN MATCHED THEN
    UPDATE SET Name = s.Name, Phone = s.Phone, Email = s.Email, Address = s.Address, Status = s.Status
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Phone, Email, Address, Status) VALUES (s.Id, s.Name, s.Phone, s.Email, s.Address, s.Status);

SET IDENTITY_INSERT [dbo].[Supplier] OFF;
GO

-- ==================== SEED PRODUCTS ====================
BEGIN TRY
    BEGIN TRANSACTION;

    -- Xóa dữ liệu seed cũ để import lại.
    -- Xóa OrderDetail trỏ tới Product (đơn hàng demo) để tránh lỗi FK;
    -- giữ nguyên bảng Order (chỉ mất chi tiết dòng hàng demo).
    DELETE FROM [OrderDetail] WHERE ProductId IN (SELECT Id FROM [Product]);
    DELETE FROM [InventoryBatch];
    DELETE FROM [Product];

    -- Reset identity
    DBCC CHECKIDENT ('[Product]', RESEED, 0);

    SET IDENTITY_INSERT [dbo].[Product] ON;

    INSERT INTO [dbo].[Product]
        (Id, Name, ActiveIngredient, Price, StockQuantity, Unit, [Description], ImageUrl, CategoryId, SupplierId)
    VALUES
    (1, N'Băng cá nhân không thấm nước', N'Vải/PU, keo dán y tế', 25000, 1, N'hộp', N'Băng dán vết thương nhỏ, chống thấm nước, bảo vệ vết thương khỏi bụi bẩn và vi khuẩn.', N'/images/Bangcanhankhongthamnuoc.jpg', 1, 7),
    (2, N'Băng dính y tế', N'Keo dán y tế (vải/giấy)', 20000, 2, N'cuộn', N'Băng keo cố định gạc, ống truyền và dụng cụ y tế, ít gây kích ứng da.', N'/images/Bangdinhyte.jpg', 1, 6),
    (3, N'Băng gạc', N'Cotton 100%', 15000, 1, N'cuộn', N'Băng cuộn cotton thấm hút tốt, dùng băng vết thương và cố định gạc.', N'/images/Banggac.jpg', 1, 2),
    (4, N'Băng tam giác', N'Vải cotton', 20000, 1, N'cái', N'Băng vải hình tam giác dùng làm dây đeo treo tay, băng ép cầm máu trong sơ cứu.', N'/images/Bangtamgiac.jpg', 1, 4),
    (5, N'Băng thun 3 móc', N'Cotton, sợi thun', 25000, 2, N'cuộn', N'Băng thun đàn hồi có móc cài, dùng băng ép, cố định khớp khi bong gân, trật khớp.', N'/images/Bangthun3moc.jpg', 1, 7),
    (6, N'Cáng cứu thương', N'Khung nhôm, vải bạt', 650000, 1, N'cái', N'Cáng gấp gọn dùng vận chuyển người bị thương, chịu lực tốt, dễ mang theo.', N'/images/Cangcuuthuong.webp', 1, 4),
    (7, N'Cồn y tế 70 độ', N'Ethanol 70%', 15000, 1, N'chai', N'Dung dịch sát khuẩn da, dụng cụ y tế và vùng da quanh vết thương.', N'/images/Conyte70.jpg', 1, 6),
    (8, N'Dây garo cầm máu', N'Dây cao su đàn hồi', 35000, 1, N'cái', N'Dây garo dùng cầm máu chi khi chảy máu nặng, hỗ trợ lấy máu và truyền dịch.', N'/images/Daygarocammau.jpg', 1, 6),
    (9, N'Dung dịch rửa vết thương', N'Polyhexanide, Betaine', 120000, 2, N'chai', N'Dung dịch làm sạch và sát khuẩn nhẹ vết thương, vết bỏng, giúp hạn chế nhiễm khuẩn.', N'/images/Dungdichruavetthuong.jpg', 1, 6),
    (10, N'Dung dịch sát khuẩn tay', N'Ethanol, Chlorhexidine', 60000, 2, N'chai', N'Dung dịch rửa tay sát khuẩn, diệt khuẩn nhanh, dùng trước khi chăm sóc vết thương.', N'/images/Dungdichsatkhuantay.jpg', 1, 1),
    (11, N'Gạc cuộn y tế', N'Cotton', 20000, 1, N'cuộn', N'Gạc cuộn mềm, thấm hút tốt, dùng băng bó và cố định vết thương.', N'/images/Gaccuonyte.jpg', 1, 2),
    (12, N'Gạc mỏng chống dính', N'Gạc tẩm TLC-NOSF (Urgotul)', 95000, 2, N'hộp', N'Gạc tẩm chống dính, giúp thay băng không đau và thúc đẩy lành vết thương.', N'/images/Gacmochongdinh.webp', 1, 5),
    (13, N'Gạc y tế vô trùng', N'Cotton vô trùng', 20000, 2, N'gói', N'Miếng gạc vô trùng dùng đắp, che phủ và thấm dịch vết thương.', N'/images/Gacytevotrung.jpg', 1, 2),
    (14, N'Găng tay y tế', N'Nitrile/Latex', 120000, 1, N'hộp', N'Găng tay dùng một lần, bảo vệ người chăm sóc khi tiếp xúc vết thương, dịch cơ thể.', N'/images/Gangtayyte.jpg', 1, 5),
    (15, N'Gel rửa tay khô', N'Ethanol 70%, glycerin', 55000, 1, N'chai', N'Gel rửa tay không cần nước, diệt khuẩn nhanh và giữ ẩm cho da tay.', N'/images/Gelruataykho.jpg', 1, 4),
    (16, N'Giấy lau sát khuẩn', N'Cồn isopropyl', 45000, 2, N'gói', N'Khăn giấy tẩm cồn dùng lau sát khuẩn da và bề mặt dụng cụ.', N'/images/Giaylausatkhuan.jpg', 1, 4),
    (17, N'Kéo y tế đầu tù', N'Thép không gỉ', 45000, 2, N'cái', N'Kéo đầu tù dùng cắt băng, gạc an toàn, không làm tổn thương da.', N'/images/Keoytedautu.jpg', 1, 5),
    (18, N'Khay quả đậu y tế', N'Inox', 60000, 2, N'cái', N'Khay hình quả đậu bằng inox đựng dụng cụ, bông gạc khi thay băng, chăm sóc vết thương.', N'/images/khayquadauyte.jpg', 1, 7),
    (19, N'Mặt nạ hô hấp hồi sức bỏ túi', N'Nhựa y tế, van một chiều', 85000, 2, N'cái', N'Mặt nạ hỗ trợ hô hấp nhân tạo (CPR) bảo vệ người cấp cứu khi thổi ngạt.', N'/images/Matnahoisucbotui.webp', 1, 4),
    (20, N'Máy đo huyết áp bắp tay', N'Thiết bị điện tử (Omron)', 950000, 2, N'cái', N'Máy đo huyết áp tự động, hiển thị huyết áp và nhịp tim, dùng tại nhà.', N'/images/Maydohuyetap.jpg', 1, 4),
    (21, N'Máy đo nồng độ oxy SpO2', N'Cảm biến quang học', 350000, 2, N'cái', N'Máy kẹp ngón tay đo SpO2 và nhịp mạch, theo dõi nhanh tình trạng hô hấp.', N'/images/Maydooxi.jpg', 1, 5),
    (22, N'Miếng bảo vệ vết thương Nexcare', N'Màng polyurethane', 40000, 1, N'hộp', N'Miếng dán trong suốt bảo vệ vết thương, vết loét tỳ đè, giữ môi trường lành thương.', N'/images/Miengbaovevetthuong.jpg', 1, 4),
    (23, N'Miếng dán bảo vệ mắt', N'Vải không dệt, keo y tế', 30000, 1, N'hộp', N'Miếng dán che chắn và bảo vệ mắt sau chấn thương hoặc phẫu thuật.', N'/images/Miengdanbaovemat.jpg', 1, 6),
    (24, N'Đai nẹp cẳng tay', N'Vải, thanh nẹp', 120000, 2, N'cái', N'Đai cố định và nâng đỡ cẳng tay, hỗ trợ khi gãy xương, chấn thương.', N'/images/Nepcanhtay.webp', 1, 4),
    (25, N'Giày nẹp cố định cổ chân', N'Nhựa cứng, đệm khí', 650000, 1, N'cái', N'Giày nẹp cố định cổ chân và bàn chân sau chấn thương, bó bột hoặc phẫu thuật.', N'/images/Nepchan.jpg', 1, 4),
    (26, N'Nẹp cổ tay', N'Vải thun, thanh nẹp', 130000, 2, N'cái', N'Nẹp hỗ trợ cố định cổ tay khi bong gân, viêm gân, hội chứng ống cổ tay.', N'/images/Nepcotay.jpg', 1, 1),
    (27, N'Nẹp ngón tay', N'Nhôm bọc xốp', 45000, 2, N'cái', N'Nẹp cố định ngón tay khi gãy, trật khớp hoặc chấn thương.', N'/images/Nepngontay.jpg', 1, 5),
    (28, N'Nhiệt kế thủy ngân', N'Thủy ngân', 35000, 1, N'cái', N'Nhiệt kế thủy tinh đo nhiệt độ cơ thể, độ chính xác cao.', N'/images/Nhietke.jpg', 1, 5),
    (29, N'Nhiệt kế điện tử', N'Cảm biến nhiệt', 70000, 2, N'cái', N'Nhiệt kế điện tử đo nhiệt độ nhanh, hiển thị số, an toàn cho trẻ em.', N'/images/Nhietkedientu.jpg', 1, 6),
    (30, N'Nhiệt kế hồng ngoại', N'Cảm biến hồng ngoại', 450000, 2, N'cái', N'Nhiệt kế đo trán không tiếp xúc, cho kết quả trong vài giây.', N'/images/Nhietkedientu2.jpg', 1, 4),
    (31, N'Nhíp y tế', N'Thép không gỉ', 25000, 1, N'cái', N'Nhíp gắp bông gạc, dị vật nhỏ khi sơ cứu và thay băng.', N'/images/Nhipyte.jpg', 1, 6),
    (32, N'Nước muối sinh lý', N'Natri clorid 0,9%', 10000, 1, N'chai', N'Dung dịch rửa vết thương, vệ sinh mắt, mũi, an toàn và dịu nhẹ.', N'/images/Nuocmuoisinhly.jpg', 1, 3),
    (33, N'Oxy già', N'Hydrogen peroxide 3%', 8000, 2, N'chai', N'Dung dịch sát khuẩn, rửa vết thương nhỏ, loại bỏ máu bẩn và dị vật.', N'/images/Oxygia.jpg', 1, 1),
    (34, N'Povidone sát khuẩn', N'Povidone iodine 10%', 20000, 1, N'chai', N'Dung dịch sát khuẩn ngoài da, vết thương, vết bỏng nhỏ và trước tiêm chích.', N'/images/povidonesatkhuan.jpg', 1, 5),
    (35, N'Que thử đường huyết', N'Glucose oxidase', 280000, 1, N'hộp', N'Que thử dùng với máy đo đường huyết, theo dõi đường huyết tại nhà.', N'/images/Quethuduonghuyet.webp', 1, 6),
    (36, N'Túi chườm nóng lạnh', N'Cao su y tế', 55000, 1, N'cái', N'Túi chườm giảm đau, giảm sưng, hạ sốt hoặc làm ấm cơ thể.', N'/images/Tuichuom.jpg', 1, 5),
    (37, N'Túi đựng chất thải y tế', N'Nhựa PE', 30000, 2, N'cuộn', N'Túi phân loại và đựng chất thải y tế, rác thải sinh hoạt.', N'/images/tuidungchatthaiyte.jpg', 1, 1),
    (38, N'Túi sơ cứu gia đình', N'Bộ dụng cụ sơ cứu cơ bản', 250000, 1, N'túi', N'Túi đựng bộ dụng cụ sơ cứu gồm băng gạc, cồn, kéo, nhiệt kế dùng khi cần.', N'/images/Tuisocuugiadinh.jpg', 1, 6),
    (39, N'Túi treo tay', N'Vải cotton', 60000, 2, N'cái', N'Túi đỡ và treo tay khi gãy xương, chấn thương vai, cánh tay.', N'/images/Tuitreotay.webp', 1, 6),
    (40, N'Acrivastine', N'Acrivastine', 90000, 2, N'hộp', N'Thuốc kháng histamin giảm triệu chứng viêm mũi dị ứng, mày đay.', N'/images/Acrivastine.jpg', 2, 8),
    (41, N'Aerius 5mg', N'Desloratadine', 130000, 2, N'hộp', N'Kháng histamin thế hệ mới điều trị viêm mũi dị ứng và mày đay mạn tính.', N'/images/Aerius5mg.webp', 2, 2),
    (42, N'Azelastine nhỏ mắt', N'Azelastine HCl', 150000, 2, N'lọ', N'Thuốc nhỏ mắt giảm ngứa, đỏ mắt do viêm kết mạc dị ứng.', N'/images/Azelastineeyedrops.jpg', 2, 2),
    (43, N'Bepotastine nhỏ mắt', N'Bepotastine besilate 1,5%', 200000, 1, N'lọ', N'Thuốc nhỏ mắt kháng histamin điều trị viêm kết mạc dị ứng.', N'/images/Bepotastineeyedrops.jpg', 2, 1),
    (44, N'Betamethasone valerate cream', N'Betamethasone valerate 0,1%', 25000, 2, N'tuýp', N'Kem corticoid bôi ngoài da trị viêm da, eczema, ngứa do dị ứng.', N'/images/Betamethasonevaleratecream.webp', 2, 8),
    (45, N'Bilastine 20mg', N'Bilastine 20mg', 150000, 2, N'hộp', N'Kháng histamin H1 điều trị viêm mũi dị ứng và mày đay, ít gây buồn ngủ.', N'/images/Bilastine20mg.jpg', 2, 6),
    (46, N'Bilaxten 20mg', N'Bilastine 20mg', 160000, 1, N'hộp', N'Viên uống giảm hắt hơi, ngứa mũi, mày đay do dị ứng.', N'/images/Bilaxten20mg.webp', 2, 3),
    (47, N'Cetirizine 10mg', N'Cetirizine dihydrochloride', 30000, 1, N'hộp', N'Kháng histamin giảm triệu chứng viêm mũi dị ứng, mày đay.', N'/images/Cetirizine10mg.webp', 2, 7),
    (48, N'Chlorpheniramine 4mg', N'Chlorpheniramine maleate', 10000, 1, N'hộp', N'Kháng histamin thế hệ 1, giảm sổ mũi, ngứa, hắt hơi do dị ứng.', N'/images/Chlorpheniramine4mg.webp', 2, 2),
    (49, N'Clarityne', N'Loratadine 10mg', 60000, 1, N'hộp', N'Kháng histamin ít gây buồn ngủ, điều trị viêm mũi dị ứng và mày đay.', N'/images/Clarityne.jpg', 2, 8),
    (50, N'Clobetasol propionate cream', N'Clobetasol propionate 0,05%', 20000, 1, N'tuýp', N'Kem corticoid mạnh trị viêm da, vảy nến, chàm.', N'/images/Clobetasolpropionatecream.webp', 2, 8),
    (51, N'Desonide cream', N'Desonide 0,05%', 60000, 2, N'tuýp', N'Kem corticoid nhẹ trị viêm da cơ địa, ngứa, dị ứng da.', N'/images/Desonidecream.jpg', 2, 3),
    (52, N'Ebastine 20mg', N'Ebastine 20mg', 100000, 2, N'hộp', N'Kháng histamin điều trị viêm mũi dị ứng, mày đay.', N'/images/Ebastine20mg.jpg', 2, 7),
    (53, N'Elomet cream', N'Mometasone furoate 0,1%', 45000, 1, N'tuýp', N'Kem bôi giảm viêm, ngứa trong viêm da, vảy nến.', N'/images/Elometcream.jpg', 2, 1),
    (54, N'Eumovate cream', N'Clobetasone butyrate 0,05%', 70000, 2, N'tuýp', N'Kem corticoid trung bình trị chàm, viêm da tiếp xúc, ngứa.', N'/images/Eumovatecream.jpg', 2, 4),
    (55, N'Fenistil Gel', N'Dimetindene maleate 0,1%', 85000, 2, N'tuýp', N'Gel bôi giảm ngứa do côn trùng đốt, bỏng nắng nhẹ, mày đay.', N'/images/FenistilGel.webp', 2, 8),
    (56, N'Fexofenadine 60mg', N'Fexofenadine HCl 60mg', 80000, 1, N'hộp', N'Kháng histamin không gây buồn ngủ trị viêm mũi dị ứng.', N'/images/Fexofenadine60mg.webp', 2, 3),
    (57, N'Fluocinolone acetonide cream', N'Fluocinolone acetonide 0,025%', 15000, 1, N'tuýp', N'Kem corticoid trị viêm da, eczema, ngứa.', N'/images/Fluocinoloneacetonidecream.webp', 2, 1),
    (58, N'Hydrocortisone 1% cream', N'Hydrocortisone 1%', 20000, 1, N'tuýp', N'Kem corticoid nhẹ trị viêm da, côn trùng cắn, ngứa.', N'/images/Hydrocortisone1_cream.jpg', 2, 4),
    (59, N'Hydroxyzine 25mg', N'Hydroxyzine HCl 25mg', 60000, 2, N'lọ', N'Thuốc kháng histamin giảm ngứa, mày đay và lo âu nhẹ.', N'/images/Hydroxyzine25mg.jpg', 2, 6),
    (60, N'Ketotifen 1mg', N'Ketotifen fumarate 1mg', 40000, 1, N'hộp', N'Phòng ngừa hen phế quản dị ứng và điều trị viêm mũi dị ứng.', N'/images/Ketotifen.jpg', 2, 5),
    (61, N'Ketotifen nhỏ mắt', N'Ketotifen fumarate 0,025%', 90000, 1, N'lọ', N'Thuốc nhỏ mắt giảm ngứa, đỏ do viêm kết mạc dị ứng.', N'/images/Ketotifeneyedrops.webp', 2, 7),
    (62, N'Levocetirizine 5mg', N'Levocetirizine dihydrochloride 5mg', 70000, 2, N'hộp', N'Kháng histamin điều trị viêm mũi dị ứng, mày đay.', N'/images/Levocetirizine5mg.png', 2, 3),
    (63, N'Mometasone furoate cream', N'Mometasone furoate 0,1%', 35000, 1, N'tuýp', N'Kem corticoid bôi giảm viêm, ngứa các bệnh da dị ứng.', N'/images/Mometasonefuroatecream.webp', 2, 4),
    (64, N'Montelukast 10mg', N'Montelukast sodium 10mg', 180000, 2, N'hộp', N'Phòng ngừa hen phế quản, giảm triệu chứng viêm mũi dị ứng.', N'/images/Montelukast10mg.png', 2, 1),
    (65, N'Olopatadine nhỏ mắt', N'Olopatadine HCl 0,1%', 220000, 1, N'lọ', N'Thuốc nhỏ mắt điều trị ngứa mắt do viêm kết mạc dị ứng.', N'/images/Olopatadineeyedrops.webp', 2, 7),
    (66, N'Pataday nhỏ mắt', N'Olopatadine HCl 0,2%', 280000, 1, N'lọ', N'Thuốc nhỏ mắt dùng một lần mỗi ngày giảm ngứa mắt dị ứng.', N'/images/Patadayeyedrops.jpg', 2, 4),
    (67, N'Pimecrolimus cream', N'Pimecrolimus 1%', 250000, 1, N'tuýp', N'Kem ức chế calcineurin trị viêm da cơ địa nhẹ đến vừa.', N'/images/Pimecrolimuscream.png', 2, 3),
    (68, N'Singulair', N'Montelukast 10mg', 400000, 1, N'hộp', N'Thuốc dự phòng hen và viêm mũi dị ứng theo mùa.', N'/images/Singulair.jpg', 2, 2),
    (69, N'Tacrolimus ointment', N'Tacrolimus 0,1%', 180000, 1, N'tuýp', N'Thuốc mỡ ức chế miễn dịch tại chỗ trị viêm da cơ địa.', N'/images/Tacrolimusointment.webp', 2, 3),
    (70, N'Telfast 120mg', N'Fexofenadine HCl 120mg', 110000, 1, N'hộp', N'Kháng histamin giảm triệu chứng viêm mũi dị ứng theo mùa.', N'/images/Telfast120mg.jpg', 2, 1),
    (71, N'Telfast HD 180mg', N'Fexofenadine HCl 180mg', 140000, 2, N'hộp', N'Kháng histamin liều cao điều trị mày đay mạn tính và viêm mũi dị ứng.', N'/images/Telfast180mg.webp', 2, 6),
    (72, N'Xolair', N'Omalizumab 150mg', 6500000, 1, N'hộp', N'Thuốc sinh học điều trị hen dị ứng nặng và mày đay mạn tính tự phát.', N'/images/Xolairomalizumab.jpg', 2, 1),
    (73, N'Xyzal 5mg', N'Levocetirizine 5mg', 120000, 2, N'hộp', N'Kháng histamin thế hệ mới điều trị viêm mũi dị ứng, mày đay.', N'/images/Xyzal5mg.webp', 2, 5),
    (74, N'Zyrtec siro', N'Cetirizine dihydrochloride 1mg/ml', 90000, 2, N'chai', N'Siro kháng histamin cho trẻ em giảm các triệu chứng dị ứng.', N'/images/zyrtec.jpg', 2, 3),
    (75, N'Agiclovir 5%', N'Aciclovir 5%', 15000, 1, N'tuýp', N'Kem bôi trị nhiễm Herpes simplex ở môi và da.', N'/images/Agiclovir.webp', 3, 6),
    (76, N'Antifungal Powder', N'Miconazole, kẽm oxide', 70000, 1, N'lọ', N'Phấn rắc chống nấm, giảm ngứa, hăm kẽ chân và nếp gấp.', N'/images/AntifungalPowder.webp', 3, 4),
    (77, N'Bông tẩy trang', N'Cotton 100%', 25000, 2, N'gói', N'Bông tẩy trang mềm mại, thấm hút tốt, an toàn cho da nhạy cảm.', N'/images/Bongtaytrang.avif', 3, 7),
    (78, N'Calamine lotion', N'Calamine, kẽm oxide', 55000, 1, N'chai', N'Dung dịch làm dịu, giảm ngứa do côn trùng cắn, phát ban.', N'/images/Calamine.webp', 3, 6),
    (79, N'Calamine cream', N'Calamine, kẽm oxide', 35000, 1, N'tuýp', N'Kem làm dịu da, giảm ngứa và kích ứng nhẹ.', N'/images/CalamineCream.webp', 3, 4),
    (80, N'Kem dưỡng Ceramide', N'Ceramide, glycerin', 250000, 1, N'tuýp', N'Kem dưỡng phục hồi hàng rào bảo vệ da, giữ ẩm cho da khô, nhạy cảm.', N'/images/Ceramide.jpg', 3, 6),
    (81, N'Clotrimazole 1%', N'Clotrimazole 1%', 15000, 1, N'tuýp', N'Kem trị nấm da, nấm bẹn, lang ben.', N'/images/Clotrimazone.webp', 3, 7),
    (82, N'Kem dưỡng Moogoo', N'Sữa, vitamin E, dầu hạnh nhân', 280000, 2, N'tuýp', N'Kem dưỡng ẩm dịu nhẹ, thành phần an toàn cho da nhạy cảm.', N'/images/CreamMoogoo.webp', 3, 4),
    (83, N'Kem chống nắng SPF50', N'Zinc oxide, Titanium dioxide', 320000, 1, N'tuýp', N'Kem chống nắng phổ rộng SPF50, bảo vệ da khỏi tia UVA/UVB.', N'/images/DailyPrevention50.jpg', 3, 1),
    (84, N'Sữa tắm Dove dịu nhẹ', N'Chất làm sạch dịu nhẹ, 1/4 kem dưỡng ẩm', 130000, 2, N'chai', N'Sữa tắm làm sạch và dưỡng ẩm, cho da mềm mịn.', N'/images/DoveDiunhe.jpg', 3, 6),
    (85, N'Dual Urea 5%', N'Urea 5%', 220000, 1, N'chai', N'Kem dưỡng ẩm chuyên sâu cho da khô, da cơ địa.', N'/images/DualUrea5.webp', 3, 3),
    (86, N'Footgel', N'Urea, menthol', 45000, 1, N'tuýp', N'Gel chăm sóc chân, làm mềm da, giảm mùi và mệt mỏi.', N'/images/Footgel.jpg', 3, 3),
    (87, N'Nước tẩy trang Garnier', N'Micellar water', 100000, 2, N'chai', N'Nước tẩy trang làm sạch bụi bẩn, lớp trang điểm, dịu nhẹ cho da.', N'/images/GarnierSkin.webp', 3, 6),
    (88, N'Genatreson cream', N'Betamethasone, Gentamicin, Clotrimazole', 25000, 1, N'tuýp', N'Kem trị viêm da nhiễm khuẩn, nấm và dị ứng.', N'/images/Genatreson.jpg', 3, 4),
    (89, N'Sữa rửa mặt Hada Labo', N'Hyaluronic acid', 130000, 2, N'tuýp', N'Sữa rửa mặt làm sạch nhẹ nhàng và cấp ẩm cho da.', N'/images/HadaLabo.webp', 3, 5),
    (90, N'Sữa rửa mặt Hatomugi', N'Chiết xuất ý dĩ', 120000, 2, N'tuýp', N'Sữa rửa mặt dưỡng ẩm, làm sáng da, mềm mịn.', N'/images/hatomugi.webp', 3, 8),
    (91, N'Serum Hyaluron', N'Hyaluronic acid', 350000, 2, N'chai', N'Serum cấp ẩm, giúp da căng mịn, giảm nếp nhăn nhỏ.', N'/images/Hyaluron.jpg', 3, 7),
    (92, N'Hyaluronic Acid serum', N'Hyaluronic acid', 300000, 1, N'chai', N'Tinh chất cấp nước chuyên sâu, phục hồi da khô.', N'/images/HyaluronicAcid.jpg', 3, 7),
    (93, N'Hydro Trefi', N'Hydrocortisone', 35000, 2, N'tuýp', N'Kem bôi corticoid nhẹ giảm viêm, ngứa trong viêm da.', N'/images/HydroTrefi.jpg', 3, 5),
    (94, N'Gel Jeju Aloe', N'Chiết xuất lô hội', 60000, 1, N'tuýp', N'Gel lô hội làm dịu, cấp ẩm cho da bị nắng, kích ứng.', N'/images/JeJuAloe.webp', 3, 6),
    (95, N'Kem dưỡng da ban đêm', N'Peptide, vitamin E', 250000, 1, N'hũ', N'Kem dưỡng phục hồi da ban đêm, giữ ẩm và làm mềm da.', N'/images/Kemduongdabandem.jpg', 3, 6),
    (96, N'Khăn mặt cotton', N'Cotton', 35000, 2, N'cái', N'Khăn mặt mềm, thấm hút tốt, dùng cho da nhạy cảm.', N'/images/KhanmatCotton.webp', 3, 2),
    (97, N'Son dưỡng môi', N'Sáp ong, dầu thực vật', 50000, 2, N'thỏi', N'Son dưỡng ngăn nứt nẻ, khô môi.', N'/images/LipBalm.jpg', 3, 2),
    (98, N'Mentholatum Lip', N'Menthol, camphor', 35000, 2, N'thỏi', N'Son dưỡng môi mát, giảm nứt nẻ và khô môi.', N'/images/Mentholatum.webp', 3, 3),
    (99, N'Miconazole 2%', N'Miconazole nitrate 2%', 15000, 1, N'tuýp', N'Kem điều trị nấm da, lang ben, nấm móng.', N'/images/Miconazole.webp', 3, 4),
    (100, N'Panthenol', N'Dexpanthenol 5%', 40000, 2, N'tuýp', N'Kem làm lành da, giảm kích ứng, nứt nẻ, hăm tã.', N'/images/Panthenol.webp', 3, 4),
    (101, N'Soforthilfe', N'Chiết xuất thảo dược', 90000, 1, N'tuýp', N'Kem làm dịu nhanh các kích ứng da, côn trùng cắn.', N'/images/Soforthilfe.jpg', 3, 7),
    (102, N'Soothing Gel Pharmacity', N'Aloe vera, panthenol', 70000, 1, N'tuýp', N'Gel làm dịu da, giảm đỏ và kích ứng.', N'/images/Soothinggel.jpg', 3, 6),
    (103, N'Cetaphil Sun SPF50', N'Chất chống nắng phổ rộng', 300000, 1, N'tuýp', N'Kem chống nắng nhẹ, không gây bết dính, bảo vệ da khỏi UVA/UVB.', N'/images/Sun50.webp', 3, 8),
    (104, N'The Max Creme', N'Peptide, chất chống oxy hóa', 900000, 2, N'hũ', N'Kem dưỡng chống lão hóa, hỗ trợ trẻ hóa da.', N'/images/TheMax.webp', 3, 3),
    (105, N'Urae 10%', N'Urea 10%', 180000, 1, N'chai', N'Lotion dưỡng ẩm cho da rất khô, sần sùi.', N'/images/Urae10.webp', 3, 8),
    (106, N'Vaseline', N'Petroleum jelly', 60000, 1, N'hũ', N'Sáp dưỡng ẩm khóa nước, bảo vệ da nứt nẻ, môi khô.', N'/images/Vaseline.jpg', 3, 2),
    (107, N'Vital C', N'Vitamin C', 650000, 2, N'tuýp', N'Sản phẩm dưỡng da vitamin C hỗ trợ làm sáng và đều màu da.', N'/images/VitalC.jpg', 3, 7),
    (108, N'L''''Oréal Vitamino Color', N'Chất bảo vệ màu, vitamin', 350000, 2, N'chai', N'Dầu gội dưỡng và giữ màu tóc nhuộm lâu phai.', N'/images/VitaminoColorSpectrum.jpg', 3, 1),
    (109, N'Artischocken Kapseln', N'Chiết xuất atiso', 250000, 2, N'hộp', N'Viên atiso hỗ trợ chức năng gan và tiêu hóa.', N'/images/atisobogan.webp', 4, 1),
    (110, N'Blackmores Ginkgo Forte', N'Ginkgo biloba extract', 450000, 2, N'lọ', N'Hỗ trợ tuần hoàn não, cải thiện trí nhớ và tập trung.', N'/images/BlackmoresGink.webp', 4, 3),
    (111, N'Blackmores Milk Thistle', N'Silymarin (cây kế sữa)', 420000, 1, N'lọ', N'Hỗ trợ bảo vệ và thải độc gan.', N'/images/BlackmoresGinseng.jpg', 4, 6),
    (112, N'Boganic', N'Cao actiso, rau đắng đất, bìm bìm', 90000, 2, N'hộp', N'Thực phẩm bảo vệ sức khỏe giúp mát gan, giải độc, hỗ trợ chức năng gan.', N'/images/Boganic.jpg', 4, 6),
    (113, N'Codeage Marine Collagen Peptides', N'Collagen peptide cá biển', 900000, 1, N'hũ', N'Bột collagen biển hỗ trợ da, tóc, móng và khớp.', N'/images/CodeageMarineCollagenPeptides.webp', 4, 7),
    (114, N'DHC Collagen', N'Collagen peptide, vitamin C', 700000, 2, N'hộp', N'Collagen nước giúp da đàn hồi, mịn màng.', N'/images/DHCCollagen.webp', 4, 7),
    (115, N'Đông trùng hạ thảo', N'Cordyceps', 1500000, 1, N'hộp', N'Tăng cường sức khỏe, bồi bổ cơ thể, nâng cao đề kháng.', N'/images/Dongtrunghathao.webp', 4, 3),
    (116, N'Nature''''s Bounty Hair Skin Nails', N'Biotin, vitamin C, E', 350000, 1, N'lọ', N'Hỗ trợ tóc, da, móng chắc khỏe.', N'/images/hairskinnailssupplement.webp', 4, 7),
    (117, N'Healthy Care CoQ10 150mg', N'Coenzyme Q10 150mg', 450000, 1, N'lọ', N'Hỗ trợ tim mạch và sản sinh năng lượng tế bào.', N'/images/HealthyCareCoenzymeQ10.webp', 4, 2),
    (118, N'Healthy Care Cranberry 25000mg', N'Chiết xuất nam việt quất', 400000, 1, N'lọ', N'Hỗ trợ sức khỏe đường tiết niệu.', N'/images/HealthyCareCranberry.webp', 4, 4),
    (119, N'Healthy Care Fish Oil 1000mg', N'Omega 3 (EPA, DHA)', 350000, 1, N'lọ', N'Dầu cá hỗ trợ tim mạch, não bộ và thị lực.', N'/images/HealthyCareFishOil1000mg.webp', 4, 8),
    (120, N'Healthy Care Ginkgo Biloba 2000', N'Ginkgo biloba 2000mg', 380000, 1, N'lọ', N'Hỗ trợ tuần hoàn máu não, cải thiện trí nhớ.', N'/images/HealthyCareGinkgoBiloba.jpg', 4, 6),
    (121, N'Healthy Care Liver Detox', N'Cây kế sữa, atiso', 400000, 1, N'lọ', N'Hỗ trợ thanh lọc và bảo vệ gan.', N'/images/HealthyCareLiverDetox.jpg', 4, 4),
    (122, N'Healthy Care Shark Cartilage 750mg', N'Sụn vi cá mập 750mg', 450000, 2, N'lọ', N'Hỗ trợ sức khỏe xương khớp.', N'/images/HealthyCareSharkCartilage.jpg', 4, 5),
    (123, N'Hepa-Merz', N'L-ornithine L-aspartate', 350000, 2, N'hộp', N'Hỗ trợ điều trị bệnh gan, giảm amoniac máu.', N'/images/HepaMerz.jpg', 4, 6),
    (124, N'Hồng sâm Hàn Quốc', N'Saponin ginsenoside', 600000, 2, N'hộp', N'Tăng cường thể lực, nâng cao sức đề kháng.', N'/images/hongsamKorean.webp', 4, 6),
    (125, N'Sữa Ensure lốc', N'Protein, vitamin, khoáng chất', 150000, 2, N'lốc', N'Sữa dinh dưỡng bổ sung năng lượng cho người lớn.', N'/images/HopsuaEnsure.jpg', 4, 8),
    (126, N'Jointlab', N'Glucosamine, chondroitin', 300000, 1, N'hộp', N'Hỗ trợ sụn khớp, giảm đau khớp.', N'/images/Jointlab.jpg', 4, 1),
    (127, N'Living Healthy Glucosamine Sulphate', N'Glucosamine sulphate', 500000, 2, N'hộp', N'Hỗ trợ tái tạo sụn khớp, cải thiện vận động.', N'/images/LivingHealthyGlucosamineSulphate.jpg', 4, 5),
    (128, N'Max Biocare Femme-F', N'Vitamin, khoáng chất, chiết xuất thảo mộc', 480000, 1, N'lọ', N'Hỗ trợ sức khỏe và cân bằng nội tiết cho nữ giới.', N'/images/MaxBiocareFemme-F.png', 4, 7),
    (129, N'Max Biocare Homme-F', N'Vitamin, khoáng chất, chiết xuất thảo mộc', 480000, 2, N'lọ', N'Hỗ trợ sức khỏe và sinh lực cho nam giới.', N'/images/MaxBiocareHomme-F.png', 4, 4),
    (130, N'Nature''''s Bounty Melatonin', N'Melatonin 3mg', 250000, 1, N'lọ', N'Hỗ trợ giấc ngủ, điều hòa nhịp sinh học.', N'/images/melatonin.webp', 4, 6),
    (131, N'Nucos Collagen 100%', N'Collagen thủy phân', 500000, 2, N'lọ', N'Collagen hỗ trợ làm đẹp da, tóc, móng.', N'/images/NucosCollagen.png', 4, 2),
    (132, N'Nutren Optimum', N'Protein, vitamin, khoáng chất', 600000, 2, N'lon', N'Sữa dinh dưỡng y học cho người cần bồi bổ.', N'/images/Nutren.webp', 4, 4),
    (133, N'Sữa ong chúa Royal Jelly 1000mg', N'Royal jelly', 450000, 1, N'lọ', N'Bồi bổ sức khỏe, hỗ trợ tăng cường đề kháng và làm đẹp da.', N'/images/RoyalJelly.jpg', 4, 5),
    (134, N'Dầu cá hồi Omega-3', N'Omega-3 (EPA, DHA)', 550000, 2, N'lọ', N'Hỗ trợ tim mạch, não và thị lực.', N'/images/salmonoilOmega3supplement.webp', 4, 2),
    (135, N'Silymarin 70mg', N'Silymarin 70mg', 120000, 2, N'hộp', N'Hỗ trợ bảo vệ tế bào gan, giải độc gan.', N'/images/Silymarin.jpg', 4, 1),
    (136, N'Sữa bột Ensure Gold', N'Protein, HMB, vitamin', 850000, 1, N'lon', N'Sữa bột dinh dưỡng giúp bổ sung năng lượng, duy trì cơ bắp.', N'/images/SuabotEnsure.jpg', 4, 1),
    (137, N'Valerian Sleep', N'Chiết xuất Valerian', 350000, 1, N'lọ', N'Hỗ trợ ngủ ngon, giảm căng thẳng.', N'/images/valeriansleep.jpg', 4, 2),
    (138, N'Webber Naturals Glucosamine', N'Glucosamine, chondroitin, MSM', 500000, 2, N'lọ', N'Hỗ trợ sức khỏe xương khớp.', N'/images/WebberNaturalsGlucosamine.jpg', 4, 4),
    (139, N'Yến sào', N'Tổ yến chưng sẵn', 1200000, 1, N'hộp', N'Bồi bổ sức khỏe, phục hồi thể trạng.', N'/images/Yensao.jpg', 4, 2),
    (140, N'AEvit', N'Vitamin A, Vitamin E', 30000, 2, N'hộp', N'Bổ sung vitamin A và E, hỗ trợ thị lực và làn da.', N'/images/AEvit.jpg', 5, 1),
    (141, N'Aquadetrim D3', N'Cholecalciferol (Vitamin D3)', 80000, 1, N'lọ', N'Dạng giọt bổ sung vitamin D3 phòng còi xương.', N'/images/AquadetrimD3.jpg', 5, 5),
    (142, N'Băng y tế cá nhân', N'Keo dán y tế', 30000, 1, N'hộp', N'Băng dán cá nhân bảo vệ vết thương nhỏ.', N'/images/Bangytecanhan.jpg', 5, 1),
    (143, N'Berocca', N'Vitamin B, C, khoáng chất', 150000, 2, N'tuýp', N'Viên sủi bổ sung vitamin nhóm B, C, tăng năng lượng.', N'/images/Berocca.jpg', 5, 1),
    (144, N'Blackmores Bio Iron', N'Sắt, vitamin C', 300000, 1, N'lọ', N'Bổ sung sắt phòng thiếu máu.', N'/images/BlackmoresBioiron.jpg', 5, 4),
    (145, N'Blackmores Bio Magnesium', N'Magie', 320000, 2, N'lọ', N'Hỗ trợ hoạt động cơ, thần kinh, giảm chuột rút.', N'/images/BlackmoresBioMagnes.jpg', 5, 1),
    (146, N'Blackmores Bio Zinc', N'Kẽm', 280000, 1, N'lọ', N'Hỗ trợ miễn dịch, sức khỏe làn da.', N'/images/BlackmoresBioZinc.jpg', 5, 7),
    (147, N'Calcium Carbonate', N'Calcium carbonate', 350000, 2, N'lọ', N'Bổ sung canxi cho xương và răng chắc khỏe.', N'/images/CalciumCarbon.jpg', 5, 2),
    (148, N'Calcium Citrate', N'Calcium citrate', 380000, 2, N'lọ', N'Canxi dễ hấp thu hỗ trợ xương chắc khỏe.', N'/images/CalciumCitrate.jpg', 5, 2),
    (149, N'Caltrate 600+D3', N'Calcium 600mg, Vitamin D3', 250000, 2, N'lọ', N'Bổ sung canxi và vitamin D3 phòng loãng xương.', N'/images/Caltrate600D3.jpg', 5, 8),
    (150, N'Cebion', N'Vitamin C 1000mg', 70000, 1, N'tuýp', N'Viên sủi vitamin C tăng sức đề kháng.', N'/images/Cebion.jpg', 5, 4),
    (151, N'Centrum Advance', N'Vitamin tổng hợp, khoáng chất', 350000, 1, N'lọ', N'Bổ sung vitamin và khoáng chất cho người trưởng thành.', N'/images/Centrum.webp', 5, 2),
    (152, N'Centrum Men', N'Vitamin, khoáng chất cho nam', 380000, 1, N'lọ', N'Vitamin tổng hợp dành riêng cho nam giới.', N'/images/Centrummen.jpg', 5, 5),
    (153, N'Centrum Women', N'Vitamin, khoáng chất cho nữ', 380000, 1, N'lọ', N'Vitamin tổng hợp dành riêng cho nữ giới.', N'/images/Centrumwomen.jpg', 5, 8),
    (154, N'Elevit', N'Vitamin, acid folic, sắt', 580000, 1, N'hộp', N'Vitamin tổng hợp cho phụ nữ chuẩn bị mang thai và đang mang thai.', N'/images/Elevit.jpg', 5, 7),
    (155, N'Ferrous Fumarate', N'Sắt fumarat 210mg', 45000, 2, N'hộp', N'Bổ sung sắt điều trị thiếu máu thiếu sắt.', N'/images/Ferrousfumarate.jpg', 5, 5),
    (156, N'Ferrovit', N'Sắt, acid folic, vitamin B12', 90000, 1, N'hộp', N'Bổ sung sắt, phòng và điều trị thiếu máu.', N'/images/Ferrrovit.jpg', 5, 5),
    (157, N'Folic acid', N'Acid folic 5mg', 20000, 1, N'lọ', N'Bổ sung acid folic, phòng dị tật ống thần kinh thai nhi.', N'/images/Folic acid.jpg', 5, 6),
    (158, N'Magnesium B6', N'Magie, vitamin B6', 40000, 1, N'hộp', N'Bổ sung magie, giảm căng thẳng, chuột rút.', N'/images/MagnesiumB6.jpg', 5, 5),
    (159, N'Nature Made Fish Oil', N'Omega-3', 350000, 2, N'lọ', N'Dầu cá hỗ trợ tim mạch.', N'/images/NatureFishoil.jpg', 5, 2),
    (160, N'Neurobion', N'Vitamin B1, B6, B12', 60000, 1, N'hộp', N'Điều trị viêm dây thần kinh, tê bì chân tay.', N'/images/Neurobion.jpg', 5, 2),
    (161, N'One A Day Multi', N'Vitamin tổng hợp', 320000, 1, N'lọ', N'Vitamin tổng hợp hằng ngày.', N'/images/OneadayMulti.jpg', 5, 8),
    (162, N'Ostelin Calcium & Vitamin D3', N'Canxi, vitamin D3', 290000, 1, N'lọ', N'Hỗ trợ xương chắc khỏe.', N'/images/OstelinVitaminD.jpg', 5, 2),
    (163, N'Pharmaton', N'Vitamin, khoáng chất, nhân sâm', 250000, 2, N'hộp', N'Bồi bổ cơ thể, tăng cường thể lực.', N'/images/Pharmaton.jpg', 5, 8),
    (164, N'Redoxon', N'Vitamin C, kẽm', 120000, 1, N'tuýp', N'Viên sủi vitamin C và kẽm tăng đề kháng.', N'/images/Redoxon.jpg', 5, 1),
    (165, N'Solgar Vitamin B12 1000mcg', N'Vitamin B12', 450000, 1, N'lọ', N'Hỗ trợ hệ thần kinh và tạo hồng cầu.', N'/images/SolgarVitaminB12.jpg', 5, 4),
    (166, N'Solgar VM-75', N'Vitamin, khoáng chất', 700000, 2, N'lọ', N'Vitamin tổng hợp cao cấp bổ sung toàn diện.', N'/images/SolgarVM75.jpg', 5, 7),
    (167, N'Supradyn', N'Vitamin tổng hợp, khoáng chất', 150000, 2, N'tuýp', N'Viên sủi bổ sung vitamin, tăng năng lượng.', N'/images/Supradyn.jpg', 5, 6),
    (168, N'Vitamin A', N'Retinol 5000IU', 20000, 1, N'hộp', N'Bổ sung vitamin A cho mắt và da.', N'/images/VitaminA.jpg', 5, 3),
    (169, N'Vitamin B1', N'Thiamine 100mg', 15000, 2, N'lọ', N'Hỗ trợ chuyển hóa năng lượng, thần kinh.', N'/images/VitaminB1.jpg', 5, 1),
    (170, N'Vitamin B2', N'Riboflavin', 15000, 1, N'lọ', N'Hỗ trợ chuyển hóa, bảo vệ da và niêm mạc.', N'/images/VitaminB2.jpg', 5, 6),
    (171, N'Vitamin B6', N'Pyridoxine', 15000, 1, N'lọ', N'Hỗ trợ chuyển hóa protein, hệ thần kinh.', N'/images/VitaminB6.jpg', 5, 6),
    (172, N'Vitamin B12', N'Cyanocobalamin', 280000, 2, N'lọ', N'Hỗ trợ sản sinh hồng cầu, thần kinh.', N'/images/VitaminB12.jpg', 5, 5),
    (173, N'Vitamin B Complex', N'Vitamin nhóm B', 250000, 2, N'lọ', N'Bổ sung các vitamin nhóm B.', N'/images/VitaminBcomplex.jpg', 5, 4),
    (174, N'Vitamin D3', N'Cholecalciferol 50mcg', 220000, 2, N'lọ', N'Giúp hấp thu canxi, xương chắc khỏe.', N'/images/VitaminD3.jpg', 5, 5),
    (175, N'Vitamin E', N'Tocopherol 400IU', 180000, 2, N'lọ', N'Chống oxy hóa, đẹp da.', N'/images/VitaminE.jpg', 5, 5),
    (176, N'Vitamin K1', N'Phytomenadione', 50000, 1, N'hộp', N'Hỗ trợ đông máu.', N'/images/VitaminK1.jpg', 5, 4),
    (177, N'Vitamin K2', N'Menaquinone MK-7', 350000, 2, N'lọ', N'Hỗ trợ canxi vào xương, tim mạch khỏe.', N'/images/VitaminK2.jpg', 5, 2),
    (178, N'Vitrum', N'Vitamin, khoáng chất', 290000, 2, N'lọ', N'Bổ sung vitamin tổng hợp hằng ngày.', N'/images/Vitrumvitamin.jpg', 5, 7),
    (179, N'Zinc Gluconate', N'Kẽm gluconat', 60000, 1, N'hộp', N'Bổ sung kẽm, tăng đề kháng.', N'/images/Zincgluconate.jpg', 5, 8),
    (180, N'Kẽm sulfat', N'Kẽm sulfat', 30000, 2, N'lọ', N'Siro bổ sung kẽm cho trẻ biếng ăn, tiêu chảy.', N'/images/ZincSuffate.jpg', 5, 5),
    (181, N'Refix 550', N'Rifaximin 550mg', 450000, 2, N'hộp', N'Kháng sinh đường ruột, điều trị hội chứng ruột kích thích, tiêu chảy do nhiễm khuẩn.', N'/images/Rifaximin.jpg', 6, 4),
    (182, N'Sulfasalazine', N'Sulfasalazine 500mg', 120000, 2, N'lọ', N'Điều trị viêm loét đại tràng và bệnh Crohn.', N'/images/Sulfasalazine.jpg', 6, 6),
    (183, N'Mesalazine', N'Mesalazine 500mg', 180000, 2, N'hộp', N'Điều trị viêm loét đại tràng.', N'/images/Mesalazine.jpg', 6, 7),
    (184, N'Festal', N'Pancreatin, hemicellulase, bile', 60000, 2, N'hộp', N'Hỗ trợ tiêu hóa, điều trị khó tiêu, đầy bụng.', N'/images/Festal.jpg', 6, 7),
    (185, N'Pancreatin', N'Pancreatin', 50000, 1, N'hộp', N'Men tụy hỗ trợ tiêu hóa.', N'/images/Pancreatin.jpg', 6, 5),
    (186, N'Ondansetron', N'Ondansetron 4mg', 40000, 2, N'hộp', N'Phòng và điều trị buồn nôn, nôn.', N'/images/Ondansetron.jpg', 6, 4),
    (187, N'Metoclopramide', N'Metoclopramide 10mg', 20000, 2, N'hộp', N'Điều trị buồn nôn, nôn, rối loạn vận động dạ dày.', N'/images/Metoclopramide.png', 6, 5),
    (188, N'Motilium-M', N'Domperidone 10mg', 55000, 1, N'hộp', N'Điều trị buồn nôn, đầy bụng, chậm tiêu.', N'/images/Motilium.webp', 6, 1),
    (189, N'Sorbitol Sanofi', N'Sorbitol 5g', 30000, 2, N'hộp', N'Thuốc bột điều trị táo bón.', N'/images/SorbitolSanofi.jpg', 6, 8),
    (190, N'Senna', N'Sennosides', 40000, 2, N'hộp', N'Trà thảo dược nhuận tràng.', N'/images/Senna.webp', 6, 8),
    (191, N'Dulcolax', N'Bisacodyl 5mg', 50000, 2, N'hộp', N'Điều trị táo bón.', N'/images/Bisacodyl.png', 6, 2),
    (192, N'Bisacodyl DHG', N'Bisacodyl 5mg', 20000, 1, N'hộp', N'Điều trị táo bón ngắn hạn.', N'/images/Bisacodyl.png', 6, 8),
    (193, N'Macrogol', N'Macrogol 4000', 90000, 2, N'hộp', N'Điều trị táo bón, chuẩn bị nội soi.', N'/images/Macrogol.jpg', 6, 2),
    (194, N'Forlax', N'Macrogol 4000 10g', 110000, 2, N'hộp', N'Điều trị táo bón người lớn.', N'/images/Forlax.png', 6, 8),
    (195, N'Lactulose', N'Lactulose', 60000, 1, N'hộp', N'Điều trị táo bón, hỗ trợ hệ vi khuẩn đường ruột.', N'/images/Lactulose.jpg', 6, 1),
    (196, N'Duphalac', N'Lactulose 10g/15ml', 110000, 2, N'hộp', N'Điều trị táo bón, hỗ trợ bệnh gan.', N'/images/Duphalac.jpg', 6, 6),
    (197, N'Saccharomyces boulardii', N'Saccharomyces boulardii', 120000, 2, N'hộp', N'Men vi sinh phòng và điều trị tiêu chảy.', N'/images/Saccharomycesboulardii.jpg', 6, 5),
    (198, N'Lacteol Fort', N'Lactobacillus acidophilus', 140000, 2, N'hộp', N'Điều trị tiêu chảy cấp và mạn tính.', N'/images/LacteolFort.webp', 6, 3),
    (199, N'BioGaia Drops', N'Lactobacillus reuteri', 480000, 2, N'lọ', N'Men vi sinh dạng giọt cho trẻ nhỏ.', N'/images/BioGaiamenvisinh.webp', 6, 6),
    (200, N'Racecadotril', N'Racecadotril 30mg', 60000, 2, N'hộp', N'Điều trị tiêu chảy cấp ở trẻ em.', N'/images/Racecadotril.jpg', 6, 2),
    (201, N'Hidrasec', N'Racecadotril 100mg', 110000, 1, N'hộp', N'Điều trị tiêu chảy cấp.', N'/images/Hidrasec.jpg', 6, 1),
    (202, N'Imodium', N'Loperamide 2mg', 45000, 2, N'hộp', N'Điều trị tiêu chảy cấp không do nhiễm khuẩn.', N'/images/Imodium.webp', 6, 4),
    (203, N'Smecta', N'Diosmectite 3g', 70000, 1, N'hộp', N'Điều trị tiêu chảy, đau dạ dày.', N'/images/Smecta.webp', 6, 1),
    (204, N'Pepto-Bismol', N'Bismuth subsalicylate', 110000, 1, N'hộp', N'Giảm khó chịu dạ dày, ợ nóng, tiêu chảy.', N'/images/subsalicylate.png', 6, 6),
    (205, N'Sucralfate', N'Sucralfate 1g', 75000, 1, N'hộp', N'Bảo vệ niêm mạc, điều trị loét dạ dày, tá tràng.', N'/images/Sucralfate.webp', 6, 7),
    (206, N'Cimetidine', N'Cimetidine 200mg', 25000, 1, N'hộp', N'Giảm tiết acid dạ dày.', N'/images/Cimetidine.png', 6, 6),
    (207, N'Famotidin 40', N'Famotidine 40mg', 35000, 2, N'hộp', N'Điều trị loét dạ dày, trào ngược.', N'/images/Famotidin.webp', 6, 4),
    (208, N'Pantoprazole STADA', N'Pantoprazole 40mg', 90000, 2, N'hộp', N'Điều trị trào ngược dạ dày thực quản, loét dạ dày.', N'/images/Esomeprazole.webp', 6, 8),
    (209, N'Esomeprazole STADA 20mg', N'Esomeprazole 20mg', 120000, 1, N'hộp', N'Điều trị trào ngược dạ dày thực quản.', N'/images/Esomeprazole.webp', 6, 8),
    (210, N'Omeprazol DHG', N'Omeprazole 20mg', 40000, 2, N'hộp', N'Điều trị loét dạ dày, trào ngược.', N'/images/Omeprazole.webp', 6, 8),
    (211, N'Mylanta', N'Nhôm hydroxyd, magie hydroxyd, simethicone', 50000, 1, N'hộp', N'Trung hòa acid dạ dày, giảm đầy hơi.', N'/images/Mylanta.jpg', 6, 2),
    (212, N'Simethicone', N'Simethicone 80mg', 45000, 1, N'hộp', N'Giảm đầy hơi, chướng bụng.', N'/images/Simethicone.webp', 6, 4),
    (213, N'Maalox', N'Nhôm hydroxyd, magie hydroxyd', 55000, 2, N'hộp', N'Trung hòa acid dạ dày, giảm ợ nóng.', N'/images/Maalox.jpg', 6, 6),
    (214, N'Gaviscon', N'Natri alginate, natri bicarbonate', 90000, 1, N'hộp', N'Điều trị ợ nóng, trào ngược.', N'/images/Gaviscon.webp', 6, 3),
    (215, N'Phosphalugel', N'Nhôm phosphat', 45000, 1, N'hộp', N'Giảm đau dạ dày, ợ chua.', N'/images/Phosphalugel.jpg', 6, 6),
    (216, N'Debridat', N'Trimebutine maleate', 85000, 1, N'hộp', N'Điều trị rối loạn chức năng tiêu hóa.', N'/images/Debridat.jpg', 6, 3),
    (217, N'Duspatalin Retard', N'Mebeverine HCl 200mg', 140000, 1, N'hộp', N'Điều trị hội chứng ruột kích thích.', N'/images/Duspatalin.webp', 6, 3),
    (218, N'Spasfon', N'Phloroglucinol, trimethylphloroglucinol', 80000, 2, N'hộp', N'Giảm đau co thắt đường tiêu hóa.', N'/images/Spasfon.jpg', 6, 3),
    (219, N'Meteospasmyl', N'Alverine citrate, simethicone', 85000, 2, N'hộp', N'Điều trị rối loạn tiêu hóa, đầy hơi.', N'/images/Meteospasmyl.webp', 6, 3),
    (220, N'Buscopan', N'Hyoscine butylbromide', 75000, 1, N'hộp', N'Giảm co thắt cơ trơn tiêu hóa.', N'/images/Buscopan.webp', 6, 3),
    (221, N'Enterogermina', N'Bacillus clausii', 120000, 2, N'hộp', N'Men vi sinh phòng và điều trị rối loạn đường ruột.', N'/images/Enterogermina.jpg', 6, 1),
    (222, N'Rebamipide 100mg', N'Rebamipide 100mg', 120000, 1, N'hộp', N'Bảo vệ niêm mạc dạ dày.', N'/images/rebamipide100mg.webp', 6, 1),
    (223, N'Rabeprazol', N'Rabeprazole sodium 20mg', 70000, 2, N'hộp', N'Điều trị loét dạ dày, trào ngược.', N'/images/Rabeprazol.png', 6, 6),
    (224, N'Oresol', N'Glucose, natri, kali', 10000, 2, N'hộp', N'Bù nước và điện giải khi tiêu chảy.', N'/images/Oresol.jpg', 6, 3),
    (225, N'Glucosamine Orihiro', N'Glucosamine', 800000, 1, N'lọ', N'Hỗ trợ xương khớp, nhập khẩu từ Nhật Bản.', N'/images/GlucosamineOrihiro.webp', 6, 1),
    (226, N'Arcoxia 90mg', N'Etoricoxib 90mg', 180000, 1, N'hộp', N'Giảm đau, chống viêm xương khớp.', N'/images/Arcoxia90mg.webp', 7, 1),
    (227, N'Bisolvon', N'Bromhexine HCl', 40000, 2, N'hộp', N'Tiêu đờm, long đờm trong bệnh đường hô hấp.', N'/images/Bisolvon.png', 7, 5),
    (228, N'Brufen 400mg', N'Ibuprofen 400mg', 75000, 1, N'hộp', N'Giảm đau, hạ sốt, kháng viêm.', N'/images/Brufen400mg.jpg', 7, 2),
    (229, N'Celebrex 200mg', N'Celecoxib 200mg', 220000, 1, N'hộp', N'Giảm đau viêm khớp, thoái hóa khớp.', N'/images/Celebrex200mg.jpg', 7, 6),
    (230, N'Celecoxib 100mg', N'Celecoxib 100mg', 90000, 2, N'hộp', N'Giảm đau, chống viêm.', N'/images/Celecoxib100mg.webp', 7, 2),
    (231, N'Celecoxib 200mg', N'Celecoxib 200mg', 120000, 1, N'hộp', N'Giảm đau, chống viêm xương khớp.', N'/images/Celecoxib200mg.jpg', 7, 4),
    (232, N'Counterpain', N'Methyl salicylate, menthol', 55000, 2, N'tuýp', N'Kem xoa bóp giảm đau cơ, đau khớp.', N'/images/Counterpain.jpg', 7, 8),
    (233, N'Deep Heat Rub', N'Methyl salicylate, menthol', 70000, 2, N'tuýp', N'Kem xoa giảm đau cơ, bong gân.', N'/images/DeepHeatRub.webp', 7, 3),
    (234, N'Diclofenac 50mg', N'Diclofenac sodium 50mg', 20000, 2, N'hộp', N'Giảm đau, kháng viêm xương khớp.', N'/images/Diclofenac50mg.webp', 7, 7),
    (235, N'Diclofenac gel 1%', N'Diclofenac 1%', 40000, 1, N'tuýp', N'Gel bôi giảm đau, viêm khớp, bong gân.', N'/images/Diclofenacgel1_.jpg', 7, 8),
    (236, N'Efferalgan 500mg', N'Paracetamol 500mg', 50000, 2, N'hộp', N'Viên sủi giảm đau, hạ sốt.', N'/images/Efferalgan.jpg', 7, 7),
    (237, N'Efferalgan Vitamin C', N'Paracetamol, vitamin C', 65000, 2, N'hộp', N'Viên sủi giảm đau, hạ sốt kèm vitamin C.', N'/images/EfferalganVitaminC.jpg', 7, 3),
    (238, N'Etoricoxib 60mg', N'Etoricoxib 60mg', 120000, 1, N'hộp', N'Giảm đau xương khớp.', N'/images/Etoricoxib60mg.webp', 7, 5),
    (239, N'Hapacol sủi', N'Paracetamol 500mg', 40000, 2, N'hộp', N'Viên sủi giảm đau, hạ sốt.', N'/images/Hapacol_Sui.jpg', 7, 8),
    (240, N'Hapacol Caplet 500', N'Paracetamol 500mg', 20000, 2, N'hộp', N'Giảm đau, hạ sốt.', N'/images/HapacolCaplet_Vien.jpg', 7, 6),
    (241, N'Hapacol Extra', N'Paracetamol 500mg, caffeine', 25000, 2, N'hộp', N'Giảm đau mạnh hơn, hạ sốt.', N'/images/HapacolExtra.jpg', 7, 6),
    (242, N'Ibuprofen 200mg', N'Ibuprofen 200mg', 20000, 2, N'hộp', N'Giảm đau, hạ sốt, kháng viêm.', N'/images/Ibuprofen200mg.jpg', 7, 8),
    (243, N'Siro ho lá thường xuân', N'Chiết xuất lá thường xuân', 90000, 2, N'chai', N'Siro giảm ho, long đờm.', N'/images/ivyleafcoughsyrup.jpg', 7, 7),
    (244, N'Fastum Gel', N'Ketoprofen 2,5%', 90000, 2, N'tuýp', N'Gel giảm đau cơ xương khớp.', N'/images/Ketoprofengel.jpg', 7, 5),
    (245, N'Lidocaine gel 2%', N'Lidocaine 2%', 50000, 2, N'tuýp', N'Gel gây tê tại chỗ.', N'/images/Lidocainegel.jpg', 7, 2),
    (246, N'Meloxicam 7,5mg', N'Meloxicam 7,5mg', 15000, 2, N'hộp', N'Giảm đau, kháng viêm xương khớp.', N'/images/Meloxicam7.5mg.jpg', 7, 2),
    (247, N'Meloxicam 15mg', N'Meloxicam 15mg', 25000, 1, N'hộp', N'Giảm đau, kháng viêm viêm khớp.', N'/images/Meloxicam15mg.webp', 7, 8),
    (248, N'Mofen 400mg', N'Ibuprofen 400mg', 30000, 1, N'hộp', N'Giảm đau, hạ sốt.', N'/images/Mofen400mg.jpg', 7, 4),
    (249, N'Naproxen 250mg', N'Naproxen 250mg', 30000, 2, N'hộp', N'Giảm đau, chống viêm.', N'/images/Naproxen250mg.jpg', 7, 2),
    (250, N'Naproxen 500mg', N'Naproxen 500mg', 45000, 2, N'hộp', N'Giảm đau, chống viêm.', N'/images/Naproxen500mg.jpg', 7, 2),
    (251, N'NeilMed Sinus Rinse', N'Natri clorid, natri bicarbonate', 250000, 1, N'bộ', N'Bộ rửa mũi xoang, làm sạch dịch nhầy.', N'/images/NeilMedSinusRinse.jpg', 7, 1),
    (252, N'Nurofen siro', N'Ibuprofen', 90000, 2, N'chai', N'Siro giảm đau, hạ sốt cho trẻ em.', N'/images/Nurofen.jpg', 7, 5),
    (253, N'Panadol 500mg', N'Paracetamol 500mg', 20000, 1, N'hộp', N'Giảm đau, hạ sốt.', N'/images/Panadol500mg.jpg', 7, 3),
    (254, N'Panadol Extra', N'Paracetamol, caffeine', 30000, 2, N'hộp', N'Giảm đau đầu, đau cơ, hạ sốt.', N'/images/PanadolExtra.jpg', 7, 5),
    (255, N'Paracetamol STADA 500mg', N'Paracetamol 500mg', 20000, 2, N'hộp', N'Giảm đau, hạ sốt thông thường.', N'/images/Paracetamol.jpg', 7, 1),
    (256, N'Paracetamol 650mg', N'Paracetamol 650mg', 25000, 2, N'hộp', N'Giảm đau, hạ sốt.', N'/images/Paracetamol650mg.webp', 7, 1),
    (257, N'Paracetamol siro cho trẻ', N'Paracetamol 120mg/5ml', 35000, 1, N'chai', N'Siro hạ sốt giảm đau cho trẻ em.', N'/images/paracetamolsirokid.jpg', 7, 3),
    (258, N'Perskindol Active Gel', N'Chiết xuất tinh dầu thiên nhiên', 120000, 1, N'tuýp', N'Gel giảm đau cơ, bong gân.', N'/images/PerskindolActiveGel.jpg', 7, 7),
    (259, N'Salonpas', N'Methyl salicylate, menthol', 35000, 2, N'hộp', N'Miếng dán giảm đau cơ, đau lưng.', N'/images/Salonpas.webp', 7, 6),
    (260, N'Salonpas Gel', N'Methyl salicylate, menthol', 60000, 1, N'tuýp', N'Gel giảm đau cơ xương khớp.', N'/images/SalonpasGel.jpg', 7, 7),
    (261, N'Sumatriptan 50mg', N'Sumatriptan 50mg', 90000, 1, N'hộp', N'Điều trị cơn đau nửa đầu.', N'/images/Sumatriptan50mg.webp', 7, 6),
    (262, N'Tramadol 50mg', N'Tramadol HCl 50mg', 40000, 1, N'hộp', N'Giảm đau mức độ vừa đến nặng.', N'/images/Tramadol50mg.jpg', 7, 7),
    (263, N'Tylenol', N'Paracetamol', 150000, 1, N'chai', N'Giảm đau, hạ sốt.', N'/images/Tylenol.jpg', 7, 6),
    (264, N'Ultracet', N'Tramadol 37,5mg, paracetamol 325mg', 180000, 1, N'hộp', N'Giảm đau mức độ trung bình đến nặng.', N'/images/Ultracet.jpg', 7, 3),
    (265, N'ACC 200', N'Acetylcystein 200mg', 50000, 1, N'hộp', N'Thuốc tiêu nhầy, long đờm.', N'/images/ACC200.webp', 8, 6),
    (266, N'Audispray Adult', N'Nước biển sâu', 150000, 2, N'chai', N'Xịt vệ sinh tai, làm sạch ráy tai.', N'/images/Audispray.jpg', 8, 1),
    (267, N'Avamys', N'Fluticasone furoate', 180000, 2, N'chai', N'Xịt mũi điều trị viêm mũi dị ứng.', N'/images/Avamys.jpg', 8, 2),
    (268, N'Betadine Gargle', N'Povidone iodine', 60000, 1, N'chai', N'Dung dịch súc họng sát khuẩn.', N'/images/BetadineGargle.webp', 8, 2),
    (269, N'Cerumol Ear Drops', N'Dầu ô liu, chlorobutanol', 110000, 2, N'lọ', N'Thuốc nhỏ tai làm mềm ráy tai.', N'/images/CerumolEarDrops.webp', 8, 4),
    (270, N'Ciprofloxacin nhỏ tai', N'Ciprofloxacin 0,3%', 50000, 1, N'lọ', N'Thuốc nhỏ tai điều trị nhiễm khuẩn tai.', N'/images/Ciprofloxacineardrops.png', 8, 1),
    (271, N'Dextromethorphan', N'Dextromethorphan HBr', 25000, 2, N'hộp', N'Thuốc giảm ho khan.', N'/images/Dextromethorphan.jpg', 8, 8),
    (272, N'Dorithricin', N'Tyrothricin, benzalkonium, benzocaine', 65000, 1, N'hộp', N'Viên ngậm giảm đau họng, sát khuẩn.', N'/images/Dorithricin.webp', 8, 4),
    (273, N'Ray-C', N'Dung dịch vệ sinh tai', 130000, 1, N'chai', N'Xịt làm sạch tai, giảm ráy tai.', N'/images/Dungdichmemtai.png', 8, 4),
    (274, N'Flixonase', N'Fluticasone propionate', 190000, 2, N'chai', N'Xịt mũi điều trị viêm mũi dị ứng.', N'/images/Flixonase.jpg', 8, 4),
    (275, N'Fluimucil 200mg', N'Acetylcystein 200mg', 60000, 1, N'hộp', N'Thuốc long đờm.', N'/images/Fluimucil200mg.webp', 8, 5),
    (276, N'Glycerin borat 3%', N'Natri borat, glycerin', 20000, 2, N'lọ', N'Dung dịch bôi miệng, sát khuẩn tại chỗ.', N'/images/glycerinborat.jpg', 8, 5),
    (277, N'Hexaspray', N'Biclotymol', 70000, 1, N'chai', N'Xịt họng sát khuẩn, giảm viêm.', N'/images/Hexaspray.jpg', 8, 5),
    (278, N'Iliadin 0,05%', N'Oxymetazoline 0,05%', 55000, 1, N'lọ', N'Thuốc nhỏ mũi trị nghẹt mũi.', N'/images/Iliadin0.05_.jpg', 8, 8),
    (279, N'Lysopaine', N'Lysozyme, bacitracin', 60000, 2, N'hộp', N'Viên ngậm giảm viêm họng.', N'/images/Lysopaine.jpg', 8, 4),
    (280, N'Mometasone nasal spray', N'Mometasone furoate', 120000, 2, N'chai', N'Xịt mũi điều trị viêm mũi dị ứng.', N'/images/Mometasonenasalspray.jpg', 8, 5),
    (281, N'Mucosolvan', N'Ambroxol HCl', 60000, 2, N'chai', N'Tiêu đờm trong bệnh hô hấp.', N'/images/Mucosolvan.avif', 8, 2),
    (282, N'Nasonex', N'Mometasone furoate', 200000, 2, N'chai', N'Xịt mũi điều trị viêm mũi dị ứng.', N'/images/Nasonex.jpg', 8, 2),
    (283, N'Natri clorid 0,9%', N'Natri clorid 0,9%', 10000, 2, N'chai', N'Nước muối nhỏ mũi, rửa mũi.', N'/images/Natriclorid0.9_.jpg', 8, 3),
    (284, N'Otinum', N'Choline salicylate', 50000, 2, N'lọ', N'Thuốc nhỏ tai giảm viêm.', N'/images/Otinum.jpg', 8, 8),
    (285, N'Otipax', N'Phenazone, lidocaine', 90000, 2, N'lọ', N'Thuốc nhỏ tai giảm đau viêm tai giữa.', N'/images/Otipax.jpg', 8, 5),
    (286, N'Otrivin 0,1%', N'Xylometazoline 0,1%', 60000, 1, N'chai', N'Xịt mũi giảm nghẹt mũi.', N'/images/Otrivin0.1_.jpg', 8, 4),
    (287, N'Otrivin trẻ em', N'Xylometazoline 0,05%', 60000, 2, N'chai', N'Thuốc nhỏ mũi cho trẻ em.', N'/images/Otrivintreem.webp', 8, 3),
    (288, N'Physiomer Nasal Spray', N'Nước biển', 140000, 1, N'chai', N'Xịt vệ sinh mũi.', N'/images/PhysiomerNasalSpray.jpg', 8, 3),
    (289, N'Prospan siro ho', N'Chiết xuất lá thường xuân', 120000, 2, N'chai', N'Siro giảm ho, long đờm.', N'/images/Prospansiroho.webp', 8, 5),
    (290, N'Rhinocort Aqua', N'Budesonide', 180000, 1, N'chai', N'Xịt mũi điều trị viêm mũi.', N'/images/RhinocortAqua.jpg', 8, 3),
    (291, N'Siro ho thảo dược', N'Thảo dược', 50000, 2, N'chai', N'Siro giảm ho, giảm đau họng.', N'/images/Sirohothaoduoc.jpg', 8, 2),
    (292, N'Sterimar Baby', N'Nước biển', 120000, 2, N'chai', N'Xịt mũi vệ sinh cho trẻ nhỏ.', N'/images/SterimarBaby.webp', 8, 6),
    (293, N'Sterimar Blocked Nose', N'Nước biển ưu trương', 150000, 2, N'chai', N'Xịt mũi giảm nghẹt mũi.', N'/images/SterimarBlockedNose.png', 8, 1),
    (294, N'Strepsils', N'Amylmetacresol, dichlorobenzyl alcohol', 40000, 2, N'hộp', N'Viên ngậm giảm đau họng.', N'/images/Strepsils.jpg', 8, 1),
    (295, N'Tantum Verde spray', N'Benzydamine HCl', 100000, 2, N'chai', N'Xịt họng giảm đau, kháng viêm.', N'/images/TantumVerdespray.jpg', 8, 7),
    (296, N'Tyrotab', N'Tyrothricin', 25000, 1, N'hộp', N'Viên ngậm sát khuẩn họng.', N'/images/Tyrotab.jpg', 8, 4),
    (297, N'Voltaren Emulgel', N'Diclofenac diethylamine', 130000, 1, N'tuýp', N'Gel giảm đau cơ, khớp.', N'/images/Voltaren.jpg', 8, 8),
    (298, N'Xịt nước biển sâu', N'Nước biển sâu', 100000, 2, N'chai', N'Xịt vệ sinh mũi hằng ngày.', N'/images/Xisatnuocbiensau.webp', 8, 6),
    (299, N'Xylometazoline', N'Xylometazoline HCl', 40000, 2, N'chai', N'Thuốc xịt giảm nghẹt mũi.', N'/images/Xylometazoline.jpg', 8, 3);

    SET IDENTITY_INSERT [dbo].[Product] OFF;

    -- Tạo lô tồn kho đơn giản cho mỗi sản phẩm (để StockQuantity có ý nghĩa)
    INSERT INTO [InventoryBatch] (ProductId, LotNumber, Quantity, MfgDate, ExpDate, ImportPrice)
    SELECT Id, 'LOT-' + CAST(Id AS VARCHAR(10)) + '-2025', StockQuantity, '2025-01-01', '2027-12-31', CAST(Price * 0.6 AS DECIMAL(18,2))
    FROM [Product];

    COMMIT TRANSACTION;
    PRINT N'Đã import thành công ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' sản phẩm.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'Lỗi: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

-- Kiểm tra
SELECT COUNT(*) AS ProductCount FROM [Product];
SELECT c.Id, c.Name, COUNT(p.Id) AS ProductCount
FROM [Category] c
LEFT JOIN [Product] p ON p.CategoryId = c.Id
GROUP BY c.Id, c.Name
ORDER BY c.Id;
GO

-- Domain constraints protect the database even when a request bypasses the UI.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Product_Stock_Price')
    ALTER TABLE [dbo].[Product] WITH CHECK ADD CONSTRAINT CK_Product_Stock_Price CHECK (StockQuantity >= 0 AND Price >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_InventoryBatch_Values')
    ALTER TABLE [dbo].[InventoryBatch] WITH CHECK ADD CONSTRAINT CK_InventoryBatch_Values CHECK (Quantity > 0 AND ImportPrice >= 0 AND MfgDate < ExpDate);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_OrderDetail_Values')
    ALTER TABLE [dbo].[OrderDetail] WITH CHECK ADD CONSTRAINT CK_OrderDetail_Values CHECK (Quantity > 0 AND UnitPrice >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_Values')
    ALTER TABLE [dbo].[Order] WITH CHECK ADD CONSTRAINT CK_Order_Values CHECK (TotalAmount >= 0 AND OrderType IN ('Online','POS') AND Status IN ('Pending','Checking','Approved','Shipping','Completed','Cancelled'));
GO

-- ==================== VERIFY ====================
PRINT N'=== Users ===';
SELECT Id, Email, Role, Status FROM [User];

PRINT N'=== Categories ===';
SELECT Id, Name FROM [Category] ORDER BY Id;

PRINT N'=== Suppliers ===';
SELECT Id, Name FROM [Supplier] ORDER BY Id;

PRINT N'=== Products ===';
SELECT COUNT(*) AS ProductCount FROM [Product];
SELECT TOP 10 Id, Name, Price, StockQuantity, Unit, CategoryId, SupplierId, 
       CASE WHEN ImageUrl = N'' THEN 'NO IMAGE' ELSE 'OK' END AS ImageStatus
FROM [Product] ORDER BY Id;

PRINT N'Tạo tài khoản admin đầu tiên qua POST /api/auth/bootstrap-admin.';
GO
