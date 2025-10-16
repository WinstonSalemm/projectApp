-- Добавляем колонку OwnerUserName в таблицу Clients
ALTER TABLE Clients ADD COLUMN OwnerUserName VARCHAR(100) NULL;

-- Можно добавить индекс для быстрого поиска
CREATE INDEX idx_clients_owner ON Clients(OwnerUserName);
