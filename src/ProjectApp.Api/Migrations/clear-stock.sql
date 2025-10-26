-- Очистка склада (MySQL)
-- Удаляет все партии и транзакции

-- Удаляем все транзакции
DELETE FROM InventoryTransactions;
SELECT 'InventoryTransactions cleared' AS message;

-- Удаляем все партии
DELETE FROM Batches;
SELECT 'Batches cleared' AS message;

-- Удаляем все товары из поставок
DELETE FROM SupplyItems;
SELECT 'SupplyItems cleared' AS message;

SELECT 'Stock cleared successfully!' AS message;
