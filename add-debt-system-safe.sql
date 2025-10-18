-- =====================================================
-- МИГРАЦИЯ: СИСТЕМА ДОЛГОВ (БЕЗОПАСНАЯ ВЕРСИЯ)
-- =====================================================
-- Проверяет существование колонок перед добавлением
-- =====================================================

-- 1. СОЗДАНИЕ ТАБЛИЦЫ ТОВАРОВ В ДОЛГЕ (если не существует)
-- =====================================================
CREATE TABLE IF NOT EXISTS DebtItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    DebtId INT NOT NULL,
    ProductId INT NOT NULL,
    ProductName VARCHAR(500) NOT NULL,
    Sku VARCHAR(100) NULL,
    Qty DECIMAL(18,2) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedAt DATETIME(6) NULL,
    UpdatedBy VARCHAR(255) NULL,
    
    INDEX IX_DebtItems_DebtId (DebtId),
    INDEX IX_DebtItems_ProductId (ProductId),
    
    FOREIGN KEY (DebtId) REFERENCES Debts(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. ДОБАВЛЕНИЕ КОЛОНОК В ТАБЛИЦУ ДОЛГОВ (безопасно)
-- =====================================================

-- Проверяем и добавляем OriginalAmount
SET @dbname = DATABASE();
SET @tablename = "Debts";
SET @columnname = "OriginalAmount";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (table_name = @tablename)
      AND (table_schema = @dbname)
      AND (column_name = @columnname)
  ) > 0,
  "SELECT 1",
  "ALTER TABLE Debts ADD COLUMN OriginalAmount DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER Amount"
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- Проверяем и добавляем Notes
SET @columnname = "Notes";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (table_name = @tablename)
      AND (table_schema = @dbname)
      AND (column_name = @columnname)
  ) > 0,
  "SELECT 1",
  "ALTER TABLE Debts ADD COLUMN Notes TEXT NULL AFTER Status"
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- Проверяем и добавляем CreatedAt
SET @columnname = "CreatedAt";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (table_name = @tablename)
      AND (table_schema = @dbname)
      AND (column_name = @columnname)
  ) > 0,
  "SELECT 1",
  "ALTER TABLE Debts ADD COLUMN CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) AFTER Notes"
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- Проверяем и добавляем CreatedBy
SET @columnname = "CreatedBy";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (table_name = @tablename)
      AND (table_schema = @dbname)
      AND (column_name = @columnname)
  ) > 0,
  "SELECT 1",
  "ALTER TABLE Debts ADD COLUMN CreatedBy VARCHAR(255) NULL AFTER CreatedAt"
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- 3. ОБНОВЛЕНИЕ ДАННЫХ
-- =====================================================
-- Обновляем OriginalAmount для существующих долгов (если колонка была добавлена)
UPDATE Debts 
SET OriginalAmount = Amount 
WHERE OriginalAmount = 0 OR OriginalAmount IS NULL;

-- 4. ДОБАВЛЕНИЕ ИНДЕКСОВ
-- =====================================================
CREATE INDEX IF NOT EXISTS IX_Debts_ClientId_Status ON Debts(ClientId, Status);
CREATE INDEX IF NOT EXISTS IX_Debts_DueDate ON Debts(DueDate);
CREATE INDEX IF NOT EXISTS IX_Debts_CreatedAt ON Debts(CreatedAt);

-- 5. ПРОВЕРКА
-- =====================================================
SELECT '✅ МИГРАЦИЯ ЗАВЕРШЕНА!' AS Status;
SELECT 'Таблица DebtItems создана' AS Info;
SELECT 'Таблица Debts обновлена' AS Info;

SELECT 'Проверка структуры Debts:' AS Check;
DESCRIBE Debts;

SELECT 'Проверка структуры DebtItems:' AS Check;
DESCRIBE DebtItems;
