CREATE TABLE IF NOT EXISTS `SupplyCostCalculations` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `BatchId` INT NULL,
    `ExchangeRate` DECIMAL(18,2) NOT NULL DEFAULT 158.08,
    `CustomsFee` DECIMAL(18,2) NOT NULL DEFAULT 105000.00,
    `VatPercent` DECIMAL(5,2) NOT NULL DEFAULT 22.00,
    `CorrectionPercent` DECIMAL(5,2) NOT NULL DEFAULT 0.50,
    `SecurityPercent` DECIMAL(5,2) NOT NULL DEFAULT 0.20,
    `DeclarationPercent` DECIMAL(5,2) NOT NULL DEFAULT 1.00,
    `CertificationPercent` DECIMAL(5,2) NOT NULL DEFAULT 1.00,
    `CalculationBase` DECIMAL(18,2) NOT NULL DEFAULT 10000000.00,
    `LoadingPercent` DECIMAL(5,2) NOT NULL DEFAULT 1.60,
    
    `ProductId` INT NOT NULL,
    `ProductName` VARCHAR(500) NOT NULL,
    `Sku` VARCHAR(100) NULL,
    `Quantity` DECIMAL(18,3) NOT NULL,
    `PriceRub` DECIMAL(18,2) NOT NULL,
    `PriceTotal` DECIMAL(18,2) NOT NULL,
    `Weight` DECIMAL(18,3) NULL,
    
    `CustomsAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `VatAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `CorrectionAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `SecurityAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `DeclarationAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `CertificationAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `LoadingAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `DeviationAmount` DECIMAL(18,2) NULL,
    
    `TotalCost` DECIMAL(18,2) NOT NULL,
    `UnitCost` DECIMAL(18,2) NOT NULL,
    
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `CreatedBy` VARCHAR(100) NULL,
    `Notes` TEXT NULL,
    
    CONSTRAINT `FK_SupplyCostCalculations_Batches` 
        FOREIGN KEY (`BatchId`) REFERENCES `Batches`(`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_SupplyCostCalculations_Products` 
        FOREIGN KEY (`ProductId`) REFERENCES `Products`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_SupplyCostCalculations_BatchId` ON `SupplyCostCalculations`(`BatchId`);
CREATE INDEX `IX_SupplyCostCalculations_ProductId` ON `SupplyCostCalculations`(`ProductId`);
CREATE INDEX `IX_SupplyCostCalculations_CreatedAt` ON `SupplyCostCalculations`(`CreatedAt` DESC);

ALTER TABLE `SupplyCostCalculations` COMMENT = 'Детальные расчеты себестоимости для каждой партии товара НД-40';
