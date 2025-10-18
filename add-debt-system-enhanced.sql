-- =====================================================
-- МИГРАЦИЯ: СИСТЕМА ДОЛГОВ - ПОЛНАЯ РЕАЛИЗАЦИЯ
-- =====================================================
-- Дата: 2025-01-18
-- Описание: Добавление полноценной системы долгов с товарами
-- =====================================================

-- 1. СОЗДАНИЕ ТАБЛИЦЫ ТОВАРОВ В ДОЛГЕ
-- =====================================================
CREATE TABLE IF NOT EXISTS DebtItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    DebtId INT NOT NULL,
    ProductId INT NOT NULL,
    ProductName VARCHAR(500) NOT NULL,
    Sku VARCHAR(100) NULL,
    Qty DECIMAL(18,2) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedAt DATETIME(6) NULL,
    UpdatedBy VARCHAR(255) NULL,
    
    INDEX IX_DebtItems_DebtId (DebtId),
    INDEX IX_DebtItems_ProductId (ProductId),
    
    FOREIGN KEY (DebtId) REFERENCES Debts(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. ОБНОВЛЕНИЕ ТАБЛИЦЫ ДОЛГОВ
-- =====================================================
-- Добавляем новые поля в существующую таблицу Debts

-- Изначальная сумма долга (не меняется)
ALTER TABLE Debts 
ADD COLUMN OriginalAmount DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER Amount;

-- Примечания
ALTER TABLE Debts 
ADD COLUMN Notes TEXT NULL AFTER Status;

-- Дата создания
ALTER TABLE Debts 
ADD COLUMN CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) AFTER Notes;

-- Кто создал
ALTER TABLE Debts 
ADD COLUMN CreatedBy VARCHAR(255) NULL AFTER CreatedAt;

-- Обновляем OriginalAmount для существующих долгов
UPDATE Debts 
SET OriginalAmount = Amount 
WHERE OriginalAmount = 0;

-- 3. ДОБАВЛЕНИЕ ИНДЕКСОВ ДЛЯ ПРОИЗВОДИТЕЛЬНОСТИ
-- =====================================================
CREATE INDEX IX_Debts_ClientId_Status ON Debts(ClientId, Status);
CREATE INDEX IX_Debts_DueDate ON Debts(DueDate);
CREATE INDEX IX_Debts_CreatedAt ON Debts(CreatedAt);

-- 4. СПРАВОЧНАЯ ИНФОРМАЦИЯ
-- =====================================================
SELECT '✅ МИГРАЦИЯ ЗАВЕРШЕНА!' AS Status;
SELECT 'Таблица DebtItems создана' AS Info;
SELECT 'Таблица Debts обновлена (OriginalAmount, Notes, CreatedAt, CreatedBy)' AS Info;
SELECT 'Индексы добавлены для оптимизации запросов' AS Info;

-- 5. ПРОВЕРКА СТРУКТУРЫ
-- =====================================================
SELECT 'Проверка структуры DebtItems:' AS Info;
DESCRIBE DebtItems;

SELECT 'Проверка структуры Debts:' AS Info;
DESCRIBE Debts;

-- =====================================================
-- ИНСТРУКЦИЯ ПО ИСПОЛЬЗОВАНИЮ
-- =====================================================
/*

**КАК РАБОТАЕТ СИСТЕМА ДОЛГОВ:**

1. **Создание долга при продаже:**
   - Менеджер создает продажу с PaymentType = Debt (11)
   - Указывает срок оплаты (DebtDueDate) или по умолчанию +30 дней
   - Автоматически создается запись в Debts с товарами в DebtItems

2. **Редактирование товаров в долге:**
   PUT /api/debts/{id}/items
   - Можно изменить цену и количество каждого товара
   - Автоматически пересчитывается общая сумма долга
   - Учитываются уже сделанные оплаты

3. **Частичная оплата долга:**
   POST /api/debts/{id}/pay
   - Клиент может платить по частям
   - Сумма долга уменьшается
   - При полной оплате статус → Paid

4. **Просмотр долгов клиента:**
   GET /api/debts/by-client/{clientId}
   - Все долги конкретного клиента
   - Общая сумма долга

5. **Список должников:**
   GET /api/clients/debtors
   - Все клиенты с активными долгами
   - Сортировка по сумме долга

6. **Детали клиента с долгом:**
   GET /api/clients/{id}/with-debt
   - Информация о клиенте
   - Его текущий долг
   - История покупок
   - Общая сумма покупок

**ВАЖНО:**
- Нельзя редактировать оплаченные долги
- Товары в долге хранят snapshot данных на момент создания
- Можно изменить цену товара в долге без влияния на каталог

*/
