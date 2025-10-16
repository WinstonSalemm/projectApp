-- Добавляем колонку Cost в таблицу Products
ALTER TABLE Products ADD COLUMN Cost DECIMAL(18,2) NOT NULL DEFAULT 0.00 COMMENT 'Себестоимость товара';
