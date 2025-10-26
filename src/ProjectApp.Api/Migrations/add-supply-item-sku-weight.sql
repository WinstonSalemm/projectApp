-- Добавление полей Sku и Weight в таблицу SupplyItems
-- Дата: 2025-01-26

-- Добавляем поле Sku
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SupplyItems]') AND name = 'Sku')
BEGIN
    ALTER TABLE SupplyItems ADD Sku NVARCHAR(200) NOT NULL DEFAULT '';
    PRINT 'Column Sku added to SupplyItems';
END
ELSE
BEGIN
    PRINT 'Column Sku already exists in SupplyItems';
END
GO

-- Добавляем поле Weight
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SupplyItems]') AND name = 'Weight')
BEGIN
    ALTER TABLE SupplyItems ADD Weight DECIMAL(18, 4) NOT NULL DEFAULT 0;
    PRINT 'Column Weight added to SupplyItems';
END
ELSE
BEGIN
    PRINT 'Column Weight already exists in SupplyItems';
END
GO

PRINT 'Migration add-supply-item-sku-weight.sql completed successfully!';
GO
