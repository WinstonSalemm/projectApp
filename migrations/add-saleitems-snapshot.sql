-- Add Sku and ProductName to SaleItems for snapshot at time of sale
-- This fixes the issue where product names are missing in returns if product is deleted

-- Add columns
ALTER TABLE SaleItems ADD COLUMN Sku TEXT NULL;
ALTER TABLE SaleItems ADD COLUMN ProductName TEXT NULL;

-- Fill existing data from Products table
UPDATE SaleItems 
SET Sku = (SELECT Sku FROM Products WHERE Products.Id = SaleItems.ProductId),
    ProductName = (SELECT Name FROM Products WHERE Products.Id = SaleItems.ProductId);

-- Create index for faster queries
CREATE INDEX IF NOT EXISTS IX_SaleItems_Sku ON SaleItems(Sku);
