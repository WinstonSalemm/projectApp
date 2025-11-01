-- Добавление/обновление схемы для модуля "Долг" (MySQL 8.0+)
-- Скрипт идемпотентный: безопасно запускать повторно. Требуется MySQL 8.0.29+ для ADD COLUMN IF NOT EXISTS

START TRANSACTION;

-- 0) Базовые таблицы (на случай отсутствия)
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

-- 1) Debts: новые поля
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `StoreId` INT NULL;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `ManagerId` INT NULL;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `UpdatedAt` DATETIME(6) NULL;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `MissingQtyForConversion` DECIMAL(18,3) NULL;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `RetryCount` INT NOT NULL DEFAULT 0;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `LastRetryAt` DATETIME(6) NULL;
ALTER TABLE `Debts` ADD COLUMN IF NOT EXISTS `LastMovementAt` DATETIME(6) NULL;

-- Индексы для ускорения выборок по менеджеру/статусу/клиенту
CREATE INDEX IF NOT EXISTS `IX_Debts_ManagerId` ON `Debts` (`ManagerId`);
CREATE INDEX IF NOT EXISTS `IX_Debts_Client_Status` ON `Debts` (`ClientId`, `Status`);

-- (опционально) связь на Users.Id, если используется Users
-- ALTER TABLE `Debts` ADD CONSTRAINT `FK_Debts_Users_ManagerId` FOREIGN KEY (`ManagerId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL;

-- 2) DebtItems: гарантируем типы и индексы (если колонок нет — добавляем)
ALTER TABLE `DebtItems` MODIFY COLUMN `Qty`   DECIMAL(18,3) NOT NULL;
ALTER TABLE `DebtItems` MODIFY COLUMN `Price` DECIMAL(18,2) NOT NULL;
ALTER TABLE `DebtItems` MODIFY COLUMN `Total` DECIMAL(18,2) NOT NULL;

-- 3) DebtPayments: дополнительные поля
ALTER TABLE `DebtPayments` ADD COLUMN IF NOT EXISTS `Method` VARCHAR(32) NULL;
ALTER TABLE `DebtPayments` ADD COLUMN IF NOT EXISTS `Comment` VARCHAR(512) NULL;
ALTER TABLE `DebtPayments` ADD COLUMN IF NOT EXISTS `ManagerId` INT NULL;
ALTER TABLE `DebtPayments` ADD COLUMN IF NOT EXISTS `CreatedBy` VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS `IX_DebtPayments_PaidAt` ON `DebtPayments` (`PaidAt`);
CREATE INDEX IF NOT EXISTS `IX_DebtPayments_ManagerId` ON `DebtPayments` (`ManagerId`);
-- (опционально) внеш. ключ на Users
-- ALTER TABLE `DebtPayments` ADD CONSTRAINT `FK_DebtPayments_Users_ManagerId` FOREIGN KEY (`ManagerId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL;

-- 4) DebtMovements: детализация списаний по партиям (новая таблица)
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

COMMIT;

-- Примечание:
-- Если ваша версия MySQL не поддерживает "ADD COLUMN IF NOT EXISTS" или "CREATE INDEX IF NOT EXISTS",
-- можно запускать скрипт по разделам, игнорируя ошибки "Duplicate column name" / "Duplicate key name".
