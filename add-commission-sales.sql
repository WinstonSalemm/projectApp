-- =========================================
-- МИГРАЦИЯ: Партнерская программа (комиссионные клиенты)
-- Дата: 18.10.2025
-- =========================================

USE railway;

-- 1. Добавляем поля партнера в таблицу Clients
ALTER TABLE Clients 
ADD COLUMN IsCommissionAgent BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'Является ли клиент партнером',
ADD COLUMN CommissionBalance DECIMAL(18,2) NOT NULL DEFAULT 0 COMMENT 'Текущий баланс комиссии',
ADD COLUMN CommissionAgentSince DATETIME(6) NULL COMMENT 'Дата становления партнером',
ADD COLUMN CommissionNotes TEXT NULL COMMENT 'Примечания по партнерству';

-- 2. Добавляем поля комиссии в таблицу Sales
ALTER TABLE Sales 
ADD COLUMN CommissionAgentId INT NULL COMMENT 'ID клиента-партнера',
ADD COLUMN CommissionRate DECIMAL(5,2) NULL COMMENT 'Процент комиссии партнеру',
ADD COLUMN CommissionAmount DECIMAL(18,2) NULL COMMENT 'Сумма комиссии партнеру';

-- 3. Добавляем поля комиссии в таблицу Contracts
ALTER TABLE Contracts
ADD COLUMN CommissionAgentId INT NULL COMMENT 'ID клиента-партнера',
ADD COLUMN CommissionAmount DECIMAL(18,2) NULL COMMENT 'Сумма комиссии партнеру';

-- 4. Создаем таблицу CommissionTransactions (все операции по комиссиям)
CREATE TABLE IF NOT EXISTS CommissionTransactions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CommissionAgentId INT NOT NULL COMMENT 'ID клиента-партнера',
    Type INT NOT NULL COMMENT '0=Accrual, 1=ContractAccrual, 2=PaymentCash, 3=PaymentCard, 4=PaymentProduct, 5=Adjustment',
    Amount DECIMAL(18,2) NOT NULL COMMENT 'Сумма транзакции (+начисление/-выплата)',
    BalanceAfter DECIMAL(18,2) NOT NULL COMMENT 'Баланс после транзакции',
    RelatedSaleId INT NULL COMMENT 'ID связанной продажи',
    RelatedContractId INT NULL COMMENT 'ID связанного договора',
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CreatedBy VARCHAR(255) NULL COMMENT 'Кто создал транзакцию',
    Notes TEXT NULL COMMENT 'Примечания',
    
    INDEX IX_CommissionTransactions_AgentId (CommissionAgentId),
    INDEX IX_CommissionTransactions_Type (Type),
    INDEX IX_CommissionTransactions_SaleId (RelatedSaleId),
    INDEX IX_CommissionTransactions_ContractId (RelatedContractId),
    INDEX IX_CommissionTransactions_CreatedAt (CreatedAt),
    
    FOREIGN KEY (CommissionAgentId) REFERENCES Clients(Id) ON DELETE CASCADE,
    FOREIGN KEY (RelatedSaleId) REFERENCES Sales(Id) ON DELETE SET NULL,
    FOREIGN KEY (RelatedContractId) REFERENCES Contracts(Id) ON DELETE SET NULL
);

-- 5. Добавляем индексы для быстрого поиска
CREATE INDEX IX_Sales_CommissionAgentId ON Sales(CommissionAgentId);
CREATE INDEX IX_Contracts_CommissionAgentId ON Contracts(CommissionAgentId);
CREATE INDEX IX_Clients_IsCommissionAgent ON Clients(IsCommissionAgent);

-- 6. Обновляем существующие данные
UPDATE Clients SET IsCommissionAgent = FALSE WHERE IsCommissionAgent IS NULL;
UPDATE Clients SET CommissionBalance = 0 WHERE CommissionBalance IS NULL;

SELECT 'Миграция партнерской программы завершена!' AS Status;
SELECT COUNT(*) AS TotalClients FROM Clients;
SELECT COUNT(*) AS PartnerClients FROM Clients WHERE IsCommissionAgent = TRUE;
