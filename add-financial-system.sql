-- Миграция: Финансовая система (Кассы, Транзакции, Операционные расходы)
-- Дата: 2025-01-18
-- Описание: Добавление полноценной финансовой системы для 100% прозрачности бизнеса

-- ===== ТАБЛИЦА 1: Кассы (Cashboxes) =====
CREATE TABLE IF NOT EXISTS `Cashboxes` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(128) NOT NULL COMMENT 'Название кассы',
  `Type` INT NOT NULL DEFAULT 0 COMMENT '0=Office, 1=Warehouse, 2=Manager, 3=BankAccount, 4=CryptoWallet',
  `Currency` VARCHAR(10) NOT NULL DEFAULT 'UZS' COMMENT 'Валюта',
  `CurrentBalance` DECIMAL(18,2) NOT NULL DEFAULT 0 COMMENT 'Текущий баланс',
  `ResponsibleUser` VARCHAR(64) NULL COMMENT 'Ответственное лицо (Username)',
  `IsActive` TINYINT(1) NOT NULL DEFAULT 1 COMMENT 'Активна ли касса',
  `Description` VARCHAR(512) NULL COMMENT 'Описание',
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` DATETIME(6) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_Cashboxes_Type` (`Type`),
  INDEX `IX_Cashboxes_IsActive` (`IsActive`),
  INDEX `IX_Cashboxes_ResponsibleUser` (`ResponsibleUser`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Кассы и счета компании';

-- ===== ТАБЛИЦА 2: Транзакции между кассами (CashTransactions) =====
CREATE TABLE IF NOT EXISTS `CashTransactions` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Type` INT NOT NULL DEFAULT 0 COMMENT '0=Income, 1=Expense, 2=Transfer, 3=SalePayment, 4=Withdrawal',
  `FromCashboxId` INT NULL COMMENT 'Касса-источник (откуда)',
  `ToCashboxId` INT NULL COMMENT 'Касса-назначение (куда)',
  `Amount` DECIMAL(18,2) NOT NULL COMMENT 'Сумма',
  `Currency` VARCHAR(10) NOT NULL DEFAULT 'UZS',
  `Category` VARCHAR(64) NULL COMMENT 'Категория транзакции',
  `Description` VARCHAR(512) NOT NULL COMMENT 'Описание/назначение',
  `LinkedSaleId` INT NULL COMMENT 'Связь с продажей',
  `LinkedPurchaseId` INT NULL COMMENT 'Связь с закупкой',
  `LinkedExpenseId` INT NULL COMMENT 'Связь с расходом',
  `CreatedBy` VARCHAR(64) NOT NULL COMMENT 'Кто создал',
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `Status` INT NOT NULL DEFAULT 1 COMMENT '0=Pending, 1=Completed, 2=Cancelled',
  PRIMARY KEY (`Id`),
  INDEX `IX_CashTransactions_Type` (`Type`),
  INDEX `IX_CashTransactions_FromCashboxId` (`FromCashboxId`),
  INDEX `IX_CashTransactions_ToCashboxId` (`ToCashboxId`),
  INDEX `IX_CashTransactions_CreatedAt` (`CreatedAt`),
  INDEX `IX_CashTransactions_LinkedSaleId` (`LinkedSaleId`),
  INDEX `IX_CashTransactions_LinkedPurchaseId` (`LinkedPurchaseId`),
  INDEX `IX_CashTransactions_LinkedExpenseId` (`LinkedExpenseId`),
  CONSTRAINT `FK_CashTransactions_FromCashbox` FOREIGN KEY (`FromCashboxId`) REFERENCES `Cashboxes` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_CashTransactions_ToCashbox` FOREIGN KEY (`ToCashboxId`) REFERENCES `Cashboxes` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Движение денежных средств';

-- ===== ТАБЛИЦА 3: Операционные расходы (OperatingExpenses) =====
CREATE TABLE IF NOT EXISTS `OperatingExpenses` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Type` INT NOT NULL COMMENT '0=Salary, 1=Rent, 2=Utilities, 3=Transport, 4=Customs, 5=Logistics, 6=Marketing, 7=Taxes, 8=Insurance, 9=Maintenance, 10=Office, 11=Communication, 12=Banking, 13=Legal, 99=Other',
  `Amount` DECIMAL(18,2) NOT NULL COMMENT 'Сумма расхода',
  `Currency` VARCHAR(10) NOT NULL DEFAULT 'UZS',
  `ExpenseDate` DATETIME(6) NOT NULL COMMENT 'Дата расхода',
  `Description` VARCHAR(512) NOT NULL COMMENT 'Описание',
  `Category` VARCHAR(64) NULL COMMENT 'Категория (для группировки)',
  `IsRecurring` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Регулярный расход',
  `RecurringPeriod` INT NULL COMMENT '0=Daily, 1=Weekly, 2=Monthly, 3=Quarterly, 4=Yearly',
  `CashboxId` INT NULL COMMENT 'Касса, из которой оплачено',
  `Recipient` VARCHAR(128) NULL COMMENT 'Получатель платежа',
  `CreatedBy` VARCHAR(64) NOT NULL COMMENT 'Кто создал',
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `PaymentStatus` INT NOT NULL DEFAULT 1 COMMENT '0=Pending, 1=Paid, 2=Overdue',
  `PaidAt` DATETIME(6) NULL COMMENT 'Дата оплаты',
  PRIMARY KEY (`Id`),
  INDEX `IX_OperatingExpenses_Type` (`Type`),
  INDEX `IX_OperatingExpenses_ExpenseDate` (`ExpenseDate`),
  INDEX `IX_OperatingExpenses_PaymentStatus` (`PaymentStatus`),
  INDEX `IX_OperatingExpenses_CashboxId` (`CashboxId`),
  INDEX `IX_OperatingExpenses_IsRecurring` (`IsRecurring`),
  CONSTRAINT `FK_OperatingExpenses_Cashbox` FOREIGN KEY (`CashboxId`) REFERENCES `Cashboxes` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Операционные расходы бизнеса';

-- ===== НАЧАЛЬНЫЕ ДАННЫЕ: Создаем основные кассы =====
INSERT INTO `Cashboxes` (`Name`, `Type`, `Currency`, `CurrentBalance`, `IsActive`, `Description`)
VALUES 
  ('Офис - Главная касса', 0, 'UZS', 0, 1, 'Основная касса в офисе'),
  ('Склад ND-40', 1, 'UZS', 0, 1, 'Касса на складе ND-40 (неоприходованные товары)'),
  ('Склад IM-40', 1, 'UZS', 0, 1, 'Касса на складе IM-40 (оприходованные товары)'),
  ('Банковский счет - Основной', 3, 'UZS', 0, 1, 'Основной расчетный счет компании'),
  ('Банковский счет - Валютный (USD)', 3, 'USD', 0, 1, 'Валютный счет для международных платежей')
ON DUPLICATE KEY UPDATE `Name` = VALUES(`Name`);

-- ===== ПРОВЕРКА =====
SELECT 'Миграция финансовой системы выполнена успешно!' AS Status;
SELECT 'Создано таблиц: 3 (Cashboxes, CashTransactions, OperatingExpenses)' AS Info;
SELECT COUNT(*) AS InitialCashboxes FROM `Cashboxes`;
