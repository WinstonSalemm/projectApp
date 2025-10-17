-- Миграция: Коммерческая аналитика и система акций
-- Дата: 2025-10-17

-- Создаем таблицу акций/скидок
CREATE TABLE IF NOT EXISTS `Promotions` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(256) NOT NULL,
  `Description` TEXT NOT NULL,
  `StartDate` DATETIME(6) NOT NULL,
  `EndDate` DATETIME(6) NOT NULL,
  `Type` INT NOT NULL,
  `DiscountPercent` DECIMAL(5,2) NOT NULL DEFAULT 0,
  `DiscountAmount` DECIMAL(18,2) NULL,
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
  `CreatedAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NOT NULL,
  `Note` VARCHAR(512) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_Promotions_IsActive` (`IsActive`),
  INDEX `IX_Promotions_StartDate_EndDate` (`StartDate`, `EndDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Создаем таблицу товаров в акциях
CREATE TABLE IF NOT EXISTS `PromotionItems` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `PromotionId` INT NOT NULL,
  `ProductId` INT NOT NULL,
  `CustomDiscountPercent` DECIMAL(5,2) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_PromotionItems_PromotionId` (`PromotionId`),
  INDEX `IX_PromotionItems_ProductId` (`ProductId`),
  CONSTRAINT `FK_PromotionItems_Promotions` 
    FOREIGN KEY (`PromotionId`) REFERENCES `Promotions` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_PromotionItems_Products` 
    FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
