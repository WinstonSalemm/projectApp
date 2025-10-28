# Returns Fix Summary

## Problem
The managers analytics table was **not showing returns** (возвраты) data. Returns were not being calculated or displayed.

## Root Causes
1. **Backend API** - `AnalyticsController.Managers()` was missing:
   - Returns calculation logic
   - `TotalReturns`, `NetRevenue`, `SalesCount`, `ReturnsCount` fields in DTO
   
2. **Frontend** - `AnalyticsViewModel` was missing:
   - Returns fields in both `ManagerStatRow` and `ManagerStatsDto`
   - Display of returns data in UI

3. **No logging** - No debug logs to see what values were being calculated

## Solution Implemented

### Backend Changes (`AnalyticsController.cs`)

**Added to ManagerStatsRow DTO:**
```csharp
public decimal TotalReturns { get; set; }
public decimal NetRevenue { get; set; }
public int SalesCount { get; set; }
public int ReturnsCount { get; set; }
```

**Added Returns Calculation Logic:**
```csharp
// Get all sales for this manager in period
var salesQuery = db.Sales
    .AsNoTracking()
    .Where(s => s.CreatedBy == user.UserName && s.CreatedAt >= dateFrom && s.CreatedAt < dateTo);

var salesCount = await salesQuery.CountAsync(ct);
var totalRevenue = await salesQuery.SelectMany(s => s.Items).SumAsync(i => i.Qty * i.UnitPrice, ct);

// Find returns linked to these sales via RefSaleId
var salesIds = await salesQuery.Select(s => s.Id).ToListAsync(ct);
var returnsQuery = db.Returns
    .AsNoTracking()
    .Where(r => salesIds.Contains(r.RefSaleId ?? 0));

var returnsCount = await returnsQuery.CountAsync(ct);
var totalReturns = await returnsQuery.SumAsync(r => (decimal?)r.Sum, ct) ?? 0m;

// Calculate net revenue
var netRevenue = totalRevenue - totalReturns;
```

**Added Debug Logging:**
```csharp
Console.WriteLine($"[MANAGER STATS] {user.UserName}: Sales={salesCount}, Revenue={totalRevenue:N0}, Returns={returnsCount}, ReturnsSum={totalReturns:N0}, Net={netRevenue:N0}");
```

**Changed Sorting:**
- Before: Sorted by `TotalRevenue`
- After: Sorted by `NetRevenue` (revenue minus returns)

### Frontend Changes

**Updated `AnalyticsViewModel.cs`:**

1. **ManagerStatRow class** - Added fields:
   ```csharp
   public decimal TotalReturns { get; set; }
   public decimal NetRevenue { get; set; }
   public int SalesCount { get; set; }
   public int ReturnsCount { get; set; }
   ```

2. **ManagerStatsDto class** - Added same fields

3. **LoadManagerStats()** - Updated to:
   - Use `NetRevenue` for bar width calculation
   - Sort by `NetRevenue` instead of `TotalRevenue`
   - Populate all new fields from API response

**Updated `ManagerAnalyticsPage.xaml`:**

Changed graph bars to show **NetRevenue** instead of TotalRevenue:
```xml
<Label Grid.Column="2" Text="{Binding NetRevenue, StringFormat='{0:N0}'}" ... />
```

Added returns display in manager details:
```xml
<Grid Grid.Row="1" ColumnDefinitions="Auto,*" ColumnSpacing="8">
  <Label Text="🛒 Продаж:" />
  <Label Text="{Binding SalesCount}" />
</Grid>

<Grid Grid.Row="3" ColumnDefinitions="Auto,*" ColumnSpacing="8">
  <Label Text="↩️ Возвратов:" />
  <Label Text="{Binding ReturnsCount}" TextColor="Error" />
</Grid>

<Grid Grid.Row="4" ColumnDefinitions="Auto,*" ColumnSpacing="8">
  <Label Text="🚫 Сумма возвратов:" />
  <Label Text="{Binding TotalReturns, StringFormat='{0:N0} сум'}" TextColor="Error" />
</Grid>

<Grid Grid.Row="5" ColumnDefinitions="Auto,*" ColumnSpacing="8">
  <Label Text="✅ Чистый оборот:" />
  <Label Text="{Binding NetRevenue, StringFormat='{0:N0} сум'}" FontSize="16" TextColor="Success" />
</Grid>
```

## How Returns Are Linked

Returns are linked to managers through the **sale they came from**:

1. Sale has `CreatedBy` field → identifies which manager made the sale
2. Return has `RefSaleId` field → links return to original sale
3. Query logic: Find all sales by manager → Get their IDs → Find returns with those IDs as `RefSaleId`

```sql
-- Conceptual query
SELECT * FROM Returns 
WHERE RefSaleId IN (
  SELECT Id FROM Sales 
  WHERE CreatedBy = 'manager_username' 
  AND CreatedAt >= @from AND CreatedAt < @to
)
```

## What You'll See Now

### Backend Logs (Console)
```
[MANAGER STATS] john_manager: Sales=15, Revenue=25,000,000, Returns=2, ReturnsSum=3,500,000, Net=21,500,000
[MANAGER STATS] mary_manager: Sales=22, Revenue=40,000,000, Returns=1, ReturnsSum=1,200,000, Net=38,800,000
[MANAGER STATS] Total managers: 5, Period: 2025-01-01 to 2025-02-01
```

### Frontend UI
Each manager card now shows:
- 🛒 **Продаж**: 15
- 💰 **Общий оборот**: 25,000,000 сум
- ↩️ **Возвратов**: 2 (in red)
- 🚫 **Сумма возвратов**: 3,500,000 сум (in red)
- ✅ **Чистый оборот**: 21,500,000 сум (large, green, prominent)
- 👥 **Оборот своим клиентам**: 18,000,000 сум
- 👤 **Приведенных клиентов**: 8

## Testing

To verify the fix:

1. **Check backend logs** - Run the API and call `/api/analytics/managers`
   - Look for console output with sales/returns numbers
   - Verify NetRevenue = TotalRevenue - TotalReturns

2. **Check API response** - Use Postman or browser:
   ```
   GET https://tranquil-upliftment-production.up.railway.app/api/analytics/managers
   ```
   Should return JSON with all fields populated

3. **Check MAUI app** - Open Manager Analytics page:
   - Should see returns count and sum for each manager
   - Net revenue should be highlighted in green
   - Managers sorted by net revenue (not gross)

## Files Modified

**Backend:**
- `src/ProjectApp.Api/Controllers/AnalyticsController.cs`

**Frontend:**
- `src/ProjectApp.Client.Maui/ViewModels/AnalyticsViewModel.cs`
- `src/ProjectApp.Client.Maui/Views/ManagerAnalyticsPage.xaml`

## Status: ✅ FIXED

The returns system is now fully integrated into manager analytics with proper calculation, logging, and display.
