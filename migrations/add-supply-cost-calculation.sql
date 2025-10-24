-- Миграция: Добавление системы расчета себестоимости НД-40
-- Дата: 2025-01-24
-- Описание: Таблица для хранения расчетов себестоимости партий товаров с учетом таможни, НДС и прочих затрат

CREATE TABLE IF NOT EXISTS SupplyCostCalculations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BatchId INTEGER NULL,
    
    -- Глобальные параметры поставки
    ExchangeRate DECIMAL(18,2) NOT NULL DEFAULT 158.08,
    CustomsFee DECIMAL(18,2) NOT NULL DEFAULT 105000.00,
    VatPercent DECIMAL(5,2) NOT NULL DEFAULT 22.00,
    CorrectionPercent DECIMAL(5,2) NOT NULL DEFAULT 0.50,
    SecurityPercent DECIMAL(5,2) NOT NULL DEFAULT 0.20,
    DeclarationPercent DECIMAL(5,2) NOT NULL DEFAULT 1.00,
    CertificationPercent DECIMAL(5,2) NOT NULL DEFAULT 1.00,
    CalculationBase DECIMAL(18,2) NOT NULL DEFAULT 10000000.00,
    LoadingPercent DECIMAL(5,2) NOT NULL DEFAULT 1.60,
    
    -- Параметры позиции
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    PriceRub DECIMAL(18,2) NOT NULL,
    PriceTotal DECIMAL(18,2) NOT NULL,
    Weight DECIMAL(18,3) NULL,
    
    -- Рассчитанные значения
    CustomsAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    VatAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    CorrectionAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    SecurityAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeclarationAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    CertificationAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    LoadingAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeviationAmount DECIMAL(18,2) NULL,
    
    -- Итоговые значения
    TotalCost DECIMAL(18,2) NOT NULL,
    UnitCost DECIMAL(18,2) NOT NULL,
    
    -- Мета-информация
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy TEXT NULL,
    Notes TEXT NULL,
    
    FOREIGN KEY (BatchId) REFERENCES Batches(Id) ON DELETE SET NULL,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS IX_SupplyCostCalculations_BatchId ON SupplyCostCalculations(BatchId);
CREATE INDEX IF NOT EXISTS IX_SupplyCostCalculations_ProductId ON SupplyCostCalculations(ProductId);
CREATE INDEX IF NOT EXISTS IX_SupplyCostCalculations_CreatedAt ON SupplyCostCalculations(CreatedAt DESC);

-- Комментарии к таблице
-- Эта таблица хранит детальные расчеты себестоимости для каждой партии товара НД-40
-- Позволяет отследить все компоненты цены: таможня, НДС, логистика, сертификация и т.д.
-- Себестоимость фиксируется при создании партии и больше не меняется
