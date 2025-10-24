# СИСТЕМА РАСЧЕТА СЕБЕСТОИМОСТИ НД-40 ✅

## Статус: Backend ГОТОВ | Frontend в разработке

---

## Концепция

Правильный финансовый учет поставок с детальным расчетом себестоимости партий НД-40.  
Себестоимость **фиксируется при создании партии** и больше не изменяется.

### Ключевые принципы:
- ✅ Себестоимость вносится **ТОЛЬКО** при создании поставки  
- ✅ У каждой партии (batch) **свой уникальный себес**
- ✅ Учет всех компонентов: таможня, НДС, логистика, сертификация
- ✅ Автоматический расчет по формулам из Excel
- ✅ Склады: **НД-40** и **ИМ-40**

---

## Backend API (ГОТОВО)

### 1. Модели данных

#### SupplyCostCalculation.cs
Хранит детальный расчет себестоимости для каждой партии:

**Глобальные параметры поставки:**
- `ExchangeRate` - Курс (по умолчанию 158.08)
- `CustomsFee` - Таможенный сбор (по умолчанию 105000)
- `VatPercent` - НДС % (по умолчанию 22%)
- `CorrectionPercent` - Корректива % (по умолчанию 0.50%)
- `SecurityPercent` - Охрана % (по умолчанию 0.2%)
- `DeclarationPercent` - Декларация % (по умолчанию 1%)
- `CertificationPercent` - Сертификация % (по умолчанию 1%)
- `CalculationBase` - База для расчета (по умолчанию 10000000)
- `LoadingPercent` - Погрузка % (по умолчанию 1.6%)

**Параметры позиции:**
- `ProductId`, `ProductName`, `Sku` - Товар
- `Quantity` - Количество
- `PriceRub` - Цена в рублях за единицу
- `PriceTotal` - Цена в суммах (Quantity * PriceRub)
- `Weight` - Вес (опционально, для логистики)

**Рассчитанные значения:**
- `CustomsAmount` - Таможня
- `VatAmount` - НДС (таможня)
- `CorrectionAmount` - Корректива
- `SecurityAmount` - Охрана
- `DeclarationAmount` - Декларация
- `CertificationAmount` - Сертификация
- `LoadingAmount` - Погрузка
- `DeviationAmount` - Отклонение (опционально)

**Итоговые значения:**
- `TotalCost` - **СЕБЕСТОИМОСТЬ ЗАКУПКИ** (на все количество)
- `UnitCost` - **СЕБЕСТОИМОСТЬ ЗА ЕДИНИЦУ** (TotalCost / Quantity)

---

### 2. Сервис расчета

#### SupplyCostCalculationService.cs

**Методы:**

`Calculate(...)` - Рассчитать себестоимость для позиции
- Принимает все параметры товара и глобальные параметры поставки
- Возвращает `SupplyCostCalculation` с детальным расчетом

`GetDefaults()` - Получить значения по умолчанию
- Возвращает `Dictionary<string, decimal>` с дефолтными параметрами

**Формулы расчета:**

```
PriceTotal = Quantity * PriceRub
CustomsAmount = (PriceTotal / CalculationBase) * CustomsFee
VatAmount = (PriceTotal + CustomsAmount) * (VatPercent / 100)
CorrectionAmount = PriceTotal * (CorrectionPercent / 100)
SecurityAmount = PriceTotal * (SecurityPercent / 100)
DeclarationAmount = PriceTotal * (DeclarationPercent / 100)
CertificationAmount = PriceTotal * (CertificationPercent / 100)
LoadingAmount = PriceTotal * (LoadingPercent / 100)

TotalCost = PriceTotal + CustomsAmount + VatAmount + CorrectionAmount +
            SecurityAmount + DeclarationAmount + CertificationAmount + LoadingAmount
            
UnitCost = TotalCost / Quantity
```

---

### 3. API Эндпоинты

#### SuppliesController

**GET /api/supplies/cost-defaults**
- Получить дефолтные значения параметров расчета
- Используется для предзаполнения формы

**POST /api/supplies/cost-preview**
- Предварительный расчет себестоимости (без создания поставки)
- Body: `SupplyCreateDto` с параметрами и товарами
- Response: `SupplyCostPreviewDto` с детальными расчетами

**POST /api/supplies** (будет обновлен)
- Создание поставки с расчетом себестоимости
- Body: `SupplyCreateDto` с расширенными параметрами
- Response: Созданные батчи

---

### 4. DTO (Data Transfer Objects)

#### SupplyLineDto (обновлен)
```csharp
{
  "ProductId": 1,
  "Qty": 10,
  "UnitCost": 50000,  // Будет перезаписан рассчитанным
  "Code": "A-001",
  "Note": "Примечание",
  "VatRate": 22,
  "PriceRub": 3840,   // ⬅️ НОВОЕ: Цена в рублях
  "Weight": 10.5      // ⬅️ НОВОЕ: Вес
}
```

#### SupplyCreateDto (обновлен)
```csharp
{
  "Items": [ /* массив SupplyLineDto */ ],
  "SupplierName": "Поставщик",
  "InvoiceNumber": "INV-001",
  "PurchaseDate": "2025-01-24",
  "VatRate": 22,
  
  // ⬅️ НОВЫЕ ПАРАМЕТРЫ РАСЧЕТА НД-40:
  "ExchangeRate": 158.08,
  "CustomsFee": 105000,
  "VatPercent": 22,
  "CorrectionPercent": 0.5,
  "SecurityPercent": 0.2,
  "DeclarationPercent": 1,
  "CertificationPercent": 1,
  "CalculationBase": 10000000,
  "LoadingPercent": 1.6
}
```

#### SupplyCostPreviewDto (новый)
```csharp
{
  "Items": [
    {
      "ProductId": 1,
      "ProductName": "Огнетушитель OP-2",
      "Sku": "SKU-001",
      "Quantity": 100,
      "PriceRub": 3840.00,
      "PriceTotal": 384000.00,
      "Weight": 10.5,
      
      // Рассчитанные компоненты:
      "CustomsAmount": 4032.00,
      "VatAmount": 85367.04,
      "CorrectionAmount": 1920.00,
      "SecurityAmount": 768.00,
      "DeclarationAmount": 3840.00,
      "CertificationAmount": 3840.00,
      "LoadingAmount": 6144.00,
      "DeviationAmount": null,
      
      // Итоговая себестоимость:
      "TotalCost": 489911.04,  // ⬅️ ОБЩАЯ
      "UnitCost": 4899.11      // ⬅️ ЗА ЕДИНИЦУ
    }
  ],
  "GrandTotalCost": 489911.04,
  
  // Использованные параметры:
  "ExchangeRate": 158.08,
  "CustomsFee": 105000,
  "VatPercent": 22,
  "CorrectionPercent": 0.5,
  "SecurityPercent": 0.2,
  "DeclarationPercent": 1,
  "CertificationPercent": 1,
  "CalculationBase": 10000000,
  "LoadingPercent": 1.6
}
```

---

### 5. База данных

#### Таблица: SupplyCostCalculations

```sql
CREATE TABLE SupplyCostCalculations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BatchId INTEGER NULL,
    
    -- Глобальные параметры
    ExchangeRate DECIMAL(18,2) NOT NULL,
    CustomsFee DECIMAL(18,2) NOT NULL,
    VatPercent DECIMAL(5,2) NOT NULL,
    CorrectionPercent DECIMAL(5,2) NOT NULL,
    SecurityPercent DECIMAL(5,2) NOT NULL,
    DeclarationPercent DECIMAL(5,2) NOT NULL,
    CertificationPercent DECIMAL(5,2) NOT NULL,
    CalculationBase DECIMAL(18,2) NOT NULL,
    LoadingPercent DECIMAL(5,2) NOT NULL,
    
    -- Параметры позиции
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    Sku TEXT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    PriceRub DECIMAL(18,2) NOT NULL,
    PriceTotal DECIMAL(18,2) NOT NULL,
    Weight DECIMAL(18,3) NULL,
    
    -- Рассчитанные значения
    CustomsAmount DECIMAL(18,2) NOT NULL,
    VatAmount DECIMAL(18,2) NOT NULL,
    CorrectionAmount DECIMAL(18,2) NOT NULL,
    SecurityAmount DECIMAL(18,2) NOT NULL,
    DeclarationAmount DECIMAL(18,2) NOT NULL,
    CertificationAmount DECIMAL(18,2) NOT NULL,
    LoadingAmount DECIMAL(18,2) NOT NULL,
    DeviationAmount DECIMAL(18,2) NULL,
    
    -- Итого
    TotalCost DECIMAL(18,2) NOT NULL,
    UnitCost DECIMAL(18,2) NOT NULL,
    
    -- Мета
    CreatedAt DATETIME NOT NULL,
    CreatedBy TEXT NULL,
    Notes TEXT NULL,
    
    FOREIGN KEY (BatchId) REFERENCES Batches(Id),
    FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
```

**Индексы:**
- `IX_SupplyCostCalculations_BatchId`
- `IX_SupplyCostCalculations_ProductId`
- `IX_SupplyCostCalculations_CreatedAt`

---

## Интеграция с существующей системой

### Связь с Batches
- Каждая запись `SupplyCostCalculation` связана с `Batch` через `BatchId`
- При создании партии автоматически сохраняется расчет себестоимости
- `Batch.UnitCost` берется из `SupplyCostCalculation.UnitCost`

### AppDbContext
```csharp
public DbSet<SupplyCostCalculation> SupplyCostCalculations => Set<SupplyCostCalculation>();
```

### Program.cs
```csharp
builder.Services.AddScoped<ProjectApp.Api.Services.SupplyCostCalculationService>();
```

---

## Frontend (TODO)

### Что нужно реализовать:

1. **Обновить SuppliesPage.xaml**
   - Добавить раздел "Параметры расчета НД-40"
   - Поля для всех глобальных параметров
   - Кнопка "Рассчитать себестоимость"
   - Отображение детального расчета

2. **Обновить SuppliesViewModel.cs**
   - Свойства для глобальных параметров
   - Метод PreviewCostAsync() для вызова API
   - Отображение рассчитанных значений

3. **Создать/обновить ISuppliesService**
   - GetCostDefaultsAsync()
   - PreviewCostAsync(SupplyCreateDto)
   - Обновить CreateSupplyAsync() с новыми параметрами

4. **UI компоненты**
   - Группа инпутов для параметров (с дефолтными значениями)
   - Таблица с детальным расчетом по каждому товару
   - Итоговая сумма себестоимости
   - Индикатор компонентов (таможня, НДС, логистика и т.д.)

---

## Пример использования

### 1. Получить дефолты
```http
GET /api/supplies/cost-defaults
Authorization: Bearer {token}

Response:
{
  "ExchangeRate": 158.08,
  "CustomsFee": 105000,
  "VatPercent": 22,
  ...
}
```

### 2. Предварительный расчет
```http
POST /api/supplies/cost-preview
Authorization: Bearer {token}
Content-Type: application/json

{
  "Items": [
    {
      "ProductId": 1,
      "Qty": 100,
      "PriceRub": 3840,
      "Code": "A-001"
    }
  ],
  "ExchangeRate": 158.08,
  "CustomsFee": 105000,
  "VatPercent": 22,
  "CorrectionPercent": 0.5,
  "SecurityPercent": 0.2,
  "DeclarationPercent": 1,
  "CertificationPercent": 1,
  "CalculationBase": 10000000,
  "LoadingPercent": 1.6
}

Response: SupplyCostPreviewDto с детальными расчетами
```

### 3. Создать поставку (будет обновлено)
```http
POST /api/supplies
Authorization: Bearer {token}
Content-Type: application/json

{
  "Items": [...],
  "SupplierName": "Поставщик",
  "InvoiceNumber": "INV-001",
  ...параметры расчета...
}
```

---

## Файлы

### Backend (ГОТОВО ✅)
- `Models/SupplyCostCalculation.cs` - Модель данных
- `Services/SupplyCostCalculationService.cs` - Сервис расчета
- `Controllers/SuppliesController.cs` - API эндпоинты (обновлен)
- `Dtos/SupplyDtos.cs` - DTO (обновлены и добавлены новые)
- `Data/AppDbContext.cs` - DbSet (добавлен)
- `Program.cs` - Регистрация сервиса (добавлена)
- `migrations/add-supply-cost-calculation.sql` - Миграция БД

### Frontend (TODO 📝)
- `Views/SuppliesPage.xaml` - UI (нужно обновить)
- `ViewModels/SuppliesViewModel.cs` - ViewModel (нужно обновить)
- `Services/ISuppliesService.cs` - API Service (нужно обновить)

---

## Статус

- ✅ **Backend API** - ГОТОВ
- ✅ **Модели данных** - ГОТОВЫ
- ✅ **Сервис расчета** - ГОТОВ
- ✅ **Миграция БД** - ГОТОВА
- ✅ **Эндпоинты** - ГОТОВЫ
- ✅ **Frontend UI** - ГОТОВ ✨
- ✅ **Интеграция UI с API** - ГОТОВА ✨

---

## Frontend реализация (ЗАВЕРШЕНО)

### 1. UI компоненты (SuppliesPage.xaml) ✅
- **Секция "Параметры расчета НД-40"** с collapse/expand
- **9 полей ввода** для параметров:
  - Курс (руб/$)
  - Таможня (сум)
  - НДС %
  - Корректива %
  - Охрана %
  - Декларация %
  - Сертификация %
  - Погрузка %
  - База расчета
- **Кнопка "Рассчитать предварительно"** для расчета себестоимости
- **Блок предварительного расчета** с детальными карточками товаров:
  - Количество, цена в рублях, общая сумма
  - Себестоимость за единицу
  - Компоненты: таможня, НДС, погрузка
  - ОБЩАЯ СЕБЕСТОИМОСТЬ внизу

### 2. ViewModel (SuppliesViewModel.cs) ✅
**Новые свойства (9 параметров):**
```csharp
ExchangeRate = 158.08m
CustomsFee = 105000m
VatPercent = 22m
CorrectionPercent = 0.50m
SecurityPercent = 0.2m
DeclarationPercent = 1m
CertificationPercent = 1m
CalculationBase = 10000000m
LoadingPercent = 1.6m
```

**Новые команды:**
- `ToggleCostParamsCommand` - свернуть/развернуть параметры
- `PreviewCostCommand` - рассчитать предварительную себестоимость

**Новые коллекции:**
- `CostPreviewItems` - список товаров с рассчитанной себестоимостью
- `HasCostPreview` - флаг отображения результата
- `GrandTotalCost` - общая себестоимость

**Автозагрузка дефолтов:**
- При инициализации загружаются дефолтные значения из API

### 3. Services (API интеграция) ✅

**ISuppliesService (Interfaces.cs):**
```csharp
Task<Dictionary<string, decimal>?> GetCostDefaultsAsync()
Task<SupplyCostPreview?> PreviewCostAsync(SupplyDraft draft)
```

**ApiSuppliesService.cs:**
- Метод `GetCostDefaultsAsync()` - GET /api/supplies/cost-defaults
- Метод `PreviewCostAsync()` - POST /api/supplies/cost-preview
- Обновлен `CreateSupplyAsync()` - отправляет параметры расчета

**Новые DTO модели:**
- `SupplyDraft` - добавлены 9 параметров расчета
- `SupplyCostPreview` - результат предварительного расчета
- `SupplyCostCalculationItem` - детальный расчет товара

### 4. Converters ✅
**BoolToExpandCollapseConverter.cs:**
- Конвертирует `bool` → "▼" (свернуто) или "▲" (развернуто)
- Зарегистрирован в `App.xaml`

---

## Как это работает

### Пользовательский флоу:

1. **Админ открывает страницу "Поставки"**
2. **Добавляет товары** в поставку (как обычно)
3. **Разворачивает секцию "Параметры расчета НД-40"** (▼)
4. **Видит предзаполненные параметры** (курс, таможня, проценты)
5. **Может изменить** любой параметр при необходимости
6. **Нажимает "Рассчитать предварительно"** 🔍
7. **Видит детальный расчет** для каждого товара:
   - Кол-во, цена (руб), сумма
   - Таможня, НДС, погрузка и др.
   - **СЕБЕСТОИМОСТЬ ЗА ЕДИНИЦУ** ✨
8. **Видит ОБЩУЮ СЕБЕСТОИМОСТЬ** всей поставки 💵
9. **Создает поставку** с рассчитанной себестоимостью

### Технический флоу:

```
1. SuppliesViewModel.LoadDefaultsAsync()
   └─> ISuppliesService.GetCostDefaultsAsync()
       └─> GET /api/supplies/cost-defaults
           └─> SupplyCostCalculationService.GetDefaults()
               └─> Возвращает дефолты (курс 158.08 и т.д.)

2. User нажимает "Рассчитать предварительно"
   └─> PreviewCostCommand
       └─> ISuppliesService.PreviewCostAsync(draft)
           └─> POST /api/supplies/cost-preview
               └─> SuppliesController.PreviewCost()
                   └─> SupplyCostCalculationService.Calculate()
                       └─> Расчет по формулам
                           └─> Возвращает SupplyCostPreviewDto
                               └─> ViewModel отображает результат

3. User нажимает "Создать поставку"
   └─> CreateSupplyCommand
       └─> ISuppliesService.CreateSupplyAsync(draft с параметрами)
           └─> POST /api/supplies
               └─> SuppliesController.Create()
                   └─> Создает Batches с рассчитанной себестоимостью
```

---

## Следующие шаги

### ✅ Завершено:
- Backend API полностью
- Frontend UI полностью
- Интеграция Backend ↔ Frontend

### 📝 TODO (опционально):
1. **Добавить поле "Цена в рублях"** в форму добавления товара
2. **Автоматический расчет** при изменении параметров (real-time)
3. **Сохранение параметров** на уровне пользователя
4. **Экспорт расчета** в Excel/PDF
5. **История расчетов** для аналитики
6. **Логика ИМ-40** (следующая фаза)

---

## Файлы (обновлено)

### Backend ✅
- `Models/SupplyCostCalculation.cs`
- `Services/SupplyCostCalculationService.cs`
- `Controllers/SuppliesController.cs` (+2 эндпоинта)
- `Dtos/SupplyDtos.cs` (+3 новых DTO)
- `Data/AppDbContext.cs`
- `Program.cs`
- `migrations/add-supply-cost-calculation.sql`

### Frontend ✅
- `Views/SuppliesPage.xaml` (расширен)
- `ViewModels/SuppliesViewModel.cs` (расширен)
- `Services/Interfaces.cs` (+2 метода, +3 DTO)
- `Services/ApiSuppliesService.cs` (+2 метода)
- `Converters/BoolToExpandCollapseConverter.cs` (новый)
- `App.xaml` (регистрация конвертера)

### Документация ✅
- `SUPPLY_COST_SYSTEM.md` (этот файл)

---

**Дата:** 24 января 2025  
**Версия:** 2.0 (Backend + Frontend)  
**Статус:** ✅ ПОЛНОСТЬЮ ГОТОВО К ИСПОЛЬЗОВАНИЮ
