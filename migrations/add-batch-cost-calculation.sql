CREATE TABLE IF NOT EXISTS BatchCostSettings (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SupplyId INT NOT NULL,
    
    -- Глобальные параметры
    ExchangeRate DECIMAL(10,4) NOT NULL DEFAULT 158.08,
    
    -- Фиксированные суммы на ВСЮ партию (делятся на все товары)
    CustomsFixedTotal DECIMAL(15,2) NOT NULL DEFAULT 0,
    ShippingFixedTotal DECIMAL(15,2) NOT NULL DEFAULT 0,
    
    -- Проценты по умолчанию
    DefaultVatPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultLogisticsPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultWarehousePercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultDeclarationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultCertificationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultMchsPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DefaultDeviationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    
    FOREIGN KEY (SupplyId) REFERENCES Supplies(Id) ON DELETE CASCADE,
    INDEX idx_supply (SupplyId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS BatchCostCalculations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SupplyId INT NOT NULL,
    BatchId INT NULL,
    
    -- Базовые параметры
    ProductName VARCHAR(255) NOT NULL,
    Quantity INT NOT NULL,
    PriceRub DECIMAL(15,2) NOT NULL,
    ExchangeRate DECIMAL(10,4) NOT NULL,
    PriceSom DECIMAL(15,2) NOT NULL,
    
    -- НДС (записывается, но не участвует в расчетах)
    VatPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    
    -- Доля от фиксированных сумм (рассчитывается автоматически)
    CustomsAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    ShippingAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    
    -- Проценты от "цены в сумах"
    LogisticsPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    WarehousePercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DeclarationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    CertificationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    MchsPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    DeviationPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
    
    -- Рассчитанные суммы
    LogisticsAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    WarehouseAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    DeclarationAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    CertificationAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    MchsAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    DeviationAmount DECIMAL(15,2) NOT NULL DEFAULT 0,
    
    -- Итоги
    UnitCost DECIMAL(15,2) NOT NULL DEFAULT 0,
    TotalCost DECIMAL(15,2) NOT NULL DEFAULT 0,
    
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(100) NOT NULL,
    
    FOREIGN KEY (SupplyId) REFERENCES Supplies(Id) ON DELETE CASCADE,
    FOREIGN KEY (BatchId) REFERENCES Batches(Id) ON DELETE SET NULL,
    INDEX idx_supply (SupplyId),
    INDEX idx_batch (BatchId),
    INDEX idx_created (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE BatchCostSettings COMMENT = 'Общие настройки расчета себестоимости для поставки';
ALTER TABLE BatchCostCalculations COMMENT = 'Детальный расчет себестоимости каждой партии товара';