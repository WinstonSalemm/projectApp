# 💰 СИСТЕМА ИНКАССАЦИИ СЕРЫХ ДЕНЕГ

## 📋 КОНЦЕПЦИЯ:

**Серые продажи** = Наличные без чека + Click без чека

Эти деньги накапливаются и периодически инкассируются (сдаются).
Часть денег может оставаться на предприятии как Cash Flow или дивиденды.

---

## 🎯 БИЗНЕС-ЛОГИКА:

### **Пример работы:**

```
День 1-7: Продажи без чека
├─ Понедельник: +30 млн
├─ Вторник: +25 млн
├─ Среда: +40 млн
├─ Четверг: +35 млн
├─ Пятница: +50 млн
├─ Суббота: +15 млн
└─ Воскресенье: +5 млн
───────────────────────────
ИТОГО накоплено: 200 млн

ИНКАССАЦИЯ (понедельник):
├─ Накоплено: 200 млн
├─ Сдано: 180 млн
└─ Остаток: 20 млн ← Cash Flow на предприятии

ПОСЛЕ ИНКАССАЦИИ:
├─ Счетчик обнулился → 0
├─ 20 млн хранится отдельно
└─ Начинает копиться заново

День 8-14: Новые продажи
├─ Понедельник: +0 (инкассация)
├─ Вторник: +28 млн
├─ Среда: +32 млн
├─ Четверг: +45 млн
├─ Пятница: +55 млн
├─ Суббота: +20 млн
└─ Воскресенье: +10 млн
───────────────────────────
ИТОГО накоплено: 190 млн

ВТОРАЯ ИНКАССАЦИЯ:
├─ Накоплено: 190 млн
├─ Сдано: 190 млн
└─ Остаток: 0 млн

ОБЩИЙ НЕИНКАССИРОВАННЫЙ ОСТАТОК:
= 20 млн (первая) + 0 млн (вторая) = 20 млн
```

---

## 🏗️ АРХИТЕКТУРА:

### **1. Модель данных (CashCollection):**

```csharp
public class CashCollection
{
    public int Id { get; set; }
    public DateTime CollectionDate { get; set; }        // Дата инкассации
    public decimal AccumulatedAmount { get; set; }      // Накоплено
    public decimal CollectedAmount { get; set; }        // Сдано
    public decimal RemainingAmount { get; set; }        // Остаток
    public string? Notes { get; set; }                  // Примечание
    public string? CreatedBy { get; set; }              // Кто провел
}
```

### **2. Сервис (CashCollectionService):**

**Методы:**
- `GetSummaryAsync()` - Сводка для страницы
- `CreateCollectionAsync()` - Провести инкассацию
- `GetHistoryAsync()` - История инкассаций
- `DeleteLastCollectionAsync()` - Удалить последнюю

**Расчет накопленной суммы:**
```csharp
var greyPaymentTypes = new[] 
{ 
    PaymentType.CashNoReceipt,   // 1
    PaymentType.Click,            // 3
    PaymentType.ClickNoReceipt    // 9
};

var accumulated = await _db.Sales
    .Where(s => s.CreatedAt > lastCollectionDate && 
               greyPaymentTypes.Contains(s.PaymentType))
    .SumAsync(s => s.Total);
```

---

## 🔌 API ЭНДПОИНТЫ:

### **1. GET /api/cash-collection/summary**
Получить сводку для страницы "К инкассации"

**Response:**
```json
{
  "currentAccumulated": 150000000,      // Накоплено с последней
  "lastCollectionDate": "2025-10-14",   // Дата последней инкассации
  "totalRemainingAmount": 20000000,     // Общий остаток
  "history": [
    {
      "id": 2,
      "collectionDate": "2025-10-14",
      "accumulatedAmount": 200000000,
      "collectedAmount": 180000000,
      "remainingAmount": 20000000,
      "notes": "Дивиденды учредителей",
      "createdBy": "admin"
    }
  ]
}
```

### **2. POST /api/cash-collection**
Провести инкассацию

**Request:**
```json
{
  "collectedAmount": 180000000,
  "notes": "Частичная инкассация, 20М на дивиденды"
}
```

**Response:**
```json
{
  "id": 3,
  "collectionDate": "2025-10-19T15:00:00",
  "accumulatedAmount": 200000000,
  "collectedAmount": 180000000,
  "remainingAmount": 20000000,
  "notes": "Частичная инкассация, 20М на дивиденды",
  "createdBy": "admin"
}
```

### **3. GET /api/cash-collection/history**
История инкассаций

**Query params:**
- `from` (optional) - дата начала
- `to` (optional) - дата окончания

### **4. DELETE /api/cash-collection/last**
Удалить последнюю инкассацию (если была ошибка)

---

## 📱 UI - СТРАНИЦА "К ИНКАССАЦИИ":

### **Основные элементы:**

```
┌─────────────────────────────────────────┐
│ 💵 К ИНКАССАЦИИ                         │
├─────────────────────────────────────────┤
│                                         │
│ 📈 С ПОСЛЕДНЕЙ ИНКАССАЦИИ:              │
│ ┌─────────────────────────────────────┐ │
│ │   150,000,000 UZS                   │ │
│ │   С 14.10.2025                      │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ 💼 НЕИНКАССИРОВАННЫЙ ОСТАТОК:           │
│ ┌─────────────────────────────────────┐ │
│ │   20,000,000 UZS                    │ │
│ │   (Cash Flow на предприятии)        │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │ Сумма к сдаче:                      │ │
│ │ [__________________] UZS            │ │
│ │                                     │ │
│ │ Примечание:                         │ │
│ │ [__________________________]        │ │
│ │                                     │ │
│ │ [📤 Провести инкассацию]            │ │
│ └─────────────────────────────────────┘ │
│                                         │
├─────────────────────────────────────────┤
│ 📜 ИСТОРИЯ ИНКАССАЦИЙ                   │
├─────────────────────────────────────────┤
│ Дата        │ Накоплено │ Сдано │ Остаток│
│─────────────┼───────────┼───────┼────────│
│ 14.10.2025  │ 200 млн   │ 180М  │ 20М    │
│ 07.10.2025  │ 350 млн   │ 350М  │  0М    │
│ 30.09.2025  │ 280 млн   │ 250М  │ 30М    │
└─────────────────────────────────────────┘
```

---

## 📊 ОТЧЕТЫ И АНАЛИТИКА:

### **Метрики:**

1. **Текущая сумма к инкассации**
   - Серые продажи с последней инкассации
   - Обновляется в реальном времени

2. **Неинкассированный остаток**
   - Сумма всех RemainingAmount
   - Cash Flow на предприятии

3. **Средняя инкассация**
   - Средняя сумма за N дней

4. **% Инкассации**
   - Collected / Accumulated * 100%

### **SQL запросы для аналитики:**

```sql
-- Текущая сумма к инкассации
SELECT COALESCE(SUM(s.Total), 0) as CurrentAccumulated
FROM Sales s
WHERE s.CreatedAt > (SELECT MAX(CollectionDate) FROM CashCollections)
  AND s.PaymentType IN (1, 3, 9);

-- Общий остаток
SELECT COALESCE(SUM(RemainingAmount), 0) as TotalRemaining
FROM CashCollections;

-- Статистика за месяц
SELECT 
    COUNT(*) as Collections,
    SUM(AccumulatedAmount) as TotalAccumulated,
    SUM(CollectedAmount) as TotalCollected,
    AVG(CollectedAmount * 100.0 / AccumulatedAmount) as AvgCollectionPercent
FROM CashCollections
WHERE CollectionDate >= datetime('now', '-30 days');
```

---

## 🔐 БЕЗОПАСНОСТЬ:

### **Доступ:**
- ✅ **Owner** - полный доступ
- ✅ **Admin** - полный доступ
- ❌ **Manager** - НЕТ доступа (скрыто)

### **Аудит:**
- Логируется каждая инкассация
- Кто провел (CreatedBy)
- Когда (CreatedAt)
- Можно удалить только последнюю

---

## 🎯 ИНТЕГРАЦИЯ С НАЛОГАМИ:

### **Обновить TaxCalculationService:**

```csharp
// Разделить выручку на белую и серую
var whiteSales = sales.Where(s => 
    s.PaymentType == PaymentType.CashWithReceipt ||
    s.PaymentType == PaymentType.CardWithReceipt ||
    s.PaymentType == PaymentType.ClickWithReceipt ||
    s.PaymentType == PaymentType.Payme ||
    s.PaymentType == PaymentType.Contract
).ToList();

var greySales = sales.Where(s => 
    s.PaymentType == PaymentType.CashNoReceipt ||
    s.PaymentType == PaymentType.Click ||
    s.PaymentType == PaymentType.ClickNoReceipt
).ToList();

// Налоги ТОЛЬКО с белой выручки
report.TotalRevenue = whiteSales.Sum(s => s.Total);
report.GreyRevenue = greySales.Sum(s => s.Total);
```

---

## 📝 ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ:

### **Сценарий 1: Обычная инкассация**
```
1. Накопилось 200 млн
2. Сдаем все 200 млн
3. Остаток = 0
```

### **Сценарий 2: Частичная инкассация (дивиденды)**
```
1. Накопилось 200 млн
2. Сдаем 180 млн
3. Остаток = 20 млн (дивиденды учредителям)
```

### **Сценарий 3: Частичная инкассация (резерв)**
```
1. Накопилось 300 млн
2. Сдаем 250 млн
3. Остаток = 50 млн (резервный фонд)
```

---

## ✅ СЛЕДУЮЩИЕ ШАГИ:

1. ✅ Создать модель `CashCollection`
2. ✅ Создать сервис `CashCollectionService`
3. ✅ Создать контроллер `CashCollectionController`
4. ✅ Создать миграцию БД
5. ⏳ Зарегистрировать в Program.cs
6. ⏳ Добавить в AppDbContext
7. ⏳ Создать UI страницу в MAUI
8. ⏳ Интегрировать с налоговым учетом
9. ⏳ Тестирование

---

## 🎉 ИТОГО:

Система позволяет:
- ✅ Отслеживать серые продажи
- ✅ Проводить инкассации с гибкостью
- ✅ Хранить Cash Flow на предприятии
- ✅ Контролировать дивиденды
- ✅ Видеть полную историю
- ✅ Разделять белую и серую выручку для налогов
