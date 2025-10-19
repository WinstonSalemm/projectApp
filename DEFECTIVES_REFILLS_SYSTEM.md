# 🔧 СИСТЕМА "БРАК" И "ПЕРЕЗАРЯДКА" - ГОТОВА!

## 🎉 ЧТО РЕАЛИЗОВАНО:

### **ДЛЯ МЕНЕДЖЕРОВ - 2 НОВЫЕ ФУНКЦИИ:**

1. **БРАК** 🗑️ - списание бракованных товаров
2. **ПЕРЕЗАРЯДКА** 🔥 - перезарядка огнетушителей

---

## 🗑️ СИСТЕМА БРАКА:

### **Концепция:**
- Пришла партия товара, в ней есть брак
- Менеджер списывает бракованные товары
- Товар уменьшается на складе
- История всех списаний
- Возможность отменить (если ошибка)

### **API эндпоинты:**

```
GET  /api/defectives              - Список брака
POST /api/defectives              - Создать списание брака
POST /api/defectives/{id}/cancel  - Отменить списание
```

### **Как работает:**

#### **1. Создание списания брака:**

```http
POST /api/defectives
{
  "productId": 123,
  "quantity": 5,
  "warehouse": 0,  // 0 = ND40, 1 = IM40
  "reason": "Повреждение корпуса при транспортировке"
}
```

**Что происходит:**
- ✅ Проверяется наличие товара на складе
- ✅ Списывается 5 единиц со склада ND-40
- ✅ Создается запись в DefectiveItems
- ✅ Создается транзакция InventoryTransaction (тип Defective)
- ✅ Количество на складе уменьшается

#### **2. Отмена списания:**

```http
POST /api/defectives/15/cancel
{
  "reason": "Ошибка, товар не бракованный"
}
```

**Что происходит:**
- ✅ Товар возвращается на склад
- ✅ Статус меняется на Cancelled
- ✅ Создается транзакция DefectiveCancelled
- ✅ Количество на складе увеличивается

---

## 🔥 СИСТЕМА ПЕРЕЗАРЯДКИ:

### **Концепция:**
- Огнетушители приехали спущенные
- Их нужно перезарядить
- Указывается количество + стоимость
- Товар остается на складе (не списывается!)
- История всех перезарядок

### **API эндпоинты:**

```
GET  /api/refills              - Список перезарядок
POST /api/refills              - Создать перезарядку
POST /api/refills/{id}/cancel  - Отменить перезарядку
GET  /api/refills/stats        - Статистика по перезарядкам
```

### **Как работает:**

#### **1. Создание перезарядки:**

```http
POST /api/refills
{
  "productId": 456,
  "quantity": 10,
  "warehouse": 0,  // 0 = ND40, 1 = IM40
  "costPerUnit": 50000,  // 50,000 сум за 1 огнетушитель
  "notes": "Плановая перезарядка огнетушителей"
}
```

**Что происходит:**
- ✅ Проверяется товар
- ✅ Создается запись в RefillOperations
- ✅ TotalCost = 10 * 50,000 = 500,000 сум
- ✅ Создается транзакция InventoryTransaction (тип Refill)
- ⚠️ **Товар НЕ списывается** (остается на складе)

#### **2. Отмена перезарядки:**

```http
POST /api/refills/25/cancel
{
  "reason": "Дубликат записи"
}
```

**Что происходит:**
- ✅ Статус меняется на Cancelled
- ⚠️ Стоимость не возвращается (деньги уже потрачены)
- ℹ️ Только для корректности учета

#### **3. Статистика:**

```http
GET /api/refills/stats?from=2025-10-01&to=2025-10-31
```

**Response:**
```json
{
  "totalRefills": 15,
  "totalQuantity": 150,
  "totalCost": 7500000,
  "averageCostPerUnit": 50000
}
```

---

## 📊 РАЗНИЦА МЕЖДУ БРАКОМ И ПЕРЕЗАРЯДКОЙ:

| Параметр | БРАК 🗑️ | ПЕРЕЗАРЯДКА 🔥 |
|----------|---------|----------------|
| **Списание товара** | ✅ ДА (уменьшает остатки) | ❌ НЕТ (товар остается) |
| **Стоимость** | Не указывается | Обязательна |
| **Назначение** | Испорченный товар | Ремонт/обслуживание |
| **Пример** | Битое стекло, трещины | Перезарядка огнетушителя |
| **Отмена** | Возвращает на склад | Только статус |

---

## 💾 БАЗА ДАННЫХ:

### **Таблица DefectiveItems:**

```sql
CREATE TABLE DefectiveItems (
    Id INTEGER PRIMARY KEY,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT,
    Quantity INTEGER NOT NULL,
    Warehouse INTEGER NOT NULL,  -- 0=ND40, 1=IM40
    Reason TEXT,
    Status INTEGER NOT NULL,     -- 0=Active, 1=Cancelled
    CreatedBy TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    CancelledBy TEXT,
    CancelledAt DATETIME,
    CancellationReason TEXT
);
```

### **Таблица RefillOperations:**

```sql
CREATE TABLE RefillOperations (
    Id INTEGER PRIMARY KEY,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT,
    Quantity INTEGER NOT NULL,
    Warehouse INTEGER NOT NULL,  -- 0=ND40, 1=IM40
    CostPerUnit DECIMAL(18,2) NOT NULL,
    TotalCost DECIMAL(18,2) NOT NULL,
    Notes TEXT,
    Status INTEGER NOT NULL,     -- 0=Active, 1=Cancelled
    CreatedBy TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL,
    CancelledBy TEXT,
    CancelledAt DATETIME,
    CancellationReason TEXT
);
```

---

## 🔗 ИНТЕГРАЦИЯ:

### **InventoryTransactionType (обновлен):**

```csharp
public enum InventoryTransactionType
{
    Purchase = 0,
    Sale = 1,
    ReturnIn = 2,
    ReturnOut = 3,
    MoveNdToIm = 4,
    Adjust = 5,
    Reserve = 6,
    Release = 7,
    Reprice = 8,
    Reservation = 9,
    ReservationCancelled = 10,
    ContractDelivery = 11,
    Defective = 12,              // ← НОВОЕ
    DefectiveCancelled = 13,      // ← НОВОЕ
    Refill = 14                   // ← НОВОЕ
}
```

---

## 📋 ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ:

### **Сценарий 1: Списание брака**

```
Менеджер получил партию огнетушителей
Обнаружил 5 штук с повреждениями корпуса

Действия:
1. Открывает "БРАК"
2. Выбирает товар: Огнетушитель ОП-4
3. Количество: 5
4. Склад: ND-40
5. Причина: "Повреждение корпуса при транспортировке"
6. Подтверждает

Результат:
✅ 5 огнетушителей списано с ND-40
✅ Остатки уменьшились: 50 → 45
✅ Запись в истории брака
```

### **Сценарий 2: Перезарядка**

```
Менеджер получил партию огнетушителей
Все спущенные, нужна перезарядка

Действия:
1. Открывает "ПЕРЕЗАРЯДКА"
2. Выбирает товар: Огнетушитель ОП-4
3. Количество: 10
4. Склад: ND-40
5. Стоимость за штуку: 50,000 сум
6. Примечание: "Плановая перезарядка"
7. Подтверждает

Результат:
✅ Запись создана
✅ Общая стоимость: 500,000 сум
✅ Остатки НЕ изменились (товар на месте)
✅ История перезарядок обновлена
```

### **Сценарий 3: Отмена брака (ошибка)**

```
Менеджер случайно списал товар как брак
На самом деле товар нормальный

Действия:
1. Открывает историю брака
2. Находит запись
3. Нажимает "Отменить"
4. Причина: "Ошибка, товар не бракованный"
5. Подтверждает

Результат:
✅ Товар вернулся на склад
✅ Остатки восстановлены: 45 → 50
✅ Статус: Cancelled
```

---

## 🎯 ДОСТУП:

**Кто может использовать:**
- ✅ **Manager** - создавать брак и перезарядку
- ✅ **Manager** - отменять свои записи
- ✅ **Admin/Owner** - полный доступ
- ✅ **Admin/Owner** - отменять любые записи

---

## 📁 ФАЙЛЫ:

**Backend:**
- `Models/DefectiveItem.cs` - модель брака
- `Models/RefillOperation.cs` - модель перезарядки
- `Models/InventoryTransactionType.cs` - обновлен (новые типы)
- `Controllers/DefectivesController.cs` - API брака
- `Controllers/RefillsController.cs` - API перезарядки
- `Data/AppDbContext.cs` - добавлены DbSet

**База данных:**
- `add-defectives-refills-system.sql` - миграция

---

## ⚡ ЧТО ДАЛЬШЕ:

### **1. Применить миграцию:**
```bash
sqlite3 ProjectApp.db < add-defectives-refills-system.sql
```

### **2. Создать UI в MAUI (следующий шаг):**
- Страница "Брак" с историей
- Страница "Перезарядка" с историей
- Выбор товара
- Ввод количества и причины
- Отмена записей

---

## 📊 СТАТИСТИКА:

### **Примеры запросов:**

**Самые проблемные товары (много брака):**
```sql
SELECT 
    ProductName,
    COUNT(*) as DefectCount,
    SUM(Quantity) as TotalDefectiveQty
FROM DefectiveItems
WHERE Status = 0
GROUP BY ProductId
ORDER BY TotalDefectiveQty DESC
LIMIT 10;
```

**Расходы на перезарядку за месяц:**
```sql
SELECT 
    COUNT(*) as RefillCount,
    SUM(Quantity) as TotalQuantity,
    SUM(TotalCost) as TotalCost
FROM RefillOperations
WHERE Status = 0
  AND CreatedAt >= datetime('now', '-30 days');
```

---

## ✅ ИТОГО:

```
┌────────────────────────────────┐
│ Backend API:      ✅ 100%      │
│ Модели:           ✅ 100%      │
│ Контроллеры:      ✅ 100%      │
│ БД миграция:      ✅ 100%      │
│ Документация:     ✅ 100%      │
├────────────────────────────────┤
│ ГОТОВНОСТЬ:       ✅ 100%      │
└────────────────────────────────┘
```

**Git commit:** `223080c` - Add-defectives-and-refills-system-for-managers

**BACKEND ГОТОВ! МОЖНО СОЗДАВАТЬ UI В MAUI!** 🚀

---

## 💡 ОСОБЕННОСТИ:

✅ **Брак списывает товар** - уменьшает остатки на складе  
✅ **Перезарядка НЕ трогает остатки** - товар остается на месте  
✅ **История операций** - все записано в InventoryTransactions  
✅ **Возможность отмены** - если ошибка  
✅ **Snapshot данных** - название товара и SKU сохраняются  
✅ **Выбор склада** - ND-40 или IM-40  
✅ **Учет стоимости** - для перезарядки обязателен  
✅ **Статистика** - кто, когда, сколько  

**МЕНЕДЖЕРЫ ПОЛУЧИЛИ 2 НОВЫХ ИНСТРУМЕНТА!** 🎉
