# Return Source Selector - Summary

## Проблема
При нажатии на "Возврат" в способах продаж приложение крашилось.

## Решение
Создана новая страница **ReturnSourceSelectorPage** для выбора операций, по которым можно сделать возврат.

## Что показывается
✅ **Продажи** (только нужные типы):
- Нал с чеком (CashWithReceipt)
- Нал без чека (CashNoReceipt)
- Карта с чеком (CardWithReceipt)
- Click с чеком (ClickWithReceipt)
- Click без чека (ClickNoReceipt)
- Договор (Contract) - продажи по договору

✅ **Договора** (Contracts):
- Все договора в системе

❌ **НЕ показывается:**
- Брони (Reservation)
- Сайт (Site)
- Payme

## Файлы созданы

### Models
- `Models/ReturnSourceItem.cs` - единая модель для отображения продаж и договоров

### ViewModels
- `ViewModels/ReturnSourceSelectorViewModel.cs` - логика страницы
  - Загрузка продаж через `ApiSalesService`
  - Загрузка договоров через `ApiContractsService`
  - Фильтрация по типам оплаты
  - Фильтрация по датам
  - Сортировка по дате (новые сверху)

### Views
- `Views/ReturnSourceSelectorPage.xaml` - UI страницы
- `Views/ReturnSourceSelectorPage.xaml.cs` - code-behind

### Services
- `Services/ApiContractsService.cs` - добавлен метод `GetContractsAsync()`
  - Загружает договора с фильтрацией по датам (на клиенте)
  - Сделан `ContractDto` публичным

## Файлы изменены

### Dependency Injection
- `MauiProgram.cs` - зарегистрированы:
  - `ReturnSourceSelectorViewModel`
  - `ReturnSourceSelectorPage`

### Navigation
- `Views/SaleStartPage.xaml.cs` - изменена навигация:
  - **Было:** открывалась `SalesHistoryPage`
  - **Стало:** открывается `ReturnSourceSelectorPage`

## UI/UX

### Фильтры
- Даты "От" и "До" (по умолчанию: последние 31 день)
- Кнопка "🔍 Загрузить" для применения фильтров

### Отображение элементов
Каждый элемент показывает:
- **Заголовок:** "Продажа #123 - 500,000 сум" или "Договор #DOG-001 - 1,000,000 сум"
- **Подзаголовок:** "Клиент • 28.01.2025 14:30"
- **Бейдж:** Тип оплаты ("Нал с чеком", "Карта с чеком", "Договор" и т.д.)

### Взаимодействие
- **Нажатие на продажу:** открывается `ReturnForSalePage` (существующая)
- **Нажатие на договор:** показывается "В разработке" (TODO)

## Текущий статус

✅ **Готово:**
- UI страницы выбора
- Загрузка продаж
- Загрузка договоров
- Фильтрация по типам оплаты
- Фильтрация по датам
- Возврат по продажам (работает)

⚠️ **В разработке:**
- Возврат по договорам (показывается placeholder)

## Что дальше

Чтобы добавить возврат по договорам, нужно:
1. Создать backend API для возвратов по договорам (аналогично `/api/returns` для продаж)
2. Создать страницу `ReturnForContractPage` (аналогично `ReturnForSalePage`)
3. Обновить `ReturnSourceSelectorViewModel.OpenReturnAsync()` для открытия новой страницы

## Как тестировать

1. Открыть приложение
2. Выбрать "Возврат" в способах продаж
3. **Должна открыться страница со списком операций** (вместо краша)
4. Изменить даты и нажать "Загрузить"
5. Нажать на любую продажу → откроется страница возврата
6. Нажать на договор → показывается "В разработке"

## Логи для отладки

В Debug output будут логи:
```
[SaleStartPage] Getting ReturnSourceSelectorPage from DI...
[SaleStartPage] ReturnSourceSelectorPage created successfully
[ReturnSourceSelectorViewModel] LoadAsync START
[ReturnSourceSelectorViewModel] Loading sales...
[ReturnSourceSelectorViewModel] Loading contracts...
[ReturnSourceSelectorViewModel] Loaded N items
```

## Статус: ✅ ГОТОВО

Краш исправлен, страница работает, можно делать возвраты по продажам.
