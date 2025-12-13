try
{
    // Debug flag - set to true to enable detailed logging
    bool enableDebug = true; // Change to false to disable debugging
    
    // Supported date formats
    string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "yyyy/MM/dd" };
    DateTime parsed;

    // Try to parse the input date with supported formats
    if (!DateTime.TryParseExact(ClosingDateText.Trim(), formats, 
        System.Globalization.CultureInfo.InvariantCulture, 
        System.Globalization.DateTimeStyles.None, out parsed))
    {
        throw new ArgumentException($"Ongeldig formaat: {ClosingDateText}. Gebruik bijv. 2025-10-10");
    }

    // Get timezone info for Europe/Amsterdam (handles DST automatically)
    TimeZoneInfo amsterdamZone;
    try
    {
        amsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); // Windows
    }
    catch
    {
        try
        {
            amsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); // Linux/Mac
        }
        catch
        {
            throw new Exception("Could not find timezone 'W. Europe Standard Time' or 'Europe/Amsterdam'");
        }
    }

    // Create local time range for the full day (00:00:00 to 23:59:59.999)
    DateTime localStart = new DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, DateTimeKind.Unspecified);
    DateTime localEnd = localStart.AddDays(1).AddMilliseconds(-1);

    // Check for DST edge cases (from yesterday's implementation)
    if (amsterdamZone.IsInvalidTime(localStart))
    {
        // This shouldn't happen for 00:00:00, but just in case
        if (enableDebug) System.Console.WriteLine($"[DEBUG] Invalid start time during DST transition: {localStart:yyyy-MM-dd HH:mm:ss}");
        throw new Exception($"Invalid start time during DST transition: {localStart:yyyy-MM-dd HH:mm:ss}. DST transition creates gap.");
    }

    if (amsterdamZone.IsInvalidTime(localEnd))
    {
        // This could happen for 23:59:59 on DST start day
        if (enableDebug) System.Console.WriteLine($"[DEBUG] WARNING: End time {localEnd:yyyy-MM-dd HH:mm:ss} is in DST gap. Adjusting to valid time.");
        localEnd = localEnd.AddHours(1); // Move forward to valid time
    }

    // Handle ambiguous times (from yesterday's implementation)
    DateTime utcStart, utcEnd;
    
    if (amsterdamZone.IsAmbiguousTime(localStart))
    {
        // Choose standard time (CET) for ambiguous start time
        utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, amsterdamZone);
        if (enableDebug) System.Console.WriteLine($"[DEBUG] Ambiguous start time {localStart:yyyy-MM-dd HH:mm:ss} during DST transition - choosing standard time (CET)");
    }
    else
    {
        utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, amsterdamZone);
    }

    if (amsterdamZone.IsAmbiguousTime(localEnd))
    {
        // Choose standard time (CET) for ambiguous end time
        utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, amsterdamZone);
        if (enableDebug) System.Console.WriteLine($"[DEBUG] Ambiguous end time {localEnd:yyyy-MM-dd HH:mm:ss} during DST transition - choosing standard time (CET)");
    }
    else
    {
        utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, amsterdamZone);
    }

    // Format as ISO 8601 strings with Z suffix for API consumption (consistent with yesterday's implementation)
    dateFromTime = utcStart.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    dateToTime = utcEnd.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    // Enhanced debug output (consistent with yesterday's implementation)
    if (enableDebug)
    {
        System.Console.WriteLine($"[DEBUG] Input: {ClosingDateText}");
        System.Console.WriteLine($"[DEBUG] Parsed date: {parsed:yyyy-MM-dd}");
        
        string seasonType = amsterdamZone.IsDaylightSavingTime(localStart) ? "CEST (UTC+2)" : "CET (UTC+1)";
        System.Console.WriteLine($"[DEBUG] Amsterdam local time: {localStart:yyyy-MM-dd HH:mm:ss} to {localEnd:yyyy-MM-dd HH:mm:ss} ({seasonType})");
        System.Console.WriteLine($"[DEBUG] Converted to UTC: {utcStart:yyyy-MM-dd'T'HH:mm:ss.fff} to {utcEnd:yyyy-MM-dd'T'HH:mm:ss.fff}");
        System.Console.WriteLine($"[DEBUG] API format: {dateFromTime} to {dateToTime}");
    }
}
catch (Exception ex)
{
    // Set error outputs
    dateFromTime = "";
    dateToTime = "";
    
    // Log error (consistent with yesterday's implementation)
    if (enableDebug) System.Console.WriteLine($"[DEBUG] ERROR: {ex.Message}");
    
    // Re-throw to let UiPath handle the error
    throw new Exception($"Date conversion failed: {ex.Message}");
}