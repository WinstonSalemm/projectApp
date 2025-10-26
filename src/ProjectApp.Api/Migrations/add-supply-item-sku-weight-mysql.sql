-- Добавление полей Sku и Weight в таблицу SupplyItems (MySQL)
-- Дата: 2025-01-26

-- Добавляем поле Sku (если не существует)
SET @dbname = DATABASE();
SET @tablename = 'SupplyItems';
SET @columnname = 'Sku';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE 
      TABLE_SCHEMA = @dbname
      AND TABLE_NAME = @tablename
      AND COLUMN_NAME = @columnname
  ) > 0,
  'SELECT ''Column Sku already exists'' AS message;',
  'ALTER TABLE SupplyItems ADD COLUMN Sku VARCHAR(200) NOT NULL DEFAULT '''';'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- Добавляем поле Weight (если не существует)
SET @columnname = 'Weight';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE 
      TABLE_SCHEMA = @dbname
      AND TABLE_NAME = @tablename
      AND COLUMN_NAME = @columnname
  ) > 0,
  'SELECT ''Column Weight already exists'' AS message;',
  'ALTER TABLE SupplyItems ADD COLUMN Weight DECIMAL(18, 4) NOT NULL DEFAULT 0;'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SELECT 'Migration add-supply-item-sku-weight-mysql.sql completed successfully!' AS message;
