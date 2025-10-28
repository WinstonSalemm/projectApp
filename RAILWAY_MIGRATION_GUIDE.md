# 🚂 Применение миграции на Railway MySQL

## Способ 1: Через Railway Dashboard (РЕКОМЕНДУЕТСЯ)

1. Открой **Railway Dashboard** → твой проект
2. Перейди в **MySQL Plugin** → **Data** вкладка
3. Нажми **Query**
4. Скопируй и выполни содержимое файла `migrations/add-contract-enhancements-v2-mysql.sql`

---

## Способ 2: Через Railway CLI

```bash
# 1. Подключись к базе
railway connect MySQL

# 2. В открывшемся MySQL клиенте выполни:
source migrations/add-contract-enhancements-v2-mysql.sql;
```

---

## Способ 3: Через переменные окружения

```bash
# Получи DATABASE_URL
railway variables

# Подключись через mysql клиент
mysql -h <host> -u <user> -p<password> <database> < migrations/add-contract-enhancements-v2-mysql.sql
```

---

## Проверка успешности

После применения миграции проверь что таблицы обновлены:

```sql
-- Проверь новые колонки в Contracts
DESCRIBE Contracts;

-- Проверь новые колонки в ContractItems  
DESCRIBE ContractItems;

-- Проверь что создалась таблица ContractReservations
SHOW TABLES LIKE 'ContractReservations';
DESCRIBE ContractReservations;
```

---

## 🎯 Что добавляет миграция:

### Contracts:
- `Type` - тип договора (Closed=0, Open=1)
- `ContractNumber` - номер договора
- `ClientId` - ID клиента
- `Description` - описание для закрытых договоров
- `ShippedAmount` - сумма отгруженного
- `CreatedBy` - кто создал

### ContractItems:
- `Description` - описание товара (если его нет в каталоге)
- `Status` - статус позиции (Reserved=0, Shipped=1, Cancelled=2)

### Новая таблица ContractReservations:
- Связь позиций договора с партиями товара
- Позволяет отменять договора и возвращать товар обратно
