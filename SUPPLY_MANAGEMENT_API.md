# 📦 Supply Management & Costing System API

## Описание

Система управления поставками (ND-40 / IM-40) и расчета себестоимости товара.

**Ключевые особенности:**
- Поставки создаются в **ND-40**, переводятся целиком в **IM-40**
- Расчет себестоимости с детальной разбивкой всех компонентов
- Распределение абсолютных затрат по количеству товара (шт)
- Процентные затраты применяются к цене в суммах (UZS)
- История расчетов с возможностью финализации

---

## Модели данных

### RegisterType (enum)
- `ND40 = 1` - регистр ND-40
- `IM40 = 2` - регистр IM-40

### SupplyStatus (enum)
- `HasStock = 1` - Есть товар
- `Finished = 2` - Поставка закончилась

### Supply
```json
{
  "id": 1,
  "code": "ГТД-001", // № ГТД, обязательно
  "registerType": 1, // ND40 или IM40
  "status": 1, // HasStock или Finished
  "createdAt": "2025-01-24T12:00:00Z",
  "updatedAt": "2025-01-24T12:00:00Z",
  "items": [...] // SupplyItem[]
}
```

### SupplyItem
```json
{
  "id": 1,
  "supplyId": 1,
  "productId": 5, // FK на Product
  "name": "Огнетушитель OP-2", // snapshot названия
  "quantity": 100, // шт
  "priceRub": 3840.00 // за 1 шт в рублях
}
```

### CostingSession
```json
{
  "id": 1,
  "supplyId": 1,
  "exchangeRate": 158.08, // курс RUB→UZS
  
  // Проценты (к «цена сум»)
  "vatPct": 0.22, // НДС 22%
  "logisticsPct": 0.01, // Логистика
  "storagePct": 0.005, // Склад
  "declarationPct": 0.01, // Декларация
  "certificationPct": 0.01, // Сертификация
  "mChsPct": 0.005, // МЧС
  "unforeseenPct": 0.015, // Непредвиденные
  
  // Абсолюты (UZS), распределяются по кол-ву
  "customsFeeAbs": 105000.00, // Таможня
  "loadingAbs": 10000.00, // Погрузка
  "returnsAbs": 5000.00, // Возврат
  
  "apportionMethod": 1, // ByQuantity
  "isFinalized": false, // после фикса — read-only
  "createdAt": "2025-01-24T12:00:00Z"
}
```

### CostingItemSnapshot
```json
{
  "id": 1,
  "costingSessionId": 1,
  "supplyItemId": 1,
  "name": "Огнетушитель OP-2",
  "quantity": 100,
  "priceRub": 3840.00,
  "priceUzs": 607027.20, // PriceRub * ExchangeRate
  
  // Процентные (к PriceUzs)
  "vatUzs": 133546.00,
  "logisticsUzs": 6070.27,
  "storageUzs": 3035.14,
  "declarationUzs": 6070.27,
  "certificationUzs": 6070.27,
  "mChsUzs": 3035.14,
  "unforeseenUzs": 9105.41,
  
  // Абсолюты (распределены)
  "customsUzs": 105000.00,
  "loadingUzs": 10000.00,
  "returnsUzs": 5000.00,
  
  "totalCostUzs": 893959.70, // Итог
  "unitCostUzs": 8939.60 // за 1 шт
}
```

---

## API Endpoints

### 1. Supplies (Поставки)

#### GET `/api/supplies`
Получить список поставок с фильтром

**Query параметры:**
- `registerType` (optional): `ND40` или `IM40`

**Response:**
```json
[
  {
    "id": 1,
    "code": "ГТД-001",
    "registerType": 1,
    "status": 1,
    "createdAt": "2025-01-24T12:00:00Z",
    "updatedAt": "2025-01-24T12:00:00Z",
    "items": [...]
  }
]
```

**Сортировка:** `Finished` внизу, `HasStock` сверху

---

#### POST `/api/supplies`
Создать новую поставку (всегда в ND-40)

**Request:**
```json
{
  "code": "ГТД-001" // обязательно, уникальный
}
```

**Response:** `201 Created`
```json
{
  "id": 1,
  "code": "ГТД-001",
  "registerType": 1, // ND40
  "status": 1, // HasStock
  "createdAt": "2025-01-24T12:00:00Z",
  "updatedAt": "2025-01-24T12:00:00Z"
}
```

---

#### GET `/api/supplies/{id}`
Получить поставку по ID

**Response:**
```json
{
  "id": 1,
  "code": "ГТД-001",
  "registerType": 1,
  "status": 1,
  "items": [...],
  "costingSessions": [...]
}
```

---

#### PUT `/api/supplies/{id}`
Обновить поставку (только для ND-40)

**Request:**
```json
{
  "code": "ГТД-002" // optional
}
```

**Response:** `204 No Content`

**Ограничения:**
- После перевода в IM-40 - read-only (400 Bad Request)

---

#### DELETE `/api/supplies/{id}`
Удалить поставку

**Response:** `204 No Content`

---

#### POST `/api/supplies/{id}/transfer-to-im40`
Перевод поставки целиком из ND-40 в IM-40

**Response:** `204 No Content`

**Ограничения:**
- Только для поставок в ND-40
- После перевода - read-only

---

#### PUT `/api/supplies/{id}/status`
Изменить статус поставки

**Request:**
```json
{
  "status": 2 // 1=HasStock, 2=Finished
}
```

**Response:** `204 No Content`

---

### 2. SupplyItems (Позиции поставки)

#### GET `/api/supplies/{supplyId}/items`
Получить все позиции поставки

**Response:**
```json
[
  {
    "id": 1,
    "supplyId": 1,
    "productId": 5,
    "name": "Огнетушитель OP-2",
    "quantity": 100,
    "priceRub": 3840.00,
    "product": {
      "id": 5,
      "name": "Огнетушитель OP-2",
      "sku": "OP2-RUS",
      ...
    }
  }
]
```

---

#### POST `/api/supplies/{supplyId}/items`
Добавить позицию в поставку

**Request:**
```json
{
  "name": "Огнетушитель OP-2", // обязательно
  "quantity": 100, // шт
  "priceRub": 3840.00, // за 1 шт в рублях
  "category": "Огнетушители" // optional
}
```

**Логика:**
- Если продукт с таким названием существует → используем его `ProductId`
- Иначе создаём новый продукт

**Response:** `201 Created`

**Ограничения:**
- После перевода в IM-40 - read-only (400 Bad Request)

---

#### PUT `/api/supplies/{supplyId}/items/{itemId}`
Обновить позицию

**Request:**
```json
{
  "quantity": 150, // optional
  "priceRub": 4000.00 // optional
}
```

**Response:** `204 No Content`

---

#### DELETE `/api/supplies/{supplyId}/items/{itemId}`
Удалить позицию

**Response:** `204 No Content`

---

### 3. Costing (Расчет себестоимости)

#### GET `/api/costing/sessions`
Получить список сессий расчета

**Query параметры:**
- `supplyId` (optional): фильтр по поставке

**Response:**
```json
[
  {
    "id": 1,
    "supplyId": 1,
    "exchangeRate": 158.08,
    "vatPct": 0.22,
    ...
    "isFinalized": false,
    "createdAt": "2025-01-24T12:00:00Z"
  }
]
```

---

#### GET `/api/costing/sessions/{id}`
Получить детали сессии со снапшотами

**Response:**
```json
{
  "session": {
    "id": 1,
    "supplyId": 1,
    "exchangeRate": 158.08,
    ...
  },
  "snapshots": [
    {
      "id": 1,
      "name": "Огнетушитель OP-2",
      "quantity": 100,
      "priceRub": 3840.00,
      "priceUzs": 607027.20,
      "vatUzs": 133546.00,
      ...
      "totalCostUzs": 893959.70,
      "unitCostUzs": 8939.60
    }
  ],
  "grandTotal": 893959.70
}
```

---

#### POST `/api/costing/sessions`
Создать новую сессию расчета

**Request:**
```json
{
  "supplyId": 1,
  "exchangeRate": 158.08, // обязательно > 0
  "vatPct": 0.22, // 0.22 = 22%
  "logisticsPct": 0.01,
  "storagePct": 0.005,
  "declarationPct": 0.01,
  "certificationPct": 0.01,
  "mChsPct": 0.005,
  "unforeseenPct": 0.015,
  "customsFeeAbs": 105000.00, // UZS
  "loadingAbs": 10000.00, // UZS
  "returnsAbs": 5000.00 // UZS
}
```

**Response:** `201 Created`

---

#### POST `/api/costing/sessions/{id}/recalculate`
Пересчитать снапшоты для сессии

**Response:**
```json
{
  "success": true,
  "snapshotsCount": 5,
  "grandTotal": 4469798.50,
  "invariantValid": true
}
```

**Логика:**
1. Удаляет старые снапшоты
2. Рассчитывает новые по формулам
3. Проверяет инвариант сумм абсолютов
4. Сохраняет в БД

**Ограничения:**
- Нельзя пересчитать финализированную сессию (400 Bad Request)

---

#### POST `/api/costing/sessions/{id}/finalize`
Зафиксировать расчет (после этого - только чтение)

**Response:** `204 No Content`

**Требования:**
- Должны быть рассчитаны снапшоты
- После финализации - нельзя изменять

---

## Формулы расчета

### 1. Цена в суммах
```
PriceUzs = PriceRub × ExchangeRate
```

### 2. Процентные статьи (к PriceUzs)
```
VatUzs = PriceUzs × VatPct
LogisticsUzs = PriceUzs × LogisticsPct
StorageUzs = PriceUzs × StoragePct
DeclarationUzs = PriceUzs × DeclarationPct
CertificationUzs = PriceUzs × CertificationPct
MChsUzs = PriceUzs × MChsPct
UnforeseenUzs = PriceUzs × UnforeseenPct
```

### 3. Абсолюты (распределение по количеству)
```
Share = Quantity_pos / ΣQuantity_all

CustomsUzs_pos = CustomsFeeAbs × Share
LoadingUzs_pos = LoadingAbs × Share
ReturnsUzs_pos = ReturnsAbs × Share
```

**Инвариант:** Σраспределённых по всем позициям = исходной абсолютной сумме

### 4. Итоги
```
TotalCostUzs = PriceUzs + Σ(процентные) + Σ(абсолюты)

UnitCostUzs = TotalCostUzs / Quantity
```

---

## Пример полного флоу

### 1. Создать поставку
```http
POST /api/supplies
{
  "code": "ГТД-123"
}
```

### 2. Добавить позиции
```http
POST /api/supplies/1/items
{
  "name": "Огнетушитель OP-2",
  "quantity": 100,
  "priceRub": 3840.00
}

POST /api/supplies/1/items
{
  "name": "Огнетушитель OP-4",
  "quantity": 50,
  "priceRub": 5200.00
}
```

### 3. Создать сессию расчета
```http
POST /api/costing/sessions
{
  "supplyId": 1,
  "exchangeRate": 158.08,
  "vatPct": 0.22,
  "logisticsPct": 0.01,
  "storagePct": 0.005,
  "declarationPct": 0.01,
  "certificationPct": 0.01,
  "mChsPct": 0.005,
  "unforeseenPct": 0.015,
  "customsFeeAbs": 105000.00,
  "loadingAbs": 10000.00,
  "returnsAbs": 5000.00
}
```

### 4. Пересчитать
```http
POST /api/costing/sessions/1/recalculate
```

### 5. Просмотреть результат
```http
GET /api/costing/sessions/1
```

### 6. Зафиксировать
```http
POST /api/costing/sessions/1/finalize
```

### 7. Перевести в IM-40
```http
POST /api/supplies/1/transfer-to-im40
```

---

## Валидация и ограничения

### Поставки:
- `Code` обязателен и уникален
- Создаются только в ND-40
- После перевода в IM-40 - read-only (кроме статуса)

### Позиции:
- `Quantity` > 0
- `PriceRub` >= 0
- Нельзя добавлять/редактировать после перевода в IM-40

### Расчет:
- `ExchangeRate` > 0
- Все проценты >= 0
- Все абсолюты >= 0
- Нельзя пересчитывать финализированную сессию
- После финализации - только чтение

---

## Статус коды

- `200 OK` - успешный запрос
- `201 Created` - создан ресурс
- `204 No Content` - успешно, нет тела ответа
- `400 Bad Request` - ошибка валидации
- `404 Not Found` - ресурс не найден
- `401 Unauthorized` - требуется авторизация

---

## Авторизация

Все эндпоинты требуют:
```
Authorization: Bearer {JWT_TOKEN}
Policy: AdminOnly
```

---

**Версия:** 1.0  
**Дата:** 24 января 2025
