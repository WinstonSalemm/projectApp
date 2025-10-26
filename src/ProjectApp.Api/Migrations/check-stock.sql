-- Проверка что на складе

-- Проверяем Batches
SELECT COUNT(*) as batches_count FROM Batches;
SELECT * FROM Batches LIMIT 10;

-- Проверяем SupplyItems
SELECT COUNT(*) as items_count FROM SupplyItems;
SELECT * FROM SupplyItems LIMIT 10;

-- Проверяем Stock (если есть такая таблица)
SELECT COUNT(*) as stock_count FROM Stock;
SELECT * FROM Stock LIMIT 10;
