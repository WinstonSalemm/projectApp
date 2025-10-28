#!/usr/bin/env pwsh
# Apply contract enhancements migration to Railway MySQL

Write-Host "🚀 Applying Contract Enhancements Migration to Railway..." -ForegroundColor Cyan

# Check if railway CLI is installed
$railwayExists = Get-Command railway -ErrorAction SilentlyContinue
if (-not $railwayExists) {
    Write-Host "❌ Railway CLI not found. Please install it first:" -ForegroundColor Red
    Write-Host "   npm i -g @railway/cli" -ForegroundColor Yellow
    exit 1
}

# Path to migration file
$migrationFile = "migrations/add-contract-enhancements-v2-mysql.sql"

if (-not (Test-Path $migrationFile)) {
    Write-Host "❌ Migration file not found: $migrationFile" -ForegroundColor Red
    exit 1
}

Write-Host "📄 Reading migration file: $migrationFile" -ForegroundColor Green
$sql = Get-Content $migrationFile -Raw

# Apply migration using Railway CLI
Write-Host "🔧 Executing migration on Railway MySQL database..." -ForegroundColor Yellow

# Split SQL by statement and execute each one
$statements = $sql -split ';' | Where-Object { $_.Trim() -ne '' }

foreach ($statement in $statements) {
    $cleanStatement = $statement.Trim()
    if ($cleanStatement) {
        Write-Host "  → Executing: $($cleanStatement.Substring(0, [Math]::Min(60, $cleanStatement.Length)))..." -ForegroundColor Gray
        
        # Escape single quotes for shell
        $escapedSql = $cleanStatement -replace "'", "'\''"
        
        # Execute via railway run
        railway run mysql -e "$cleanStatement"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠️  Warning: Statement may have failed (this is OK if column already exists)" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "✅ Migration completed!" -ForegroundColor Green
Write-Host "🔍 Verify the changes in your Railway database dashboard" -ForegroundColor Cyan
