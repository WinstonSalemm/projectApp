-- ===================================
-- FIX: Add ALL missing columns to Batches
-- ===================================

-- Financial analytics columns
ALTER TABLE Batches ADD COLUMN SupplierName VARCHAR(255) NULL;
ALTER TABLE Batches ADD COLUMN InvoiceNumber VARCHAR(100) NULL;
ALTER TABLE Batches ADD COLUMN PurchaseDate DATETIME(6) NULL;
ALTER TABLE Batches ADD COLUMN VatRate DECIMAL(5,2) NULL;
ALTER TABLE Batches ADD COLUMN PurchaseSource VARCHAR(100) NULL;

-- Customs and archiving
ALTER TABLE Batches ADD COLUMN GtdCode VARCHAR(64) NULL;
ALTER TABLE Batches ADD COLUMN ArchivedAt DATETIME(6) NULL;

SELECT 'All missing columns added to Batches table' AS Result;

DESCRIBE Batches;
