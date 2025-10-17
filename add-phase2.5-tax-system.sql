-- =====================================================
-- ФАЗА 2.5: Налоговый учет и аналитика
-- Система расчета налогов по законодательству Узбекистана
-- =====================================================

USE railway;

-- Таблица налоговых записей
CREATE TABLE IF NOT EXISTS `TaxRecords` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Type` INT NOT NULL COMMENT '1=НДС, 2=Налог на прибыль, 3=Соц.налог, 4=Имущество, 5=Земля, 6=Вода, 7=ИНПС, 8=Школьный фонд, 9=Упрощенка',
    `Period` DATETIME(6) NOT NULL COMMENT 'Налоговый период',
    `TaxBase` DECIMAL(18,2) NOT NULL COMMENT 'Налоговая база',
    `TaxRate` DECIMAL(5,2) NOT NULL COMMENT 'Ставка налога %',
    `TaxAmount` DECIMAL(18,2) NOT NULL COMMENT 'Сумма налога',
    `IsPaid` BOOLEAN NOT NULL DEFAULT FALSE,
    `PaidAt` DATETIME(6) NULL,
    `DueDate` DATETIME(6) NOT NULL COMMENT 'Срок уплаты',
    `CalculatedAt` DATETIME(6) NOT NULL,
    `Note` TEXT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_TaxRecords_Period` (`Period` ASC),
    INDEX `IX_TaxRecords_Type` (`Type` ASC),
    INDEX `IX_TaxRecords_DueDate` (`DueDate` ASC),
    INDEX `IX_TaxRecords_IsPaid` (`IsPaid` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Налоговые записи - история начислений и уплат';

-- Таблица налоговых настроек
CREATE TABLE IF NOT EXISTS `TaxSettings` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `System` INT NOT NULL DEFAULT 1 COMMENT '1=Общая система, 2=Упрощенная',
    `CompanyINN` VARCHAR(128) NULL COMMENT 'ИНН компании',
    `CompanyName` VARCHAR(256) NULL COMMENT 'Название компании',
    `VATRate` DECIMAL(5,2) NOT NULL DEFAULT 12.00 COMMENT 'Ставка НДС %',
    `IncomeTaxRate` DECIMAL(5,2) NOT NULL DEFAULT 15.00 COMMENT 'Ставка налога на прибыль %',
    `SocialTaxRate` DECIMAL(5,2) NOT NULL DEFAULT 12.00 COMMENT 'Ставка ЕСП %',
    `INPSRate` DECIMAL(5,2) NOT NULL DEFAULT 0.20 COMMENT 'Ставка ИНПС %',
    `SchoolFundRate` DECIMAL(5,2) NOT NULL DEFAULT 1.50 COMMENT 'Ставка школьного фонда %',
    `SimplifiedTaxRate` DECIMAL(5,2) NOT NULL DEFAULT 4.00 COMMENT 'Ставка упрощенного налога %',
    `IsVATRegistered` BOOLEAN NOT NULL DEFAULT TRUE COMMENT 'Плательщик НДС',
    `UpdatedAt` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Налоговые настройки компании';

-- Вставляем дефолтные настройки
INSERT INTO `TaxSettings` (
    `System`, 
    `VATRate`, 
    `IncomeTaxRate`, 
    `SocialTaxRate`, 
    `INPSRate`, 
    `SchoolFundRate`, 
    `SimplifiedTaxRate`,
    `IsVATRegistered`,
    `UpdatedAt`
) VALUES (
    1,      -- Общая система
    12.00,  -- НДС 12%
    15.00,  -- Налог на прибыль 15%
    12.00,  -- Социальный налог 12%
    0.20,   -- ИНПС 0.2%
    1.50,   -- Школьный фонд 1.5%
    4.00,   -- Упрощенка 4% (если переключатся)
    TRUE,   -- Плательщик НДС
    UTC_TIMESTAMP()
) ON DUPLICATE KEY UPDATE 
    `UpdatedAt` = UTC_TIMESTAMP();

SELECT '✅ Налоговая система создана!' AS Status;

-- Проверка
SELECT 
    'TaxRecords' AS TableName,
    COUNT(*) AS RecordsCount
FROM `TaxRecords`
UNION ALL
SELECT 
    'TaxSettings' AS TableName,
    COUNT(*) AS RecordsCount
FROM `TaxSettings`;

-- Справочная информация о налогах в Узбекистане (2025)
SELECT '
╔════════════════════════════════════════════════════════════════════════════╗
║  НАЛОГОВАЯ СИСТЕМА УЗБЕКИСТАНА 2025                                         ║
╠════════════════════════════════════════════════════════════════════════════╣
║  1. НДС (Налог на добавленную стоимость)                                   ║
║     • Ставка: 12%                                                           ║
║     • Срок уплаты: до 20 числа следующего месяца                           ║
║     • Декларация: ежемесячно                                                ║
║                                                                              ║
║  2. Налог на прибыль                                                        ║
║     • Ставка: 15%                                                           ║
║     • Срок уплаты: до 25 числа следующего месяца (авансовый платеж)       ║
║     • Декларация: ежеквартально                                             ║
║                                                                              ║
║  3. Единый социальный платеж (ЕСП)                                         ║
║     • Ставка: 12% от ФОТ                                                    ║
║     • Срок уплаты: до 15 числа следующего месяца                           ║
║                                                                              ║
║  4. ИНПС (обязательные страховые взносы)                                   ║
║     • Ставка: 0.2% от ФОТ                                                   ║
║     • Срок уплаты: до 15 числа следующего месяца                           ║
║                                                                              ║
║  5. Отчисления в Школьный фонд                                              ║
║     • Ставка: 1.5% от ФОТ                                                   ║
║     • Срок уплаты: до 15 числа следующего месяца                           ║
║                                                                              ║
║  6. Упрощенная система налогообложения (для малого бизнеса)                ║
║     • Ставка: 4-7.5% от выручки                                            ║
║     • Освобождение от НДС и налога на прибыль                              ║
║                                                                              ║
║  ВАЖНО: Сроки и ставки актуальны на 2025 год                               ║
╚════════════════════════════════════════════════════════════════════════════╝
' AS TaxInfo;
