DROP TABLE IF EXISTS `SupplyCostCalculations`;
CREATE TABLE IF NOT EXISTS `Supplies` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Code` VARCHAR(200) NOT NULL COMMENT '№ ГТД, ввод вручную',
    `RegisterType` TINYINT NOT NULL DEFAULT 1 COMMENT '1=ND40, 2=IM40',
    `Status` TINYINT NOT NULL DEFAULT 1 COMMENT '1=HasStock, 2=Finished',
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    UNIQUE KEY `UK_Supplies_Code` (`Code`),
    INDEX `IX_Supplies_RegisterType` (`RegisterType`),
    INDEX `IX_Supplies_Status` (`Status`),
    INDEX `IX_Supplies_CreatedAt` (`CreatedAt` DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Поставки товара (ND-40 / IM-40)';
CREATE TABLE IF NOT EXISTS `SupplyItems` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `SupplyId` INT NOT NULL,
    `ProductId` INT NOT NULL,
    `Name` VARCHAR(500) NOT NULL COMMENT 'Snapshot названия товара',
    `Quantity` INT NOT NULL COMMENT 'Количество, шт',
    `PriceRub` DECIMAL(18,4) NOT NULL COMMENT 'Цена за 1 шт в рублях',
    
    CONSTRAINT `FK_SupplyItems_Supplies` 
        FOREIGN KEY (`SupplyId`) REFERENCES `Supplies`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_SupplyItems_Products` 
        FOREIGN KEY (`ProductId`) REFERENCES `Products`(`Id`) ON DELETE RESTRICT,
    
    INDEX `IX_SupplyItems_SupplyId` (`SupplyId`),
    INDEX `IX_SupplyItems_ProductId` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Позиции поставки';

CREATE TABLE IF NOT EXISTS `CostingSessions` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `SupplyId` INT NOT NULL,
    `ExchangeRate` DECIMAL(18,4) NOT NULL COMMENT 'Курс RUB→UZS',
    `VatPct` DECIMAL(18,4) NOT NULL DEFAULT 0.2200 COMMENT 'НДС, 0.22 = 22%',
    `LogisticsPct` DECIMAL(18,4) NOT NULL DEFAULT 0.0050 COMMENT 'Логистика',
    `StoragePct` DECIMAL(18,4) NOT NULL DEFAULT 0.0020 COMMENT 'Склад',
    `DeclarationPct` DECIMAL(18,4) NOT NULL DEFAULT 0.0100 COMMENT 'Декларация',
    `CertificationPct` DECIMAL(18,4) NOT NULL DEFAULT 0.0100 COMMENT 'Сертификация',
    `MChsPct` DECIMAL(18,4) NOT NULL DEFAULT 0.0000 COMMENT 'МЧС',
    `UnforeseenPct` DECIMAL(18,4) NOT NULL DEFAULT 0.0150 COMMENT 'Непредвиденные',
        `CustomsFeeAbs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000 COMMENT 'Таможня, сбор (UZS)',
    `LoadingAbs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000 COMMENT 'Погрузка (UZS)',
    `ReturnsAbs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000 COMMENT 'Возврат (UZS)',
    
    `ApportionMethod` TINYINT NOT NULL DEFAULT 1 COMMENT '1=ByQuantity',
    `IsFinalized` BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'После фикса — только чтение',
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT `FK_CostingSessions_Supplies` 
        FOREIGN KEY (`SupplyId`) REFERENCES `Supplies`(`Id`) ON DELETE CASCADE,
    
    INDEX `IX_CostingSessions_SupplyId` (`SupplyId`),
    INDEX `IX_CostingSessions_CreatedAt` (`CreatedAt` DESC),
    INDEX `IX_CostingSessions_IsFinalized` (`IsFinalized`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Сессии расчета себестоимости';

CREATE TABLE IF NOT EXISTS `CostingItemSnapshots` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `CostingSessionId` INT NOT NULL,
    `SupplyItemId` INT NOT NULL,
    
    `Name` VARCHAR(500) NOT NULL,
    `Quantity` INT NOT NULL,
    `PriceRub` DECIMAL(18,4) NOT NULL,
    `PriceUzs` DECIMAL(18,4) NOT NULL COMMENT 'PriceRub * ExchangeRate',
    
    `VatUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `LogisticsUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `StorageUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `DeclarationUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `CertificationUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `MChsUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `UnforeseenUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    
    `CustomsUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `LoadingUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    `ReturnsUzs` DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    
    `TotalCostUzs` DECIMAL(18,4) NOT NULL COMMENT 'Итог по позиции',
    `UnitCostUzs` DECIMAL(18,4) NOT NULL COMMENT 'Себестоимость за 1 шт',
    
    CONSTRAINT `FK_CostingItemSnapshots_Sessions` 
        FOREIGN KEY (`CostingSessionId`) REFERENCES `CostingSessions`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CostingItemSnapshots_SupplyItems` 
        FOREIGN KEY (`SupplyItemId`) REFERENCES `SupplyItems`(`Id`) ON DELETE CASCADE,
    
    INDEX `IX_CostingItemSnapshots_SessionId` (`CostingSessionId`),
    INDEX `IX_CostingItemSnapshots_SupplyItemId` (`SupplyItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Снапшоты расчета себестоимости для каждой позиции';
ALTER TABLE `Supplies` COMMENT = 'Поставки товара (ND-40 / IM-40). Создаются в ND-40, переводятся целиком в IM-40';
ALTER TABLE `SupplyItems` COMMENT = 'Позиции поставки с привязкой к Products';
ALTER TABLE `CostingSessions` COMMENT = 'Сессии расчета себестоимости с параметрами (курс, проценты, абсолюты)';
ALTER TABLE `CostingItemSnapshots` COMMENT = 'Детальные расчеты себестоимости для каждой позиции (snapshot)';
