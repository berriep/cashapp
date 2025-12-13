// UiPath Invoke Code Script - Save Tokens to File
// Input Arguments: jobjTokens (JObject, In), jobjApiSettings (JObject, In)
// Output Arguments: saveSuccess (Boolean, Out), errorMessage (String, Out)

try
{
    if (jobjTokens == null)
    {
        throw new Exception("No tokens provided to save");
    }
    
    // Get the token file path from settings
    string tokenFile = jobjApiSettings["TokenFile"].ToString();
    
    if (string.IsNullOrEmpty(tokenFile))
    {
        throw new Exception("TokenFile path not specified in API settings");
    }
    
    // Convert JObject to formatted JSON string
    string tokensJson = jobjTokens.ToString(Newtonsoft.Json.Formatting.Indented);
    
    // Ensure directory exists
    string directory = System.IO.Path.GetDirectoryName(tokenFile);
    if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
    {
        System.IO.Directory.CreateDirectory(directory);
        System.Console.WriteLine($"[SaveTokens] Created directory: {directory}");
    }
    
    // Remove read-only attribute if file exists
    if (System.IO.File.Exists(tokenFile))
    {
        System.IO.File.SetAttributes(tokenFile, System.IO.FileAttributes.Normal);
    }
    
    // Write tokens to file
    System.IO.File.WriteAllText(tokenFile, tokensJson);
    
    saveSuccess = true;
    errorMessage = $"Tokens successfully saved to: {tokenFile}";
    
    System.Console.WriteLine($"[SaveTokens] Success! Tokens saved to: {tokenFile}");
    System.Console.WriteLine($"[SaveTokens] File size: {new System.IO.FileInfo(tokenFile).Length} bytes");
}
catch (Exception ex)
{
    saveSuccess = false;
    errorMessage = $"Failed to save tokens: {ex.Message}";
    System.Console.WriteLine($"[SaveTokens] Exception: {ex.ToString()}");
}

// Output variables:
// saveSuccess: Boolean indicating if save operation was successful
// errorMessage: String with save result details or error information
