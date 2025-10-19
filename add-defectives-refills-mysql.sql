-- ===================================================================
-- СИСТЕМА БРАКА И ПЕРЕЗАРЯДКИ - MySQL VERSION
-- ===================================================================
-- Для применения к MySQL на Railway
-- ===================================================================

-- 1. ТАБЛИЦА БРАКА
CREATE TABLE IF NOT EXISTS DefectiveItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    ProductName VARCHAR(500) NOT NULL,
    Sku VARCHAR(100),
    Quantity INT NOT NULL,
    Warehouse INT NOT NULL DEFAULT 0,
    Reason TEXT,
    Status INT NOT NULL DEFAULT 0,
    CreatedBy VARCHAR(100) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CancelledBy VARCHAR(100),
    CancelledAt DATETIME,
    CancellationReason TEXT,
    INDEX IX_DefectiveItems_ProductId (ProductId),
    INDEX IX_DefectiveItems_CreatedAt (CreatedAt DESC),
    INDEX IX_DefectiveItems_Status (Status),
    INDEX IX_DefectiveItems_Warehouse (Warehouse)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 2. ТАБЛИЦА ПЕРЕЗАРЯДКИ
CREATE TABLE IF NOT EXISTS RefillOperations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    ProductName VARCHAR(500) NOT NULL,
    Sku VARCHAR(100),
    Quantity INT NOT NULL,
    Warehouse INT NOT NULL DEFAULT 0,
    CostPerUnit DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCost DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes TEXT,
    Status INT NOT NULL DEFAULT 0,
    CreatedBy VARCHAR(100) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CancelledBy VARCHAR(100),
    CancelledAt DATETIME,
    CancellationReason TEXT,
    INDEX IX_RefillOperations_ProductId (ProductId),
    INDEX IX_RefillOperations_CreatedAt (CreatedAt DESC),
    INDEX IX_RefillOperations_Status (Status),
    INDEX IX_RefillOperations_Warehouse (Warehouse)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Проверка
SELECT 'Tables created successfully!' as Status;
SELECT TABLE_NAME FROM information_schema.TABLES 
WHERE TABLE_SCHEMA = DATABASE() 
AND TABLE_NAME IN ('DefectiveItems', 'RefillOperations');
