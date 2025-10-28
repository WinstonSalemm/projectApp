# 📝 СИСТЕМА ДОГОВОРОВ - КРАТКИЙ ОБЗОР

## 🎯 КОНЦЕПЦИЯ:

Два типа договоров:

### 1️⃣ **ЗАКРЫТЫЙ (Closed)**
- Товар указан в описании, но его НЕТ в каталоге
- Пример: "iPhone 15 Pro - придёт через неделю"
- Договор создается сразу, резервирование НЕ происходит
- Когда товар появится - добавляется в договор и резервируется

### 2️⃣ **ОТКРЫТЫЙ (Open)**
- Указывается ЛИМИТ суммы (например 100 млн)
- Клиент выбирает товары из каталога
- Товар АВТОМАТИЧЕСКИ резервируется из партий (FIFO)
- Прогресс-бары показывают использование лимита

---

## ✨ КЛЮЧЕВЫЕ ФИЧИ:

### ✅ Резервирование товара:
- При добавлении товара в договор → списывается из партий (ND-40 или IM-40)
- Используется FIFO (сначала старые партии)
- Создаются записи в `ContractReservations`

### ✅ Отмена договора:
- Товар ВОЗВРАЩАЕТСЯ обратно в партии
- Восстанавливается количество на складе
- Все резервы помечаются как возвращенные

### ✅ Закрытие договора:
**Проверки перед закрытием:**
1. Полностью оплачен (PaidAmount = TotalAmount)
2. Полностью отгружен (ShippedAmount = TotalAmount)
3. Все партии переведены в IM-40

Если хоть одна проверка не пройдена → ошибка с понятным сообщением.

---

## 🔌 API ENDPOINTS:

```
GET    /api/contracts                    - Список договоров
GET    /api/contracts/{id}               - Детали договора
POST   /api/contracts                    - Создать договор
POST   /api/contracts/{id}/items         - Добавить товар
POST   /api/contracts/{id}/payments      - Добавить оплату
POST   /api/contracts/{id}/deliveries    - Отгрузить товар
POST   /api/contracts/{id}/close         - Закрыть договор
POST   /api/contracts/{id}/cancel        - Отменить (вернуть товар)
```

---

## 📊 МОДЕЛИ:

**Contract:**
- Type (Closed/Open)
- ContractNumber
- ClientId
- Description (для закрытых)
- TotalAmount, PaidAmount, ShippedAmount
- Status (Active/Closed/Cancelled)

**ContractItem:**
- ProductId (nullable для товаров которых нет)
- Description (для будущих товаров)
- Qty, UnitPrice
- Status (Reserved/Shipped/Cancelled)

**ContractReservation:**
- ContractItemId
- BatchId (из какой партии взяли)
- ReservedQty
- ReturnedAt (когда вернули)

---

## 🎨 MAUI UI:

**ContractsPage (базовая версия):**
- Список договоров с прогресс-барами
- Цветные статусы
- Клик → детали
- Обновление списка

**TODO (полная версия):**
- Форма создания договора
- Добавление товаров из каталога
- Кнопки оплаты/отгрузки
- История изменений

---

## 🧪 БЫСТРЫЙ ТЕСТ:

1. **Создай договор:** POST /api/contracts
2. **Добавь товар:** POST /api/contracts/1/items
3. **Проверь резервы:** SELECT * FROM ContractReservations
4. **Отмени:** POST /api/contracts/1/cancel
5. **Проверь что вернулось:** SELECT * FROM Batches

---

## 📁 ФАЙЛЫ:

**Backend:**
- Models/Contract.cs, ContractItem.cs, ContractReservation.cs
- Models/ContractType.cs, ContractItemStatus.cs
- Services/ContractReservationService.cs
- Controllers/ContractsController.cs (обновлен)
- migrations/add-contract-enhancements-v2-mysql.sql

**Frontend:**
- Views/ContractsPage.xaml
- ViewModels/ContractsViewModel.cs
- Services/ApiContractsService.cs (обновлен)

**Docs:**
- CONTRACT_TESTING_GUIDE.md - подробная инструкция
- CONTRACTS_README.md - этот файл

---

## 🚀 СТАТУС: 80% ГОТОВО

- ✅ Backend API - 100%
- ✅ Резервирование - 100%
- ✅ Отмена/возврат - 100%
- ✅ Проверки - 100%
- ⚠️ MAUI UI - 30% (просмотр работает, создание/редактирование нет)
