/*
  HoaXinhStore SQL Server bootstrap script
  Safe to run multiple times.
*/

IF DB_ID(N'HoaXinhStoreDb') IS NULL
BEGIN
    CREATE DATABASE [HoaXinhStoreDb];
END
GO

USE [HoaXinhStoreDb];
GO

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Slug NVARCHAR(120) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Categories_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2 NULL
    );

    CREATE UNIQUE INDEX UX_Categories_Slug ON dbo.Categories(Slug);
END
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Sku NVARCHAR(50) NOT NULL,
        Name NVARCHAR(255) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        StockQuantity INT NOT NULL CONSTRAINT DF_Products_StockQuantity DEFAULT (0),
        ImageUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_Products_ImageUrl DEFAULT (N''),
        Summary NVARCHAR(1000) NOT NULL CONSTRAINT DF_Products_Summary DEFAULT (N''),
        Descriptions NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Products_Descriptions DEFAULT (N''),
        CategoryId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2 NULL,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id),
        CONSTRAINT CK_Products_Price_NonNegative CHECK (Price >= 0),
        CONSTRAINT CK_Products_Stock_NonNegative CHECK (StockQuantity >= 0)
    );

    CREATE UNIQUE INDEX UX_Products_Sku ON dbo.Products(Sku);
    CREATE INDEX IX_Products_CategoryId ON dbo.Products(CategoryId);
END
GO

IF OBJECT_ID(N'dbo.ProductImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductImages
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId INT NOT NULL,
        ImageUrl NVARCHAR(500) NOT NULL,
        IsPrimary BIT NOT NULL CONSTRAINT DF_ProductImages_IsPrimary DEFAULT (0),
        SortOrder INT NOT NULL CONSTRAINT DF_ProductImages_SortOrder DEFAULT (0),

        CONSTRAINT FK_ProductImages_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_ProductImages_ProductId ON dbo.ProductImages(ProductId);
END
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerType NVARCHAR(20) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT CK_Customers_Type CHECK (CustomerType IN (N'Guest', N'Member'))
    );

    CREATE INDEX IX_Customers_Phone_Email ON dbo.Customers(Phone, Email);
END
GO

IF OBJECT_ID(N'dbo.CustomerAddresses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAddresses
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NOT NULL,
        ReceiverName NVARCHAR(150) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        AddressLine NVARCHAR(300) NOT NULL,
        Ward NVARCHAR(100) NOT NULL CONSTRAINT DF_CustomerAddresses_Ward DEFAULT (N''),
        District NVARCHAR(100) NOT NULL CONSTRAINT DF_CustomerAddresses_District DEFAULT (N''),
        Province NVARCHAR(100) NOT NULL CONSTRAINT DF_CustomerAddresses_Province DEFAULT (N''),
        IsDefault BIT NOT NULL CONSTRAINT DF_CustomerAddresses_IsDefault DEFAULT (0),

        CONSTRAINT FK_CustomerAddresses_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_CustomerAddresses_CustomerId ON dbo.CustomerAddresses(CustomerId);
END
GO

IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderNo NVARCHAR(30) NOT NULL,
        CustomerId INT NOT NULL,
        OrderStatus NVARCHAR(30) NOT NULL,
        PaymentStatus NVARCHAR(30) NOT NULL,
        PaymentMethod INT NOT NULL,
        Subtotal DECIMAL(18,2) NOT NULL,
        DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_Discount DEFAULT (0),
        ShippingFee DECIMAL(18,2) NOT NULL CONSTRAINT DF_Orders_Shipping DEFAULT (0),
        TotalAmount DECIMAL(18,2) NOT NULL,
        Note NVARCHAR(500) NOT NULL CONSTRAINT DF_Orders_Note DEFAULT (N''),
        IsExportInvoice BIT NOT NULL CONSTRAINT DF_Orders_IsExportInvoice DEFAULT (0),
        VatCompanyName NVARCHAR(200) NOT NULL CONSTRAINT DF_Orders_VatCompanyName DEFAULT (N''),
        VatTaxCode NVARCHAR(100) NOT NULL CONSTRAINT DF_Orders_VatTaxCode DEFAULT (N''),
        VatCompanyAddress NVARCHAR(300) NOT NULL CONSTRAINT DF_Orders_VatCompanyAddress DEFAULT (N''),
        VatEmail NVARCHAR(100) NOT NULL CONSTRAINT DF_Orders_VatEmail DEFAULT (N''),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Orders_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
        CONSTRAINT CK_Orders_PaymentMethod CHECK (PaymentMethod IN (0,1)),
        CONSTRAINT CK_Orders_OrderStatus CHECK (OrderStatus IN (N'PendingConfirm',N'Confirmed',N'Cancelled',N'Completed')),
        CONSTRAINT CK_Orders_PaymentStatus CHECK (PaymentStatus IN (N'Pending',N'AwaitingGateway',N'Paid',N'Failed',N'Cancelled')),
        CONSTRAINT CK_Orders_Amounts CHECK (Subtotal >= 0 AND DiscountAmount >= 0 AND ShippingFee >= 0 AND TotalAmount >= 0)
    );

    CREATE UNIQUE INDEX UX_Orders_OrderNo ON dbo.Orders(OrderNo);
    CREATE INDEX IX_Orders_CustomerId_CreatedAtUtc ON dbo.Orders(CustomerId, CreatedAtUtc DESC);
END
GO

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId INT NOT NULL,
        ProductId INT NOT NULL,
        ProductNameSnapshot NVARCHAR(255) NOT NULL,
        SkuSnapshot NVARCHAR(50) NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        LineTotal DECIMAL(18,2) NOT NULL,

        CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id),
        CONSTRAINT CK_OrderItems_Quantity_Positive CHECK (Quantity > 0),
        CONSTRAINT CK_OrderItems_Prices_NonNegative CHECK (UnitPrice >= 0 AND LineTotal >= 0)
    );

    CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems(ProductId);
END
GO

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId INT NOT NULL,
        Provider NVARCHAR(30) NOT NULL,
        PaymentMethod NVARCHAR(30) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        TransactionRef NVARCHAR(100) NOT NULL CONSTRAINT DF_Payments_TransactionRef DEFAULT (N''),
        PaidAtUtc DATETIME2 NULL,
        RawResponseJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Payments_RawResponseJson DEFAULT (N''),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Payments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Payments_Amount_NonNegative CHECK (Amount >= 0),
        CONSTRAINT CK_Payments_Status CHECK (Status IN (N'Pending',N'Initiated',N'Paid',N'Failed',N'Cancelled',N'Refunded'))
    );

    CREATE INDEX IX_Payments_OrderId_Status ON dbo.Payments(OrderId, Status);
END
GO

/* Seed products from Google Sheet */

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (Name, Slug, IsActive)
    VALUES (N'Mỹ phẩm', N'mypham', 1),
           (N'Thực phẩm', N'thucpham', 1),
           (N'Thiết bị & gia dụng', N'thietbi', 1);
END
GO

DECLARE @CategoryMap TABLE (Slug NVARCHAR(120) PRIMARY KEY, CategoryId INT NOT NULL);
INSERT INTO @CategoryMap(Slug, CategoryId)
SELECT Slug, Id FROM dbo.Categories WHERE Slug IN (N'mypham', N'thucpham', N'thietbi');

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0001')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0001', N'MÁY LỌC NƯỚC VILLAEM 2', 14775000, 100, N'https://bizweb.dktcdn.net/thumb/large/100/506/908/products/18ar.png?v=1772245124270', N'Tặng 1 năm gói dịch vụ Heart Service trị giá 2,880,000 vnđ khi mua sản phẩm Máy lọc nước thông minh theo trường phái tối giản cho cuộc sống hiện đại Thiết kế trẻ trung, tinh tế cho những gia đình trẻ 4 chế độ nước đáp ứng mọi nhu cầu sử dụng: Nước Nóng – Nước Ấm – Nước Thường – Nước Lạnh Hệ thống lọc 5 bước tích hợp thông minh trong 4 lõi lọc loại bỏ các tạp chất độc hại theo tiêu chuẩn quốc tế Lõi lọc RO cao cấp đem lại nguồn nước sạch, tinh khiết Lõi lọc kháng khuẩn chống lại tái nhiễm khuẩn ngay trong bình chứa nước', N'Tặng 1 năm gói dịch vụ Heart Service trị giá 2,880,000 vnđ khi mua sản phẩm Máy lọc nước thông minh theo trường phái tối giản cho cuộc sống hiện đại Thiết kế trẻ trung, tinh tế cho những gia đình trẻ 4 chế độ nước đáp ứng mọi nhu cầu sử dụng: Nước Nóng – Nước Ấm – Nước Thường – Nước Lạnh Hệ thống lọc 5 bước tích hợp thông minh trong 4 lõi lọc loại bỏ các tạp chất độc hại theo tiêu chuẩn quốc tế Lõi lọc RO cao cấp đem lại nguồn nước sạch, tinh khiết Lõi lọc kháng khuẩn chống lại tái nhiễm khuẩn ngay trong bình chứa nước Lấy nước chảy liên tục với thao tác gạt cần đơn giản Đèn chiếu sáng chỉ dẫn cho thiết bị khi không đủ ánh sáng vào ban đêm sử dụng cảm biến ánh sáng Lấy nước theo nhiệt độ mong muốn chỉ với một bước xoay chiều đơn giản Chế độ Tiết kiệm điện (ECO) vào ban đêm Hiện thị mức nước khi gần hết giúp người dùng dễ nhận biết Dễ dàng tự vệ sinh với đầu vòi tháo rời Khóa an toàn tránh bỏng nước nóng, phù hợp với các gia đình có trẻ nhỏ', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0001')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0001', N'Xịt phòng Shay', 99000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_xitphong.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0002')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0002', N'Xịt Thơm Vải Idol Hàn Quốc W DRESSROOM 70ML', 95000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_xithom.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0002')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0002', N'[COWAY] BA35-A NẮP BỒN CẦU THÔNG MINH BATERI BIDET', 7900000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_napboncau.png', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0003')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0003', N'[COWAY] MÁY LỌC NƯỚC CORE - CHP-671R', 24300000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_maylocnuoc.png', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0003')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0003', N'[FREESHIP] Thuốc Nhuộm Tóc Hàn Quốc Lắc Trộn Siêu Nhanh Dạng Gel', 380000, 100, N'https://hoaxinhstore.com/assets/img/mp_thuocnhuomtoc.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0004')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0004', N'[COWAY] MÁY LỌC KHÔNG KHÍ CARTRIDGE (P) - AP-1019C', 5100000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_maylockkpink.png', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0004')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0004', N'Bột Rửa Mặt Hydrogen Enzyme AIRIVE Airy Skin Spa Cleanser 50g (pH Axid nhẹ)', 790000, 100, N'https://hoaxinhstore.com/assets/img/mp_suaruamat.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TP-0001')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TP-0001', N'Tinh chất Nghệ Nano Curcumin 365 Premium Hàn Quốc 7680mg', 1250000, 100, N'https://hoaxinhstore.com/assets/img/tp_tinhchatnghe.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thucpham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0005')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0005', N'[COWAY] AP-1008DH MÁY LỌC KHÔNG KHÍ DOLOMITIES', 10125000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_maylockk10tr.png', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0005')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0005', N'Son Dưỡng Mềm Môi Hương Dâu Enriched Lip Essence Strawberry 8.7g', 65000, 100, N'https://hoaxinhstore.com/assets/img/mp_son.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TP-0002')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TP-0002', N'Thạch Collagen Nghệ Nano Curcumin 365 Premium - Vị Xoài 750g (30 Tuýp/Hộp)', 495000, 100, N'https://hoaxinhstore.com/assets/img/tp_thachnghe.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thucpham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'MP-0006')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'MP-0006', N'Kem Dưỡng Ẩm, Kem Lót Dầu Cừu Vitamin KICHO Sheep oil cream Lanolin & Berry 65ml', 420000, 100, N'https://hoaxinhstore.com/assets/img/mp_kemduongam.png', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'mypham';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'TB-0006')
BEGIN
    INSERT INTO dbo.Products
    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)
    SELECT N'TB-0006', N'CAMERA CHỐNG TRỘM CÔNG NGHỆ BLOCKCHAIN HÀN QUỐC', 1550000, 100, N'https://hoaxinhstore.com/assets/img/tbgd_camera.jpg', N'', N'', cm.CategoryId, 1
    FROM @CategoryMap cm WHERE cm.Slug = N'thietbi';
END

GO
