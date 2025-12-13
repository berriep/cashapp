' Inputs:
'   ClosingDateText As String
' Outputs:
'   dateFromTime As String
'   dateToTime As String

Dim formats As String() = {"yyyy-MM-dd","dd-MM-yyyy","yyyy/MM/dd"}
Dim parsed As DateTime

If Not DateTime.TryParseExact(ClosingDateText.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, parsed) Then
    Throw New ArgumentException($"Ongeldig formaat: {ClosingDateText}. Gebruik bijv. 2025-10-10")
End If

Dim tz As TimeZoneInfo
Try
    tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time")
Catch
    tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam")
End Try

Dim localStart = New DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, DateTimeKind.Unspecified)
Dim localEnd = localStart.AddDays(1).AddMilliseconds(-1)

Dim utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, tz)
Dim utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz)

dateFromTime = utcStart.ToString("yyyy-MM-dd' T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)
dateToTime   = utcEnd.ToString("yyyy-MM-dd' T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)