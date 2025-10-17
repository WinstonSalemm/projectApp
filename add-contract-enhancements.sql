-- Миграция: Расширение системы договоров
-- Дата: 2025-10-17

-- 1. Добавляем новые поля в таблицу Contracts
ALTER TABLE `Contracts` 
  ADD COLUMN `CreatedBy` VARCHAR(64) NULL AFTER `CreatedAt`,
  ADD COLUMN `TotalAmount` DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER `Note`,
  ADD COLUMN `PaidAmount` DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER `TotalAmount`,
  ADD COLUMN `TotalItemsCount` INT NOT NULL DEFAULT 0 AFTER `PaidAmount`,
  ADD COLUMN `DeliveredItemsCount` INT NOT NULL DEFAULT 0 AFTER `TotalItemsCount`;

-- 2. Добавляем поля в таблицу ContractItems
ALTER TABLE `ContractItems`
  ADD COLUMN `Sku` VARCHAR(64) NOT NULL DEFAULT '' AFTER `ProductId`,
  ADD COLUMN `DeliveredQty` DECIMAL(18,3) NOT NULL DEFAULT 0 AFTER `Qty`;

-- 3. Создаем таблицу ContractPayments
CREATE TABLE IF NOT EXISTS `ContractPayments` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ContractId` INT NOT NULL,
  `Amount` DECIMAL(18,2) NOT NULL,
  `PaidAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  `Note` VARCHAR(512) NULL,
  `Method` INT NOT NULL DEFAULT 1,
  PRIMARY KEY (`Id`),
  INDEX `IX_ContractPayments_ContractId` (`ContractId`),
  INDEX `IX_ContractPayments_PaidAt` (`PaidAt`),
  CONSTRAINT `FK_ContractPayments_Contracts` 
    FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Создаем таблицу ContractDeliveries
CREATE TABLE IF NOT EXISTS `ContractDeliveries` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ContractId` INT NOT NULL,
  `ContractItemId` INT NOT NULL,
  `ProductId` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `DeliveredAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  `Note` VARCHAR(512) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ContractDeliveries_ContractId` (`ContractId`),
  INDEX `IX_ContractDeliveries_ContractItemId` (`ContractItemId`),
  INDEX `IX_ContractDeliveries_DeliveredAt` (`DeliveredAt`),
  CONSTRAINT `FK_ContractDeliveries_Contracts` 
    FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ContractDeliveries_ContractItems` 
    FOREIGN KEY (`ContractItemId`) REFERENCES `ContractItems` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. Создаем таблицу ContractDeliveryBatches (связь отгрузок с партиями)
CREATE TABLE IF NOT EXISTS `ContractDeliveryBatches` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ContractDeliveryId` INT NOT NULL,
  `BatchId` INT NOT NULL,
  `RegisterAtDelivery` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitCost` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ContractDeliveryBatches_ContractDeliveryId` (`ContractDeliveryId`),
  INDEX `IX_ContractDeliveryBatches_BatchId` (`BatchId`),
  CONSTRAINT `FK_ContractDeliveryBatches_ContractDeliveries` 
    FOREIGN KEY (`ContractDeliveryId`) REFERENCES `ContractDeliveries` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ContractDeliveryBatches_Batches` 
    FOREIGN KEY (`BatchId`) REFERENCES `Batches` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. Обновляем существующие договоры (заполняем TotalAmount)
UPDATE `Contracts` c
SET c.TotalAmount = (
  SELECT COALESCE(SUM(ci.Qty * ci.UnitPrice), 0)
  FROM `ContractItems` ci
  WHERE ci.ContractId = c.Id
);

-- 7. Обновляем TotalItemsCount
UPDATE `Contracts` c
SET c.TotalItemsCount = (
  SELECT COUNT(*)
  FROM `ContractItems` ci
  WHERE ci.ContractId = c.Id
);
