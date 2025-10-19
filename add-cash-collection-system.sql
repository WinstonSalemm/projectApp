-- ===================================================================
-- СИСТЕМА ИНКАССАЦИИ СЕРЫХ ДЕНЕГ
-- ===================================================================
-- Назначение: Учет инкассаций наличных без чека и Click без чека
-- Автор: AI Assistant
-- Дата: 2025-10-19
-- ===================================================================

-- Создаем таблицу инкассаций
CREATE TABLE IF NOT EXISTS CashCollections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CollectionDate DATETIME NOT NULL,
    AccumulatedAmount DECIMAL(18,2) NOT NULL,    -- Накоплено с последней инкассации
    CollectedAmount DECIMAL(18,2) NOT NULL,      -- Сдано при инкассации
    RemainingAmount DECIMAL(18,2) NOT NULL,      -- Остаток (Cash Flow)
    Notes TEXT,
    CreatedBy TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now'))
);

-- Индексы для быстрого поиска
CREATE INDEX IF NOT EXISTS IX_CashCollections_CollectionDate 
ON CashCollections(CollectionDate DESC);

CREATE INDEX IF NOT EXISTS IX_CashCollections_CreatedAt 
ON CashCollections(CreatedAt DESC);

-- ===================================================================
-- ПРИМЕР ДАННЫХ (для тестирования)
-- ===================================================================

-- Инкассация 1 (неделю назад)
INSERT INTO CashCollections (CollectionDate, AccumulatedAmount, CollectedAmount, RemainingAmount, Notes, CreatedBy, CreatedAt)
VALUES (
    datetime('now', '-7 days'),
    350000000,  -- Накопилось 350 млн
    350000000,  -- Сдали все
    0,          -- Остаток 0
    'Полная инкассация',
    'admin',
    datetime('now', '-7 days')
);

-- Инкассация 2 (3 дня назад)
INSERT INTO CashCollections (CollectionDate, AccumulatedAmount, CollectedAmount, RemainingAmount, Notes, CreatedBy, CreatedAt)
VALUES (
    datetime('now', '-3 days'),
    200000000,  -- Накопилось 200 млн
    180000000,  -- Сдали 180 млн
    20000000,   -- Остаток 20 млн (дивиденды)
    'Частичная инкассация, 20М на дивиденды',
    'admin',
    datetime('now', '-3 days')
);

-- ===================================================================
-- ПОЛЕЗНЫЕ ЗАПРОСЫ
-- ===================================================================

-- 1. Текущая сумма к инкассации (с последней инкассации)
/*
SELECT 
    COALESCE(SUM(s.Total), 0) as CurrentAccumulated,
    MAX(cc.CollectionDate) as LastCollectionDate
FROM Sales s
LEFT JOIN (
    SELECT MAX(CollectionDate) as CollectionDate
    FROM CashCollections
) cc ON 1=1
WHERE s.CreatedAt > COALESCE(cc.CollectionDate, '2000-01-01')
  AND s.PaymentType IN (1, 3, 9); -- CashNoReceipt, Click, ClickNoReceipt
*/

-- 2. Общий неинкассированный остаток
/*
SELECT COALESCE(SUM(RemainingAmount), 0) as TotalRemaining
FROM CashCollections;
*/

-- 3. История инкассаций за месяц
/*
SELECT 
    CollectionDate,
    AccumulatedAmount,
    CollectedAmount,
    RemainingAmount,
    Notes,
    CreatedBy
FROM CashCollections
WHERE CollectionDate >= datetime('now', '-30 days')
ORDER BY CollectionDate DESC;
*/

-- 4. Статистика инкассаций
/*
SELECT 
    COUNT(*) as TotalCollections,
    SUM(AccumulatedAmount) as TotalAccumulated,
    SUM(CollectedAmount) as TotalCollected,
    SUM(RemainingAmount) as TotalRemaining,
    AVG(AccumulatedAmount) as AvgAccumulated,
    AVG(CollectedAmount) as AvgCollected
FROM CashCollections
WHERE CollectionDate >= datetime('now', '-90 days');
*/

-- ===================================================================
-- КОММЕНТАРИИ
-- ===================================================================

/*
ЛОГИКА РАБОТЫ:

1. НАКОПЛЕНИЕ:
   - С последней инкассации копятся серые продажи
   - PaymentType: CashNoReceipt (1), Click (3), ClickNoReceipt (9)
   - Сумма автоматически рассчитывается при создании инкассации

2. ИНКАССАЦИЯ:
   - Указывается сумма сданная (CollectedAmount)
   - Остаток = Накоплено - Сдано
   - После инкассации счетчик обнуляется

3. CASH FLOW:
   - RemainingAmount хранится отдельно по каждой инкассации
   - Общий остаток = SUM(RemainingAmount)
   - Это деньги, которые остаются на предприятии

4. ДИВИДЕНДЫ:
   - Часть RemainingAmount может быть дивидендами
   - Указывается в поле Notes

ПРИМЕР:
- Накопилось: 200 млн
- Сдано: 180 млн
- Остаток: 20 млн (из них 10М дивиденды, 10М резерв)
*/
