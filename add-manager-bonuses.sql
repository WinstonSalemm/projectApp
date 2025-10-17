-- Миграция: Система бонусов менеджеров
-- Дата: 2025-10-17

-- Создаем таблицу бонусов менеджеров
CREATE TABLE IF NOT EXISTS `ManagerBonuses` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `UserName` VARCHAR(64) NOT NULL,
  `Year` INT NOT NULL,
  `Month` INT NOT NULL,
  `TotalSales` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `OwnClientsSales` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `BonusAmount` DECIMAL(18,2) NOT NULL DEFAULT 0,
  `BonusPercent` DECIMAL(5,2) NOT NULL DEFAULT 0,
  `SalesCount` INT NOT NULL DEFAULT 0,
  `OwnClientsCount` INT NOT NULL DEFAULT 0,
  `IsPaid` TINYINT(1) NOT NULL DEFAULT 0,
  `PaidAt` DATETIME(6) NULL,
  `CalculatedAt` DATETIME(6) NOT NULL,
  `Note` VARCHAR(512) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ManagerBonuses_UserName` (`UserName`),
  INDEX `IX_ManagerBonuses_Year_Month` (`Year`, `Month`),
  UNIQUE INDEX `UX_ManagerBonuses_UserName_Year_Month` (`UserName`, `Year`, `Month`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
