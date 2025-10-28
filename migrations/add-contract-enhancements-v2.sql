-- Enhanced Contract System with Reservations
-- Adds support for Open/Closed contracts, item descriptions, and batch reservations

-- Add new fields to Contracts table
ALTER TABLE Contracts ADD COLUMN Type INTEGER NOT NULL DEFAULT 0;
ALTER TABLE Contracts ADD COLUMN ContractNumber TEXT;
ALTER TABLE Contracts ADD COLUMN ClientId INTEGER;
ALTER TABLE Contracts ADD COLUMN Description TEXT;
ALTER TABLE Contracts ADD COLUMN ShippedAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE Contracts ADD COLUMN CreatedBy TEXT;

-- Update existing OrgName to be nullable (since we now use ClientId)
-- Note: SQLite doesn't support ALTER COLUMN, so this is a reminder for future migrations

-- Add new fields to ContractItems table
ALTER TABLE ContractItems ADD COLUMN Description TEXT;
ALTER TABLE ContractItems ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;

-- Create ContractReservations table
CREATE TABLE IF NOT EXISTS ContractReservations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ContractItemId INTEGER NOT NULL,
    BatchId INTEGER NOT NULL,
    ReservedQty DECIMAL(18,3) NOT NULL,
    CreatedAt TEXT NOT NULL,
    ReturnedAt TEXT,
    FOREIGN KEY (ContractItemId) REFERENCES ContractItems(Id) ON DELETE CASCADE,
    FOREIGN KEY (BatchId) REFERENCES Batches(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_ContractReservations_ContractItemId ON ContractReservations(ContractItemId);
CREATE INDEX IF NOT EXISTS IX_ContractReservations_BatchId ON ContractReservations(BatchId);

-- Add indexes to Contracts
CREATE INDEX IF NOT EXISTS IX_Contracts_ClientId ON Contracts(ClientId);
CREATE INDEX IF NOT EXISTS IX_Contracts_Status ON Contracts(Status);
CREATE INDEX IF NOT EXISTS IX_Contracts_Type ON Contracts(Type);

-- Add indexes to ContractItems
CREATE INDEX IF NOT EXISTS IX_ContractItems_ProductId ON ContractItems(ProductId);
CREATE INDEX IF NOT EXISTS IX_ContractItems_Status ON ContractItems(Status);
