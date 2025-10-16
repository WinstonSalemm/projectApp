# Скрипт для добавления менеджеров через API
$apiUrl = "https://tranquil-upliftment-production.up.railway.app"

# Логинимся как admin (предполагаем что admin уже есть)
$loginBody = @{
    userName = "admin"
    password = "admin123"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
$token = $loginResponse.token

Write-Host "Logged in as admin, token: $token"

# Заголовки с токеном
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Менеджеры для создания
$managers = @(
    @{ userName = "timur"; displayName = "Тимур"; role = "Manager"; password = "manager123" }
    @{ userName = "liliya"; displayName = "Лилия"; role = "Manager"; password = "manager123" }
    @{ userName = "albert"; displayName = "Альберт"; role = "Manager"; password = "manager123" }
    @{ userName = "alisher"; displayName = "Алишер"; role = "Manager"; password = "manager123" }
    @{ userName = "valeriy"; displayName = "Валерий"; role = "Manager"; password = "manager123" }
    @{ userName = "rasim"; displayName = "Расим"; role = "Manager"; password = "manager123" }
    @{ userName = "magazin"; displayName = "Магазин"; role = "Manager"; password = "manager123" }
)

foreach ($manager in $managers) {
    try {
        $body = $manager | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$apiUrl/api/users" -Method POST -Body $body -Headers $headers
        Write-Host "✅ Created: $($manager.displayName) ($($manager.userName))" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Failed to create $($manager.displayName): $_" -ForegroundColor Red
    }
}

Write-Host "`n✅ Done! Created managers." -ForegroundColor Green
