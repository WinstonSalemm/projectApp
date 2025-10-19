# PowerShell script for SQLite migration
# Usage: .\apply-migration.ps1

$dbPath = "ProjectApp.db"
$migrationFile = "add-defectives-refills-system.sql"

Write-Host "Applying migration to $dbPath..." -ForegroundColor Cyan

# Check if database file exists
if (-not (Test-Path $dbPath)) {
    Write-Host "ERROR: Database file $dbPath not found!" -ForegroundColor Red
    exit 1
}

# Check if migration file exists
if (-not (Test-Path $migrationFile)) {
    Write-Host "ERROR: Migration file $migrationFile not found!" -ForegroundColor Red
    exit 1
}

# Check if sqlite3 is installed
$sqlite3 = Get-Command sqlite3 -ErrorAction SilentlyContinue

if ($null -eq $sqlite3) {
    Write-Host "ERROR: sqlite3 is not installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install SQLite using one of these methods:" -ForegroundColor Yellow
    Write-Host "  1. winget install SQLite.SQLite" -ForegroundColor White
    Write-Host "  2. choco install sqlite" -ForegroundColor White
    Write-Host "  3. Download from https://www.sqlite.org/download.html" -ForegroundColor White
    exit 1
}

# Apply migration
Write-Host "Executing SQL commands..." -ForegroundColor Yellow

try {
    # Read SQL file
    $sql = Get-Content $migrationFile -Raw
    
    # Apply via sqlite3
    $sql | sqlite3 $dbPath 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: Migration applied!" -ForegroundColor Green
        
        # Check created tables
        Write-Host ""
        Write-Host "Checking created tables..." -ForegroundColor Cyan
        
        $checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('DefectiveItems', 'RefillOperations');"
        
        $tables = echo $checkSql | sqlite3 $dbPath
        
        if ($tables) {
            Write-Host "Tables created:" -ForegroundColor Green
            $tables -split "`n" | ForEach-Object {
                if ($_.Trim()) {
                    Write-Host "  - $_" -ForegroundColor White
                }
            }
        } else {
            Write-Host "WARNING: Tables not found (may already exist)" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "Done! You can now run the application." -ForegroundColor Green
    } else {
        Write-Host "ERROR: Migration failed!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
