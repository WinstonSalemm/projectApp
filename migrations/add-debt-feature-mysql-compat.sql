-- MySQL-совместимый скрипт без ALTER ... IF NOT EXISTS / CREATE INDEX IF NOT EXISTS
-- Работает на Railway/MySQL 8.0.x (проверка через INFORMATION_SCHEMA + динамический SQL)

-- 0) Базовые таблицы (если отсутствуют)
CREATE TABLE IF NOT EXISTS `Debts` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ClientId` INT NOT NULL,
  `SaleId` INT NULL,
  `Amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `OriginalAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `DueDate` DATETIME(6) NOT NULL,
  `Status` INT NOT NULL DEFAULT 0,
  `Notes` TEXT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_Debts_ClientId` (`ClientId`),
  INDEX `IX_Debts_Status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `DebtItems` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `DebtId` INT NOT NULL,
  `ProductId` INT NOT NULL,
  `ProductName` VARCHAR(256) NOT NULL,
  `Sku` VARCHAR(64) NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `Price` DECIMAL(18,2) NOT NULL,
  `Total` DECIMAL(18,2) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `UpdatedAt` DATETIME(6) NULL,
  `UpdatedBy` VARCHAR(64) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_DebtItems_DebtId` (`DebtId`),
  CONSTRAINT `FK_DebtItems_Debts_DebtId` FOREIGN KEY (`DebtId`) REFERENCES `Debts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `DebtPayments` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `DebtId` INT NOT NULL,
  `Amount` DECIMAL(18,2) NOT NULL,
  `PaidAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_DebtPayments_DebtId` (`DebtId`),
  CONSTRAINT `FK_DebtPayments_Debts_DebtId` FOREIGN KEY (`DebtId`) REFERENCES `Debts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `DebtMovements` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `DebtId` INT NOT NULL,
  `DebtItemId` INT NULL,
  `BatchId` INT NULL,
  `RegisterAtMovement` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitCost` DECIMAL(18,2) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_DebtMovements_DebtId` (`DebtId`),
  INDEX `IX_DebtMovements_DebtItemId` (`DebtItemId`),
  INDEX `IX_DebtMovements_BatchId` (`BatchId`),
  CONSTRAINT `FK_DebtMovements_Debts_DebtId` FOREIGN KEY (`DebtId`) REFERENCES `Debts` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_DebtMovements_DebtItems_DebtItemId` FOREIGN KEY (`DebtItemId`) REFERENCES `DebtItems` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_DebtMovements_Batches_BatchId` FOREIGN KEY (`BatchId`) REFERENCES `Batches` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 1) Debts: добавление недостающих колонок через INFORMATION_SCHEMA
SET @db := DATABASE();

-- Debts.StoreId
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='StoreId');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `StoreId` INT NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.ManagerId
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='ManagerId');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `ManagerId` INT NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.UpdatedAt
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='UpdatedAt');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `UpdatedAt` DATETIME(6) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.MissingQtyForConversion
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='MissingQtyForConversion');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `MissingQtyForConversion` DECIMAL(18,3) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.RetryCount
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='RetryCount');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `RetryCount` INT NOT NULL DEFAULT 0;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.LastRetryAt
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='LastRetryAt');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `LastRetryAt` DATETIME(6) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Debts.LastMovementAt
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND COLUMN_NAME='LastMovementAt');
SET @sql := IF(@exists=0, 'ALTER TABLE `Debts` ADD COLUMN `LastMovementAt` DATETIME(6) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Индексы для Debts
-- IX_Debts_ManagerId
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND INDEX_NAME='IX_Debts_ManagerId');
SET @sql := IF(@exists=0, 'CREATE INDEX `IX_Debts_ManagerId` ON `Debts` (`ManagerId`);', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- IX_Debts_Client_Status
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Debts' AND INDEX_NAME='IX_Debts_Client_Status');
SET @sql := IF(@exists=0, 'CREATE INDEX `IX_Debts_Client_Status` ON `Debts` (`ClientId`, `Status`);', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 2) DebtPayments: добавление недостающих колонок
-- DebtPayments.Method
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND COLUMN_NAME='Method');
SET @sql := IF(@exists=0, 'ALTER TABLE `DebtPayments` ADD COLUMN `Method` VARCHAR(32) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- DebtPayments.Comment
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND COLUMN_NAME='Comment');
SET @sql := IF(@exists=0, 'ALTER TABLE `DebtPayments` ADD COLUMN `Comment` VARCHAR(512) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- DebtPayments.ManagerId
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND COLUMN_NAME='ManagerId');
SET @sql := IF(@exists=0, 'ALTER TABLE `DebtPayments` ADD COLUMN `ManagerId` INT NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- DebtPayments.CreatedBy
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND COLUMN_NAME='CreatedBy');
SET @sql := IF(@exists=0, 'ALTER TABLE `DebtPayments` ADD COLUMN `CreatedBy` VARCHAR(64) NULL;', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Индексы для DebtPayments
-- IX_DebtPayments_PaidAt
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND INDEX_NAME='IX_DebtPayments_PaidAt');
SET @sql := IF(@exists=0, 'CREATE INDEX `IX_DebtPayments_PaidAt` ON `DebtPayments` (`PaidAt`);', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- IX_DebtPayments_ManagerId
SET @exists := (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='DebtPayments' AND INDEX_NAME='IX_DebtPayments_ManagerId');
SET @sql := IF(@exists=0, 'CREATE INDEX `IX_DebtPayments_ManagerId` ON `DebtPayments` (`ManagerId`);', 'SELECT 1;');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 3) Приведение типов для DebtItems (безопасно, если уже нужные)
ALTER TABLE `DebtItems` MODIFY COLUMN `Qty`   DECIMAL(18,3) NOT NULL;
ALTER TABLE `DebtItems` MODIFY COLUMN `Price` DECIMAL(18,2) NOT NULL;
ALTER TABLE `DebtItems` MODIFY COLUMN `Total` DECIMAL(18,2) NOT NULL;
