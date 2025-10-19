-- ===================================================================
-- СИСТЕМА БРАКА И ПЕРЕЗАРЯДКИ
-- ===================================================================
-- Назначение: Списание бракованных товаров и перезарядка огнетушителей
-- Автор: AI Assistant
-- Дата: 2025-10-19
-- ===================================================================

-- ===================================================================
-- 1. ТАБЛИЦА БРАКА (DEFECTIVE ITEMS)
-- ===================================================================

CREATE TABLE IF NOT EXISTS DefectiveItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT,
    Quantity INTEGER NOT NULL,
    Warehouse TEXT NOT NULL DEFAULT 'ND-40',
    Reason TEXT,
    Status INTEGER NOT NULL DEFAULT 0,  -- 0 = Active, 1 = Cancelled
    CreatedBy TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now')),
    CancelledBy TEXT,
    CancelledAt DATETIME,
    CancellationReason TEXT,
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS IX_DefectiveItems_ProductId 
ON DefectiveItems(ProductId);

CREATE INDEX IF NOT EXISTS IX_DefectiveItems_CreatedAt 
ON DefectiveItems(CreatedAt DESC);

CREATE INDEX IF NOT EXISTS IX_DefectiveItems_Status 
ON DefectiveItems(Status);

CREATE INDEX IF NOT EXISTS IX_DefectiveItems_Warehouse 
ON DefectiveItems(Warehouse);

-- ===================================================================
-- 2. ТАБЛИЦА ПЕРЕЗАРЯДОК (REFILL OPERATIONS)
-- ===================================================================

CREATE TABLE IF NOT EXISTS RefillOperations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT,
    Quantity INTEGER NOT NULL,
    Warehouse TEXT NOT NULL DEFAULT 'ND-40',
    CostPerUnit DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalCost DECIMAL(18,2) NOT NULL DEFAULT 0,
    Notes TEXT,
    Status INTEGER NOT NULL DEFAULT 0,  -- 0 = Active, 1 = Cancelled
    CreatedBy TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now')),
    CancelledBy TEXT,
    CancelledAt DATETIME,
    CancellationReason TEXT,
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS IX_RefillOperations_ProductId 
ON RefillOperations(ProductId);

CREATE INDEX IF NOT EXISTS IX_RefillOperations_CreatedAt 
ON RefillOperations(CreatedAt DESC);

CREATE INDEX IF NOT EXISTS IX_RefillOperations_Status 
ON RefillOperations(Status);

CREATE INDEX IF NOT EXISTS IX_RefillOperations_Warehouse 
ON RefillOperations(Warehouse);

-- ===================================================================
-- 3. ПРИМЕР ДАННЫХ (для тестирования)
-- ===================================================================

-- Пример брака
INSERT INTO DefectiveItems (ProductId, ProductName, Sku, Quantity, Warehouse, Reason, Status, CreatedBy, CreatedAt)
SELECT 
    1,
    'Огнетушитель ОП-4',
    'FIRE-001',
    5,
    'ND-40',
    'Повреждение корпуса при транспортировке',
    0,
    'manager1',
    datetime('now', '-7 days')
WHERE EXISTS (SELECT 1 FROM Products WHERE Id = 1);

-- Пример перезарядки
INSERT INTO RefillOperations (ProductId, ProductName, Sku, Quantity, Warehouse, CostPerUnit, TotalCost, Notes, Status, CreatedBy, CreatedAt)
SELECT 
    1,
    'Огнетушитель ОП-4',
    'FIRE-001',
    10,
    'ND-40',
    50000,
    500000,
    'Плановая перезарядка огнетушителей',
    0,
    'manager1',
    datetime('now', '-3 days')
WHERE EXISTS (SELECT 1 FROM Products WHERE Id = 1);

-- ===================================================================
-- 4. ПОЛЕЗНЫЕ ЗАПРОСЫ
-- ===================================================================

-- 1. Список брака за последний месяц
/*
SELECT 
    d.Id,
    d.ProductName,
    d.Quantity,
    d.Warehouse,
    d.Reason,
    CASE d.Status 
        WHEN 0 THEN 'Активный'
        WHEN 1 THEN 'Отменен'
    END as Status,
    d.CreatedBy,
    d.CreatedAt
FROM DefectiveItems d
WHERE d.CreatedAt >= datetime('now', '-30 days')
ORDER BY d.CreatedAt DESC;
*/

-- 2. Статистика брака по товарам
/*
SELECT 
    d.ProductName,
    d.Sku,
    COUNT(*) as DefectCount,
    SUM(d.Quantity) as TotalDefectiveQty,
    d.Warehouse
FROM DefectiveItems d
WHERE d.Status = 0
GROUP BY d.ProductId, d.Warehouse
ORDER BY TotalDefectiveQty DESC;
*/

-- 3. Список перезарядок с общей стоимостью
/*
SELECT 
    r.Id,
    r.ProductName,
    r.Quantity,
    r.CostPerUnit,
    r.TotalCost,
    r.Warehouse,
    r.Notes,
    r.CreatedBy,
    r.CreatedAt
FROM RefillOperations r
WHERE r.Status = 0
ORDER BY r.CreatedAt DESC;
*/

-- 4. Статистика перезарядок за период
/*
SELECT 
    COUNT(*) as TotalRefills,
    SUM(r.Quantity) as TotalQuantity,
    SUM(r.TotalCost) as TotalCost,
    AVG(r.CostPerUnit) as AvgCostPerUnit
FROM RefillOperations r
WHERE r.Status = 0
  AND r.CreatedAt >= datetime('now', '-30 days');
*/

-- 5. Самые проблемные товары (много брака)
/*
SELECT 
    d.ProductName,
    COUNT(*) as DefectiveRecords,
    SUM(d.Quantity) as TotalDefectiveQty,
    ROUND(CAST(SUM(d.Quantity) AS REAL) / COUNT(*), 2) as AvgQtyPerRecord
FROM DefectiveItems d
WHERE d.Status = 0
GROUP BY d.ProductId
HAVING COUNT(*) >= 2
ORDER BY TotalDefectiveQty DESC
LIMIT 10;
*/

-- 6. Расходы на перезарядку по месяцам
/*
SELECT 
    strftime('%Y-%m', r.CreatedAt) as Month,
    COUNT(*) as RefillCount,
    SUM(r.Quantity) as TotalQuantity,
    SUM(r.TotalCost) as TotalCost
FROM RefillOperations r
WHERE r.Status = 0
GROUP BY strftime('%Y-%m', r.CreatedAt)
ORDER BY Month DESC;
*/

-- ===================================================================
-- 5. КОММЕНТАРИИ И ЛОГИКА
-- ===================================================================

/*
БРАК (DEFECTIVE ITEMS):

1. СОЗДАНИЕ:
   - Менеджер указывает товар, количество, склад
   - Товар списывается со склада
   - Создается транзакция InventoryTransaction с типом Defective (12)
   - Записывается причина брака

2. ОТМЕНА:
   - Менеджер может отменить, если ошибка
   - Товар возвращается на склад
   - Создается транзакция DefectiveCancelled (13)
   - Записывается причина отмены

3. СТАТУС:
   - Active (0) - бракованный товар списан
   - Cancelled (1) - списание отменено

ПЕРЕЗАРЯДКА (REFILL OPERATIONS):

1. СОЗДАНИЕ:
   - Менеджер указывает товар (огнетушитель), количество
   - Указывается стоимость перезарядки за единицу
   - Общая стоимость = Quantity * CostPerUnit
   - НЕ влияет на остатки (товар уже на складе)
   - Создается транзакция Refill (14) для истории

2. ОТМЕНА:
   - Можно отменить, если ошибка
   - Стоимость не возвращается (уже потрачена)
   - Только для корректности учета

3. СТАТУС:
   - Active (0) - перезарядка выполнена
   - Cancelled (1) - запись отменена

РАЗНИЦА:
- БРАК списывает товар со склада (уменьшает остатки)
- ПЕРЕЗАРЯДКА не трогает остатки (товар остается на складе, просто перезарядили)
*/

-- ===================================================================
-- 6. ПРАВА ДОСТУПА
-- ===================================================================

/*
MANAGER:
- Может создавать брак
- Может создавать перезарядки
- Может отменять свои записи
- Может просматривать историю

ADMIN/OWNER:
- Полный доступ
- Может отменять любые записи
- Может видеть статистику
- Может экспортировать отчеты
*/
