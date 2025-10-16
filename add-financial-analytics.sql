-- Добавляем тип продажи в Sales
ALTER TABLE Sales ADD COLUMN Category INT NOT NULL DEFAULT 0 COMMENT 'SaleCategory: 0=White, 1=Grey, 2=Black';

-- Таблица расходов
CREATE TABLE IF NOT EXISTS Expenses (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Category INT NOT NULL COMMENT 'ExpenseCategory',
    Description VARCHAR(500) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Date DATETIME NOT NULL,
    CreatedBy VARCHAR(100),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_expenses_date (Date),
    INDEX idx_expenses_category (Category)
);

-- Таблица движения денежных средств
CREATE TABLE IF NOT EXISTS CashFlows (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Type INT NOT NULL COMMENT 'CashFlowType: 0=Income, 1=Expense',
    Amount DECIMAL(18,2) NOT NULL,
    Description VARCHAR(500) NOT NULL,
    Date DATETIME NOT NULL,
    CreatedBy VARCHAR(100),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_cashflows_date (Date),
    INDEX idx_cashflows_type (Type)
);

-- Таблица кредиторской задолженности
CREATE TABLE IF NOT EXISTS Liabilities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Type INT NOT NULL COMMENT 'LiabilityType: 0=Supplier, 1=Loan, 2=Tax, 3=Other',
    Description VARCHAR(500) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DueDate DATETIME NOT NULL,
    IsPaid BOOLEAN NOT NULL DEFAULT FALSE,
    PaidDate DATETIME NULL,
    CreatedBy VARCHAR(100),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_liabilities_duedate (DueDate),
    INDEX idx_liabilities_ispaid (IsPaid)
);

-- Таблица активов
CREATE TABLE IF NOT EXISTS Assets (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Category INT NOT NULL COMMENT 'AssetCategory: 0=Equipment, 1=Vehicle, 2=Furniture, 3=RealEstate, 4=Other',
    Name VARCHAR(200) NOT NULL,
    PurchasePrice DECIMAL(18,2) NOT NULL,
    PurchaseDate DATETIME NOT NULL,
    UsefulLifeYears INT NOT NULL,
    CreatedBy VARCHAR(100),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_assets_category (Category)
);

-- Таблица финансовых планов
CREATE TABLE IF NOT EXISTS FinancialPlans (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Year INT NOT NULL,
    Month INT NOT NULL,
    PlannedRevenue DECIMAL(18,2) NOT NULL,
    PlannedExpenses DECIMAL(18,2) NOT NULL,
    Notes VARCHAR(1000),
    CreatedBy VARCHAR(100),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE INDEX idx_financial_plans_period (Year, Month)
);
