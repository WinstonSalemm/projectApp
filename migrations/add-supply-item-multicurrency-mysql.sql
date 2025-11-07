-- Добавление недостающих колонок PriceUsd и PriceUzs в SupplyItems (MySQL)
-- Идемпотентно: проверяет наличие колонок и добавляет только при отсутствии

SET @dbname = DATABASE();
SET @tablename = 'SupplyItems';

-- Добавляем колонку Sku (на случай, если предыдущая миграция не применена)
SET @columnname = 'Sku';
SET @stmt = (
  SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
    'SELECT "Column Sku already exists" AS message;',
    'ALTER TABLE `SupplyItems` ADD COLUMN `Sku` VARCHAR(200) NOT NULL DEFAULT "" AFTER `Name`;' 
  )
);
PREPARE alterIfNotExists FROM @stmt; EXECUTE alterIfNotExists; DEALLOCATE PREPARE alterIfNotExists;

-- Добавляем колонку Weight (на случай, если предыдущая миграция не применена)
SET @columnname = 'Weight';
SET @stmt = (
  SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
    'SELECT "Column Weight already exists" AS message;',
    'ALTER TABLE `SupplyItems` ADD COLUMN `Weight` DECIMAL(18,4) NOT NULL DEFAULT 0 AFTER `PriceRub`;' 
  )
);
PREPARE alterIfNotExists FROM @stmt; EXECUTE alterIfNotExists; DEALLOCATE PREPARE alterIfNotExists;

-- Добавляем колонку PriceUsd (NULLABLE)
SET @columnname = 'PriceUsd';
SET @stmt = (
  SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
    'SELECT "Column PriceUsd already exists" AS message;',
    'ALTER TABLE `SupplyItems` ADD COLUMN `PriceUsd` DECIMAL(18,4) NULL AFTER `PriceRub`;' 
  )
);
PREPARE alterIfNotExists FROM @stmt; EXECUTE alterIfNotExists; DEALLOCATE PREPARE alterIfNotExists;

-- Добавляем колонку PriceUzs (NULLABLE)
SET @columnname = 'PriceUzs';
SET @stmt = (
  SELECT IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @columnname) > 0,
    'SELECT "Column PriceUzs already exists" AS message;',
    'ALTER TABLE `SupplyItems` ADD COLUMN `PriceUzs` DECIMAL(18,4) NULL AFTER `PriceUsd`;' 
  )
);
PREPARE alterIfNotExists FROM @stmt; EXECUTE alterIfNotExists; DEALLOCATE PREPARE alterIfNotExists;

SELECT 'Migration add-supply-item-multicurrency-mysql.sql completed successfully!' AS message;
