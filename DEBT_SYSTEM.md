# 💰 СИСТЕМА ДОЛГОВ - Полная документация

## 🎯 Концепция

**Долг** = отгрузка товара клиенту с отсроченной оплатой. Менеджер записывает товары и цены, указывает клиента и срок оплаты. Клиент платит по записанной цене, но менеджер может редактировать стоимость каждого товара в долге.

---

## 📋 Основные возможности

✅ **Создание долга** - автоматически при продаже с типом "В долг"  
✅ **Редактирование товаров** - изменение цены/количества в долге  
✅ **Частичная оплата** - клиент может платить по частям  
✅ **Список должников** - отдельная вкладка с клиентами, имеющими долги  
✅ **История покупок** - при открытии клиента видно его долг и все покупки  
✅ **Общая сумма покупок** - сколько всего клиент набрал товара на N сум  

---

## 🗄️ Модели данных

### **1. Debt (Долг)**

```csharp
public class Debt
{
    public int Id { get; set; }
    public int ClientId { get; set; }       // Клиент-должник
    public int SaleId { get; set; }         // Связь с продажей
    public decimal Amount { get; set; }     // Текущая сумма долга
    public decimal OriginalAmount { get; set; }  // Изначальная сумма
    public DateTime DueDate { get; set; }   // Срок оплаты
    public DebtStatus Status { get; set; }  // Статус
    public List<DebtItem> Items { get; set; }  // Товары в долге
    public string? Notes { get; set; }      // Примечания
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
```

### **2. DebtItem (Товар в долге)**

```csharp
public class DebtItem
{
    public int Id { get; set; }
    public int DebtId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public string? Sku { get; set; }
    public decimal Qty { get; set; }        // МОЖНО РЕДАКТИРОВАТЬ!
    public decimal Price { get; set; }      // МОЖНО РЕДАКТИРОВАТЬ!
    public decimal Total { get; set; }      // Qty * Price
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### **3. Статусы долга**

```csharp
public enum DebtStatus
{
    Open = 0,      // Открыт (активный долг)
    Paid = 1,      // Оплачен полностью
    Overdue = 2,   // Просрочен
    Canceled = 3   // Отменен
}
```

---

## 🔄 Как это работает

### **Сценарий 1: Создание долга**

**Шаг 1:** Менеджер создает продажу с типом `Debt`

```http
POST /api/sales
Authorization: Bearer <token>
Content-Type: application/json

{
  "clientId": 5,                    // Обязательно!
  "clientName": "ООО Рога и копыта",
  "paymentType": 11,                // 11 = Debt
  "debtDueDate": "2025-02-18",      // Срок оплаты (опционально, по умолчанию +30 дней)
  "debtNotes": "Постоянный клиент",
  "items": [
    {
      "productId": 1,
      "qty": 10,
      "unitPrice": 150000
    },
    {
      "productId": 3,
      "qty": 5,
      "unitPrice": 350000
    }
  ]
}
```

**Что происходит:**
1. ✅ Создается продажа с `PaymentType = Debt`
2. ✅ Автоматически создается запись в таблице `Debts`
3. ✅ Товары копируются в таблицу `DebtItems`
4. ✅ Долг получает статус `Open`

**Результат:**
```json
{
  "id": 123,
  "clientId": 5,
  "saleId": 999,
  "amount": 3250000,
  "originalAmount": 3250000,
  "dueDate": "2025-02-18",
  "status": 0,
  "items": [
    {
      "id": 1,
      "productId": 1,
      "productName": "ОП-1 (порошковый) 1 кг",
      "qty": 10,
      "price": 150000,
      "total": 1500000
    },
    {
      "id": 2,
      "productId": 3,
      "productName": "ОП-5 (порошковый) 5 кг",
      "qty": 5,
      "price": 350000,
      "total": 1750000
    }
  ]
}
```

---

### **Сценарий 2: Редактирование товаров в долге**

**Проблема:** Цена на товар изменилась, нужно пересчитать долг.

**Решение:** Редактируем товары в долге

```http
PUT /api/debts/123/items
Authorization: Bearer <token>
Content-Type: application/json

{
  "items": [
    {
      "id": 1,
      "productId": 1,
      "productName": "ОП-1 (порошковый) 1 кг",
      "sku": "OP-1",
      "qty": 10,
      "price": 160000,     // ← Повысили цену!
      "total": 1600000
    },
    {
      "id": 2,
      "productId": 3,
      "productName": "ОП-5 (порошковый) 5 кг",
      "sku": "OP-5",
      "qty": 5,
      "price": 350000,
      "total": 1750000
    }
  ]
}
```

**Что происходит:**
1. ✅ Обновляется `Qty`, `Price`, `Total` для каждого товара
2. ✅ Пересчитывается общая сумма долга
3. ✅ Учитываются уже сделанные оплаты
4. ✅ Если долг полностью оплачен → статус `Paid`

**Формула пересчета:**
```
newTotal = ΣItems.Total
paidAmount = OriginalAmount - Amount
newDebtAmount = max(0, newTotal - paidAmount)
```

---

### **Сценарий 3: Частичная оплата долга**

**Клиент принес 1,000,000 сум:**

```http
POST /api/debts/123/pay
Authorization: Bearer <token>
Content-Type: application/json

{
  "amount": 1000000
}
```

**Результат:**
```json
{
  "id": 123,
  "amount": 2350000,    // Было 3350000, стало 2350000
  "status": 0           // Всё ещё Open
}
```

**Клиент принес ещё 2,350,000 сум:**

```http
POST /api/debts/123/pay
{
  "amount": 2350000
}
```

**Результат:**
```json
{
  "id": 123,
  "amount": 0,         // Долг погашен!
  "status": 1          // Статус → Paid
}
```

---

## 📊 API Эндпоинты

### **Работа с долгами**

**1. Список всех долгов (с фильтрами)**
```http
GET /api/debts?clientId=5&status=0&from=2025-01-01&to=2025-12-31&page=1&size=50
```

**2. Детали долга с товарами**
```http
GET /api/debts/123
```

Ответ:
```json
{
  "id": 123,
  "clientId": 5,
  "clientName": "ООО Рога и копыта",
  "saleId": 999,
  "amount": 3350000,
  "originalAmount": 3350000,
  "paidAmount": 0,
  "dueDate": "2025-02-18",
  "status": "Open",
  "items": [...],
  "notes": "Постоянный клиент",
  "createdAt": "2025-01-18T12:00:00Z",
  "createdBy": "manager1"
}
```

**3. Долги конкретного клиента**
```http
GET /api/debts/by-client/5
```

Ответ:
```json
{
  "clientId": 5,
  "totalDebt": 3350000,
  "debts": [...]
}
```

**4. История оплат долга**
```http
GET /api/debts/123/payments
```

**5. Редактировать товары в долге**
```http
PUT /api/debts/123/items
Body: { "items": [...] }
```

**6. Оплатить долг**
```http
POST /api/debts/123/pay
Body: { "amount": 1000000 }
```

---

### **Работа с клиентами**

**7. Список ДОЛЖНИКОВ (клиенты с активными долгами)**
```http
GET /api/clients/debtors?page=1&size=50
```

Ответ:
```json
{
  "items": [
    {
      "clientId": 5,
      "clientName": "ООО Рога и копыта",
      "phone": "+998901234567",
      "type": "Company",
      "totalDebt": 3350000,
      "debtsCount": 1,
      "oldestDueDate": "2025-02-18"
    },
    {
      "clientId": 8,
      "clientName": "ИП Иванов",
      "phone": "+998907654321",
      "type": "Individual",
      "totalDebt": 1200000,
      "debtsCount": 2,
      "oldestDueDate": "2025-02-10"
    }
  ],
  "total": 2,
  "page": 1,
  "size": 50
}
```

**8. Клиент с долгом и историей покупок**
```http
GET /api/clients/5/with-debt
```

Ответ:
```json
{
  "client": {
    "id": 5,
    "name": "ООО Рога и копыта",
    "phone": "+998901234567",
    "type": "Company"
  },
  "debt": {
    "totalAmount": 3350000,
    "debts": [
      {
        "id": 123,
        "amount": 3350000,
        "dueDate": "2025-02-18",
        "status": 0
      }
    ]
  },
  "purchases": {
    "totalAmount": 15000000,  // ← СКОЛЬКО ВСЕГО НАБРАЛ ТОВАРА
    "count": 8,
    "history": [
      {
        "id": 999,
        "total": 3350000,
        "paymentType": 11,
        "createdAt": "2025-01-18T12:00:00Z"
      },
      {
        "id": 998,
        "total": 2500000,
        "paymentType": 0,
        "createdAt": "2025-01-15T10:00:00Z"
      }
      // ... остальные покупки
    ]
  }
}
```

---

## 🎨 UI Структура

### **Главная страница - 2 вкладки:**

**1. Клиенты (обычные)**
```
┌─────────────────────────────────────┐
│  Клиенты                            │
├─────────────────────────────────────┤
│ ООО Светлячок    +998901111111      │
│ ИП Петров        +998902222222      │
│ Физ.лицо Сидоров +998903333333      │
└─────────────────────────────────────┘
```

**2. Должники** ⚠️
```
┌─────────────────────────────────────┐
│  Должники                           │
├─────────────────────────────────────┤
│ ООО Рога и копыта                   │
│ ДОЛГ: 3,350,000 сум 🔴              │
│ Срок: 18.02.2025                    │
│                                      │
│ ИП Иванов                           │
│ ДОЛГ: 1,200,000 сум 🔴              │
│ Срок: 10.02.2025                    │
└─────────────────────────────────────┘
```

---

### **Карточка клиента:**

```
┌─────────────────────────────────────┐
│  ООО Рога и копыта                  │
│  +998901234567                      │
├─────────────────────────────────────┤
│  💰 ДОЛГ: 3,350,000 сум             │
│  📅 Срок оплаты: 18.02.2025         │
│  [Оплатить долг]                    │
├─────────────────────────────────────┤
│  📊 Всего покупок: 15,000,000 сум   │
│  🛒 Количество покупок: 8           │
├─────────────────────────────────────┤
│  ИСТОРИЯ ПОКУПОК:                   │
│  18.01.2025 - 3,350,000 сум (долг)  │
│  15.01.2025 - 2,500,000 сум         │
│  10.01.2025 - 1,800,000 сум         │
│  ...                                │
└─────────────────────────────────────┘
```

---

### **Детали долга:**

```
┌─────────────────────────────────────┐
│  Долг #123                          │
│  Клиент: ООО Рога и копыта          │
│  Срок: 18.02.2025                   │
├─────────────────────────────────────┤
│  Изначальная сумма: 3,350,000 сум   │
│  Оплачено: 0 сум                    │
│  Осталось: 3,350,000 сум            │
├─────────────────────────────────────┤
│  ТОВАРЫ:                            │
│                                      │
│  ОП-1 (порошковый) 1 кг             │
│  Кол-во: 10  Цена: 160,000          │
│  Итого: 1,600,000 сум               │
│  [Редактировать]                    │
│                                      │
│  ОП-5 (порошковый) 5 кг             │
│  Кол-во: 5  Цена: 350,000           │
│  Итого: 1,750,000 сум               │
│  [Редактировать]                    │
├─────────────────────────────────────┤
│  [Оплатить долг] [История оплат]    │
└─────────────────────────────────────┘
```

---

## ⚙️ База данных

### **Таблица: Debts**
```sql
CREATE TABLE Debts (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ClientId INT NOT NULL,
    SaleId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,           -- Текущая сумма долга
    OriginalAmount DECIMAL(18,2) NOT NULL,   -- Изначальная сумма
    DueDate DATETIME(6) NOT NULL,
    Status INT NOT NULL,
    Notes TEXT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    CreatedBy VARCHAR(255) NULL,
    
    INDEX IX_Debts_ClientId_Status (ClientId, Status),
    INDEX IX_Debts_DueDate (DueDate),
    FOREIGN KEY (ClientId) REFERENCES Clients(Id)
);
```

### **Таблица: DebtItems**
```sql
CREATE TABLE DebtItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    DebtId INT NOT NULL,
    ProductId INT NOT NULL,
    ProductName VARCHAR(500) NOT NULL,
    Sku VARCHAR(100) NULL,
    Qty DECIMAL(18,2) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedAt DATETIME(6) NULL,
    UpdatedBy VARCHAR(255) NULL,
    
    INDEX IX_DebtItems_DebtId (DebtId),
    FOREIGN KEY (DebtId) REFERENCES Debts(Id) ON DELETE CASCADE
);
```

---

## 🚀 Применение миграции

### **Вариант 1: Через Railway CLI**
```powershell
cd c:\projectApp
railway link
Get-Content "add-debt-system-enhanced.sql" | railway run mysql
```

### **Вариант 2: Через Railway Dashboard**
1. Открой Railway → Твой проект → MySQL → Query
2. Скопируй содержимое `add-debt-system-enhanced.sql`
3. Выполни запросы

---

## ✅ Что реализовано

✅ **Модели:**
- `Debt` - расширена (OriginalAmount, Notes, CreatedAt, CreatedBy, Items)
- `DebtItem` - новая модель для товаров в долге
- `PaymentType.Debt` - новый тип оплаты

✅ **Автоматическое создание долгов:**
- При продаже с `PaymentType = Debt`
- Товары копируются в `DebtItems`
- Можно указать срок оплаты

✅ **Редактирование товаров:**
- `PUT /api/debts/{id}/items`
- Изменение цены и количества
- Автоматический пересчет суммы долга

✅ **Частичная оплата:**
- `POST /api/debts/{id}/pay`
- Можно платить по частям
- Автоматическое закрытие при полной оплате

✅ **Список должников:**
- `GET /api/clients/debtors`
- Отдельная вкладка для UI
- Сортировка по сумме долга

✅ **Детали клиента:**
- `GET /api/clients/{id}/with-debt`
- Текущий долг сверху
- История покупок снизу
- Общая сумма покупок

✅ **Фильтры и запросы:**
- По клиенту, статусу, датам
- Долги конкретного клиента
- История оплат

---

## 📝 Примеры использования

### **Пример 1: Продажа в долг**

**Запрос:**
```bash
curl -X POST https://your-api.railway.app/api/sales \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": 5,
    "clientName": "ООО Рога и копыта",
    "paymentType": 11,
    "debtDueDate": "2025-02-18",
    "debtNotes": "Постоянный клиент, хорошая история",
    "items": [
      { "productId": 1, "qty": 10, "unitPrice": 150000 },
      { "productId": 3, "qty": 5, "unitPrice": 350000 }
    ]
  }'
```

### **Пример 2: Редактирование цен**

```bash
curl -X PUT https://your-api.railway.app/api/debts/123/items \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {
        "id": 1,
        "productId": 1,
        "productName": "ОП-1 (порошковый) 1 кг",
        "sku": "OP-1",
        "qty": 10,
        "price": 160000,
        "total": 1600000
      }
    ]
  }'
```

### **Пример 3: Частичная оплата**

```bash
curl -X POST https://your-api.railway.app/api/debts/123/pay \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{ "amount": 1000000 }'
```

---

## 🎉 ГОТОВО!

**Система долгов полностью реализована и готова к использованию!**

**Файлы:**
- `Models/Debt.cs` - обновлена
- `Models/DebtItem.cs` - новая
- `Models/PaymentType.cs` - добавлен Debt
- `Controllers/SalesController.cs` - автосоздание долгов
- `Controllers/DebtsController.cs` - расширен
- `Controllers/ClientsController.cs` - добавлены должники
- `Dtos/DebtDtos.cs` - новые DTO
- `Dtos/SaleDtos.cs` - поля долга
- `Data/AppDbContext.cs` - DbSet<DebtItem>
- `add-debt-system-enhanced.sql` - миграция
- `DEBT_SYSTEM.md` - эта документация

**Следующий шаг:** Примени миграцию и протестируй через Swagger! 🚀
