// UiPath Invoke Code Script - Load Tokens from File
// Input Arguments: jobjApiSettings (JObject, In)
// Output Arguments: jobjTokens (JObject, Out), loadSuccess (Boolean, Out), errorMessage (String, Out)

try
{
    // Get the token file path from settings
    string tokenFile = jobjApiSettings["TokenFile"].ToString();
    
    if (string.IsNullOrEmpty(tokenFile))
    {
        throw new Exception("TokenFile path not specified in API settings");
    }
    
    // Check if token file exists
    if (!System.IO.File.Exists(tokenFile))
    {
        loadSuccess = false;
        errorMessage = $"Token file does not exist: {tokenFile}";
        System.Console.WriteLine($"[LoadTokens] File not found: {tokenFile}");
        return;
    }
    
    // Read and parse the token file
    string tokensJson = System.IO.File.ReadAllText(tokenFile);
    
    if (string.IsNullOrEmpty(tokensJson.Trim()))
    {
        throw new Exception("Token file is empty");
    }
    
    // Parse JSON to JObject
    jobjTokens = Newtonsoft.Json.Linq.JObject.Parse(tokensJson);
    
    // Validate that essential token fields exist
    if (jobjTokens["access_token"] == null)
    {
        throw new Exception("Invalid token file: missing access_token");
    }
    
    loadSuccess = true;
    errorMessage = $"Tokens successfully loaded from: {tokenFile}";
    
    System.Console.WriteLine($"[LoadTokens] Success! Loaded tokens from: {tokenFile}");
    System.Console.WriteLine($"[LoadTokens] Access token length: {jobjTokens["access_token"]?.ToString()?.Length ?? 0}");
    System.Console.WriteLine($"[LoadTokens] Has refresh token: {jobjTokens["refresh_token"] != null}");
}
catch (Exception ex)
{
    loadSuccess = false;
    errorMessage = $"Failed to load tokens: {ex.Message}";
    jobjTokens = null;
    System.Console.WriteLine($"[LoadTokens] Exception: {ex.ToString()}");
}

// Output variables:
// jobjTokens: JObject containing loaded tokens (or null if failed)
// loadSuccess: Boolean indicating if load operation was successful
// errorMessage: String with load result details or error information
