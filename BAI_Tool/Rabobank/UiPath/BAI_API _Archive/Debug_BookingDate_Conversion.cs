// Debug script om de bookingDate conversie te testen
// Input: raboBookingDateTime = "2025-10-05 22:08:03.184628+00"
// Expected: bookingDate = "2025-10-06" (lokale tijd)

using System;

public class DebugBookingDateConversion
{
    public static void Main()
    {
        // Test case van de gebruiker
        string raboBookingDateTime = "2025-10-05 22:08:03.184628+00";
        string originalBookingDate = "2025-10-05"; // Van API
        
        Console.WriteLine("=== BOOKING DATE CONVERSION DEBUG ===");
        Console.WriteLine($"Input raboBookingDateTime: '{raboBookingDateTime}'");
        Console.WriteLine($"Original API bookingDate: '{originalBookingDate}'");
        Console.WriteLine();
        
        try
        {
            // Parse the raboBookingDateTime (UTC)
            DateTime raboBookingDT = DateTime.Parse(raboBookingDateTime);
            Console.WriteLine($"Parsed DateTime: {raboBookingDT:yyyy-MM-dd HH:mm:ss.ffffff}");
            Console.WriteLine($"DateTime Kind: {raboBookingDT.Kind}");
            Console.WriteLine($"DateTime UTC: {raboBookingDT:yyyy-MM-dd HH:mm:ss.ffffff} UTC");
            Console.WriteLine();
            
            // Convert to local time (Europe/Amsterdam)
            DateTime localBookingDT = raboBookingDT.ToLocalTime();
            Console.WriteLine($"Local DateTime: {localBookingDT:yyyy-MM-dd HH:mm:ss.ffffff}");
            Console.WriteLine($"Local DateTime Kind: {localBookingDT.Kind}");
            
            // Get timezone info
            TimeZoneInfo localZone = TimeZoneInfo.Local;
            Console.WriteLine($"Local TimeZone: {localZone.DisplayName}");
            Console.WriteLine($"Local TimeZone ID: {localZone.Id}");
            Console.WriteLine();
            
            // Extract date
            string derivedBookingDate = localBookingDT.ToString("yyyy-MM-dd");
            Console.WriteLine($"Derived bookingDate: '{derivedBookingDate}'");
            
            // Check if conversion worked as expected
            if (derivedBookingDate == "2025-10-06")
            {
                Console.WriteLine("✅ CONVERSION CORRECT: UTC 22:08 → Local next day");
            }
            else
            {
                Console.WriteLine("❌ CONVERSION PROBLEM: Expected '2025-10-06', got '{0}'", derivedBookingDate);
            }
            
            Console.WriteLine();
            Console.WriteLine("=== MANUAL TIMEZONE CONVERSION ===");
            
            // Try manual timezone conversion to Europe/Amsterdam
            try
            {
                TimeZoneInfo amsterdamZone;
                try
                {
                    amsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); // Windows
                    Console.WriteLine("Using Windows timezone: W. Europe Standard Time");
                }
                catch
                {
                    amsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); // Linux/Mac
                    Console.WriteLine("Using IANA timezone: Europe/Amsterdam");
                }
                
                DateTime manualLocal = TimeZoneInfo.ConvertTimeFromUtc(raboBookingDT, amsterdamZone);
                string manualDerivedDate = manualLocal.ToString("yyyy-MM-dd");
                
                Console.WriteLine($"Manual Amsterdam time: {manualLocal:yyyy-MM-dd HH:mm:ss.ffffff}");
                Console.WriteLine($"Manual derived date: '{manualDerivedDate}'");
                
                if (manualDerivedDate == "2025-10-06")
                {
                    Console.WriteLine("✅ MANUAL CONVERSION CORRECT");
                }
                else
                {
                    Console.WriteLine("❌ MANUAL CONVERSION ALSO WRONG");
                }
            }
            catch (Exception tzEx)
            {
                Console.WriteLine($"Timezone conversion error: {tzEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== END DEBUG ===");
    }
}