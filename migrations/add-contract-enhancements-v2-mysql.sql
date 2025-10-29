-- Enhanced Contract System with Reservations (MySQL version)
-- Adds support for Open/Closed contracts, item descriptions, and batch reservations

-- Add new fields to Contracts table
ALTER TABLE `Contracts` ADD COLUMN `Type` INT NOT NULL DEFAULT 0;
ALTER TABLE `Contracts` ADD COLUMN `ContractNumber` VARCHAR(64) NULL;
ALTER TABLE `Contracts` ADD COLUMN `ClientId` INT NULL;
ALTER TABLE `Contracts` ADD COLUMN `Description` TEXT NULL;
ALTER TABLE `Contracts` ADD COLUMN `ShippedAmount` DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE `Contracts` ADD COLUMN `CreatedBy` VARCHAR(64) NULL;
ALTER TABLE `Contracts` ADD COLUMN `TotalAmount` DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE `Contracts` ADD COLUMN `TotalItemsCount` INT NOT NULL DEFAULT 0;
ALTER TABLE `Contracts` ADD COLUMN `PaidAmount` DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE `Contracts` ADD COLUMN `DeliveredItemsCount` INT NOT NULL DEFAULT 0;

-- Add new fields to ContractItems table
ALTER TABLE `ContractItems` ADD COLUMN `Sku` VARCHAR(64) NULL;
ALTER TABLE `ContractItems` ADD COLUMN `Description` TEXT NULL;
ALTER TABLE `ContractItems` ADD COLUMN `Status` INT NOT NULL DEFAULT 0;
ALTER TABLE `ContractItems` ADD COLUMN `DeliveredQty` DECIMAL(18,3) NOT NULL DEFAULT 0;
ALTER TABLE `ContractItems` ADD COLUMN `ShippedQty` DECIMAL(18,3) NOT NULL DEFAULT 0;

-- Create ContractReservations table
CREATE TABLE IF NOT EXISTS `ContractReservations` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `ContractItemId` INT NOT NULL,
    `BatchId` INT NOT NULL,
    `ReservedQty` DECIMAL(18,3) NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL,
    `ReturnedAt` DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_ContractReservations_ContractItemId` (`ContractItemId` ASC),
    INDEX `IX_ContractReservations_BatchId` (`BatchId` ASC),
    CONSTRAINT `FK_ContractReservations_ContractItems` FOREIGN KEY (`ContractItemId`) REFERENCES `ContractItems` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ContractReservations_Batches` FOREIGN KEY (`BatchId`) REFERENCES `Batches` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Add indexes to Contracts
CREATE INDEX `IX_Contracts_ClientId` ON `Contracts`(`ClientId` ASC);
CREATE INDEX `IX_Contracts_Status` ON `Contracts`(`Status` ASC);
CREATE INDEX `IX_Contracts_Type` ON `Contracts`(`Type` ASC);

-- Add indexes to ContractItems
CREATE INDEX `IX_ContractItems_ProductId` ON `ContractItems`(`ProductId` ASC);
CREATE INDEX `IX_ContractItems_Status` ON `ContractItems`(`Status` ASC);
