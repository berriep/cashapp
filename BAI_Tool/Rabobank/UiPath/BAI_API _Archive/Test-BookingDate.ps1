Write-Host "=== BOOKING DATE CONVERSION TEST ==="
$timestamp = "2025-10-05 22:08:03.184628+00"
Write-Host "Input UTC Timestamp: $timestamp"

try {
    # Parse the timestamp
    $utcTime = [DateTime]::Parse($timestamp)
    Write-Host "Parsed UTC: $($utcTime.ToString('yyyy-MM-dd HH:mm:ss.ffffff'))"
    Write-Host "DateTime Kind: $($utcTime.Kind)"
    
    # Convert to local time
    $localTime = $utcTime.ToLocalTime()
    Write-Host "Local Time: $($localTime.ToString('yyyy-MM-dd HH:mm:ss.ffffff'))"
    Write-Host "Local Kind: $($localTime.Kind)"
    
    # Extract date
    $localDate = $localTime.ToString("yyyy-MM-dd")
    Write-Host "Final booking date: $localDate"
    
    # Check timezone info
    $timezone = [System.TimeZoneInfo]::Local
    Write-Host "System TimeZone: $($timezone.DisplayName)"
    Write-Host "TimeZone ID: $($timezone.Id)"
    
    # Check if DST is active in October
    $testDate = Get-Date "2025-10-05"
    Write-Host "Is DST active in Oct 2025: $($timezone.IsDaylightSavingTime($testDate))"
    
    # Validate result
    if ($localDate -eq "2025-10-06") {
        Write-Host "✅ SUCCESS: Conversion worked correctly (UTC 22:08 → next day local)"
    } else {
        Write-Host "❌ PROBLEM: Expected '2025-10-06', got '$localDate'"
    }
    
} catch {
    Write-Host "❌ ERROR: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=== ANALYSIS ==="
Write-Host "Expected behavior:"
Write-Host "UTC: 2025-10-05 22:08:03"
Write-Host "Amsterdam (CEST): 2025-10-06 00:08:03 (UTC+2 in October)"
Write-Host "Final date: 2025-10-06"