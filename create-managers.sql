-- Создание менеджеров напрямую в БД
-- Пользователи без пароля (вход по выбору из списка)

INSERT INTO Users (UserName, DisplayName, Role, PasswordHash, IsPasswordless, IsActive, CreatedAt) VALUES
('timur', 'Тимур', 'Manager', '', 1, 1, NOW()),
('liliya', 'Лилия', 'Manager', '', 1, 1, NOW()),
('albert', 'Альберт', 'Manager', '', 1, 1, NOW()),
('alisher', 'Алишер', 'Manager', '', 1, 1, NOW()),
('valeriy', 'Валерий', 'Manager', '', 1, 1, NOW()),
('rasim', 'Расим', 'Manager', '', 1, 1, NOW()),
('magazin', 'Магазин', 'Manager', '', 1, 1, NOW())
ON DUPLICATE KEY UPDATE DisplayName=VALUES(DisplayName), IsActive=1;
