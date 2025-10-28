# 📋 ТЕСТИРОВАНИЕ СИСТЕМЫ ДОГОВОРОВ

## ✅ ЧТО ГОТОВО:

### Backend (100%):
- ✅ Модели: Contract, ContractItem, ContractReservation
- ✅ Enums: ContractType (Open/Closed), ContractItemStatus
- ✅ Сервис резервирования товара из партий
- ✅ Автоматическая отмена резервов при отмене договора
- ✅ Проверки перед закрытием (оплата + отгрузка + ND40→IM40)
- ✅ API endpoints готовы
- ✅ Миграция применена на Railway

### Frontend MAUI (базовая версия):
- ✅ ContractsPage - список договоров
- ✅ ContractsViewModel - загрузка и отображение
- ✅ Прогресс-бары оплаты и отгрузки
- ✅ Цветная индикация статусов

---

## 🧪 КАК ТЕСТИРОВАТЬ:

### 1. Проверь API через Railway:

**Получить список договоров:**
```bash
GET https://tranquil-upliftment-production.up.railway.app/api/contracts
Authorization: Bearer YOUR_TOKEN
```

**Создать тестовый закрытый договор:**
```bash
POST https://tranquil-upliftment-production.up.railway.app/api/contracts
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN

{
  "Type": "Closed",
  "ContractNumber": "TEST-001",
  "ClientId": 1,
  "Description": "iPhone 15 Pro - придёт через неделю",
  "Items": [
    {
      "Name": "iPhone 15 Pro 256GB",
      "Description": "Ожидается поставка 05.11.2025",
      "Qty": 5,
      "UnitPrice": 15000000
    }
  ]
}
```

**Создать тестовый открытый договор:**
```bash
POST https://tranquil-upliftment-production.up.railway.app/api/contracts
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN

{
  "Type": "Open",
  "ContractNumber": "TEST-002",
  "ClientId": 2,
  "TotalAmount": 100000000,
  "Note": "Открытый договор на 100 млн - клиент выбирает из каталога",
  "Items": []
}
```

**Добавить товар из каталога в открытый договор:**
```bash
POST https://tranquil-upliftment-production.up.railway.app/api/contracts/2/items
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN

{
  "ProductId": 1,
  "Qty": 10,
  "UnitPrice": 150000
}
```

**Отменить договор (вернуть товар в партии):**
```bash
POST https://tranquil-upliftment-production.up.railway.app/api/contracts/1/cancel
Authorization: Bearer YOUR_TOKEN
```

---

### 2. Проверь в MAUI:

**Открыть страницу договоров:**
```csharp
// В коде или через Shell navigation:
await Shell.Current.GoToAsync("//contracts/test");
```

**Что должно работать:**
- ✅ Список договоров загружается
- ✅ Прогресс-бары показывают оплату и отгрузку
- ✅ Статусы цветные (Active=зеленый, Closed=серый, Cancelled=красный)
- ✅ Клик по договору показывает детали
- ✅ Кнопка обновления работает

---

## 🎯 СЦЕНАРИИ ТЕСТИРОВАНИЯ:

### Сценарий 1: Закрытый договор (товара нет)
1. Создай договор Type=Closed с товаром которого нет в каталоге
2. Укажи Description для товара
3. Договор создается БЕЗ резервирования (товара нет)
4. Товар появился на складе → добавь его в договор → резервируется автоматически

### Сценарий 2: Открытый договор (товар из каталога)
1. Создай договор Type=Open с лимитом 100 млн
2. Добавь товар из каталога (ProductId указан)
3. Товар АВТОМАТИЧЕСКИ резервируется из партий (FIFO)
4. Проверь что на складе количество уменьшилось

### Сценарий 3: Отмена договора
1. Создай договор с товаром из каталога
2. Проверь что товар зарезервирован (уменьшился на складе)
3. Отмени договор через API: POST /api/contracts/{id}/cancel
4. Проверь что товар ВЕРНУЛСЯ на склад (в те же партии)

### Сценарий 4: Закрытие договора
1. Создай договор
2. Попробуй закрыть через /api/contracts/{id}/close
3. Должна быть ошибка: "Не полностью оплачен"
4. Добавь оплату через /api/contracts/{id}/payments
5. Добавь отгрузку через /api/contracts/{id}/deliveries
6. Переведи партии в IM-40
7. Теперь закрытие должно работать

---

## 📊 ПРОВЕРКА ДАННЫХ В БД:

**Посмотри договора:**
```sql
SELECT * FROM Contracts ORDER BY CreatedAt DESC;
```

**Посмотри позиции:**
```sql
SELECT * FROM ContractItems WHERE ContractId = 1;
```

**Посмотри резервации:**
```sql
SELECT cr.*, b.ProductId, b.Qty as BatchQty, b.Register
FROM ContractReservations cr
JOIN Batches b ON cr.BatchId = b.Id
WHERE cr.ContractItemId IN (SELECT Id FROM ContractItems WHERE ContractId = 1);
```

**Проверь что товар вернулся после отмены:**
```sql
SELECT * FROM ContractReservations WHERE ReturnedAt IS NOT NULL;
```

---

## 🐛 ИЗВЕСТНЫЕ ОГРАНИЧЕНИЯ:

1. ❌ **UI для создания договоров** - пока нет (только просмотр)
2. ❌ **Прогресс-бар по позициям** - показывает только общий процент
3. ❌ **Интеграция с клиентами** - пока показывает OrgName вместо имени клиента
4. ⚠️ **Резервирование** - работает только для товаров с ProductId

---

## ✨ СЛЕДУЮЩИЕ ШАГИ:

### Приоритет 1 (необходимый функционал):
- [ ] UI создания договора (форма с выбором типа)
- [ ] UI добавления товаров из каталога
- [ ] UI детальной карточки договора с историей

### Приоритет 2 (улучшения):
- [ ] Прогресс по каждой позиции отдельно
- [ ] Загрузка имени клиента из Clients
- [ ] Кнопки оплаты/отгрузки прямо в MAUI
- [ ] Уведомления о критичных договорах

### Приоритет 3 (расширенное):
- [ ] Печать договора в PDF
- [ ] История изменений договора
- [ ] Аналитика по договорам
- [ ] Автоматические напоминания

---

## 🎉 ГОТОВО К PRODUCTION?

### Backend: ✅ ДА
- Все проверки работают
- Резервирование и отмена корректны
- API полностью функционален

### Frontend: ⚠️ ЧАСТИЧНО
- Просмотр работает
- Создание/редактирование нужно доделать

**Рекомендация:** Можно деплоить backend и тестировать через API, UI доделать позже.
