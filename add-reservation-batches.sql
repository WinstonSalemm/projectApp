-- Миграция: Система бронирования со складом
-- Дата: 2025-10-17

-- Создаем таблицу для отслеживания партий в бронировании
CREATE TABLE IF NOT EXISTS `ReservationItemBatches` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ReservationItemId` INT NOT NULL,
  `BatchId` INT NOT NULL,
  `RegisterAtReservation` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitCost` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ReservationItemBatches_ReservationItemId` (`ReservationItemId`),
  INDEX `IX_ReservationItemBatches_BatchId` (`BatchId`),
  CONSTRAINT `FK_ReservationItemBatches_ReservationItems` 
    FOREIGN KEY (`ReservationItemId`) REFERENCES `ReservationItems` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ReservationItemBatches_Batches` 
    FOREIGN KEY (`BatchId`) REFERENCES `Batches` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
