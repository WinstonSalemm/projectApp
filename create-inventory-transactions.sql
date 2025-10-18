-- =====================================================
-- CREATE TABLE: InventoryTransactions
-- =====================================================
-- Таблица для учета всех движений товара на складе
-- =====================================================

CREATE TABLE IF NOT EXISTS InventoryTransactions (
    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
    ProductId INT NOT NULL,
    Register INT NOT NULL,
    Type INT NOT NULL,
    Qty DECIMAL(18,3) NOT NULL,
    UnitCost DECIMAL(18,2) NOT NULL,
    BatchId INT NULL,
    SaleId INT NULL,
    ReturnId INT NULL,
    ReservationId INT NULL,
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CreatedBy VARCHAR(255) NULL,
    Note TEXT NULL,
    
    INDEX IX_InventoryTransactions_ProductId (ProductId),
    INDEX IX_InventoryTransactions_BatchId (BatchId),
    INDEX IX_InventoryTransactions_SaleId (SaleId),
    INDEX IX_InventoryTransactions_ReservationId (ReservationId),
    INDEX IX_InventoryTransactions_CreatedAt (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SELECT 'InventoryTransactions table created' AS Result;

DESCRIBE InventoryTransactions;
