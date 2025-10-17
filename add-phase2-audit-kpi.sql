-- =====================================================
-- ФАЗА 2: Аудит и контроль
-- Добавление таблицы AuditLogs для логирования действий
-- =====================================================

USE railway;

-- Таблица аудит-логов
CREATE TABLE IF NOT EXISTS `AuditLogs` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `UserName` VARCHAR(128) NOT NULL,
    `Action` VARCHAR(64) NOT NULL,
    `EntityType` VARCHAR(64) NOT NULL,
    `EntityId` INT NULL,
    `OldValue` TEXT NULL,
    `NewValue` TEXT NULL,
    `IpAddress` VARCHAR(64) NULL,
    `UserAgent` VARCHAR(512) NULL,
    `CreatedAt` DATETIME(6) NOT NULL,
    `Details` TEXT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_AuditLogs_UserName` (`UserName` ASC),
    INDEX `IX_AuditLogs_CreatedAt` (`CreatedAt` ASC),
    INDEX `IX_AuditLogs_EntityType_EntityId` (`EntityType` ASC, `EntityId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SELECT 'Таблица AuditLogs создана' AS Status;

-- Проверка
SELECT COUNT(*) AS AuditLogsCount FROM `AuditLogs`;
