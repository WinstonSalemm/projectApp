-- Миграция: Типы клиентов и автоматическая классификация
-- Дата: 2025-10-17

-- Добавляем новые поля в таблицу Clients
ALTER TABLE `Clients`
  ADD COLUMN IF NOT EXISTS `TotalPurchases` DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER `CreatedAt`,
  ADD COLUMN IF NOT EXISTS `PurchasesCount` INT NOT NULL DEFAULT 0 AFTER `TotalPurchases`,
  ADD COLUMN IF NOT EXISTS `LastPurchaseDate` DATETIME(6) NULL AFTER `PurchasesCount`,
  ADD COLUMN IF NOT EXISTS `TypeAssignedAt` DATETIME(6) NULL AFTER `LastPurchaseDate`;

-- Создаем индексы для оптимизации запросов
CREATE INDEX IF NOT EXISTS `IX_Clients_Type` ON `Clients` (`Type`);
CREATE INDEX IF NOT EXISTS `IX_Clients_TotalPurchases` ON `Clients` (`TotalPurchases`);
CREATE INDEX IF NOT EXISTS `IX_Clients_LastPurchaseDate` ON `Clients` (`LastPurchaseDate`);

-- Пересчитываем статистику для существующих клиентов
UPDATE `Clients` c
SET 
  c.TotalPurchases = COALESCE((
    SELECT SUM(s.Total)
    FROM `Sales` s
    WHERE s.ClientId = c.Id
  ), 0),
  c.PurchasesCount = COALESCE((
    SELECT COUNT(*)
    FROM `Sales` s
    WHERE s.ClientId = c.Id
  ), 0),
  c.LastPurchaseDate = (
    SELECT MAX(s.CreatedAt)
    FROM `Sales` s
    WHERE s.ClientId = c.Id
  );

-- Автоматическая классификация существующих клиентов
UPDATE `Clients`
SET 
  `Type` = CASE
    WHEN `TotalPurchases` >= 50000000 THEN 5  -- LargeWholesale
    WHEN `TotalPurchases` >= 10000000 THEN 4  -- Wholesale
    WHEN `TotalPurchases` > 0 THEN 3          -- Retail
    ELSE `Type`  -- Оставляем текущий тип если нет покупок
  END,
  `TypeAssignedAt` = NOW()
WHERE `TotalPurchases` > 0;
