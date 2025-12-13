# Simple HTTP Server for OAuth2 Callback
# Starts a local server on port 8080 to handle the OAuth2 callback

param(
    [Parameter(Mandatory=$false)]
    [int]$Port = 8080
)

Write-Host "Rabobank OAuth2 Callback Server" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

# Check if port is available
try {
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://localhost:$Port/")
    $listener.Start()
    $listener.Stop()
    Write-Host "Port $Port is available" -ForegroundColor Green
} catch {
    Write-Host "Port $Port is already in use!" -ForegroundColor Red
    Write-Host "Please stop any running servers or use a different port." -ForegroundColor Yellow
    exit 1
}

Write-Host "Starting callback server on: http://localhost:$Port" -ForegroundColor Green
Write-Host "Callback URL: http://localhost:$Port/callback" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor White
Write-Host ""

# Start HTTP listener
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()

Write-Host "Server started successfully!" -ForegroundColor Green
Write-Host "Waiting for OAuth2 callback..." -ForegroundColor Cyan
Write-Host ""

try {
    while ($listener.IsListening) {
        # Wait for request
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response
        
        $url = $request.Url.AbsoluteUri
        $path = $request.Url.AbsolutePath
        
        Write-Host "$(Get-Date -Format 'HH:mm:ss') - Request: $url" -ForegroundColor White
        
        if ($path -eq "/callback") {
            # Parse query parameters
            $queryParams = @{}
            if ($request.Url.Query) {
                $query = $request.Url.Query.TrimStart('?')
                foreach ($param in $query.Split('&')) {
                    $parts = $param.Split('=', 2)
                    if ($parts.Length -eq 2) {
                        $key = [System.Web.HttpUtility]::UrlDecode($parts[0])
                        $value = [System.Web.HttpUtility]::UrlDecode($parts[1])
                        $queryParams[$key] = $value
                    }
                }
            }
            
            # Check for authorization code
            if ($queryParams.ContainsKey('code')) {
                $authCode = $queryParams['code']
                $state = $queryParams['state']
                
                Write-Host ""
                Write-Host "SUCCESS: Authorization code received!" -ForegroundColor Green
                Write-Host "Code: $authCode" -ForegroundColor Yellow
                if ($state) {
                    Write-Host "State: $state" -ForegroundColor Gray
                }
                Write-Host ""
                
                # Save authorization code
                try {
                    $authCode | Out-File -FilePath "auth_code.txt" -Encoding UTF8 -NoNewline
                    Write-Host "Authorization code saved to: auth_code.txt" -ForegroundColor Green
                } catch {
                    Write-Host "Could not save authorization code: $_" -ForegroundColor Yellow
                }
                
                Write-Host ""
                Write-Host "Next step: Run the token exchange:" -ForegroundColor Cyan
                Write-Host ".\\Simple-Exchange.ps1 -AuthCode `"$authCode`"" -ForegroundColor White
                Write-Host ""
                
                # Serve success page
                $html = Get-Content "callback.html" -Raw -ErrorAction SilentlyContinue
                if (-not $html) {
                    $html = @"
<!DOCTYPE html>
<html><head><title>Success</title></head>
<body style="font-family: Arial; padding: 50px; text-align: center;">
<h1 style="color: green;">✅ Authorization Successful!</h1>
<p>Authorization code: <code>$authCode</code></p>
<p>You can close this window and return to PowerShell.</p>
</body></html>
"@
                }
                
            } elseif ($queryParams.ContainsKey('error')) {
                $authError = $queryParams['error']
                $errorDesc = $queryParams['error_description']
                
                Write-Host ""
                Write-Host "ERROR: Authorization failed!" -ForegroundColor Red
                Write-Host "Error: $authError" -ForegroundColor Yellow
                if ($errorDesc) {
                    Write-Host "Description: $errorDesc" -ForegroundColor Yellow
                }
                Write-Host ""
                
                # Serve error page
                $html = @"
<!DOCTYPE html>
<html><head><title>Error</title></head>
<body style="font-family: Arial; padding: 50px; text-align: center;">
<h1 style="color: red;">❌ Authorization Failed</h1>
<p>Error: $authError</p>
<p>$errorDesc</p>
<p>Please check your configuration and try again.</p>
</body></html>
"@
            } else {
                # No code or error - show waiting page
                $html = @"
<!DOCTYPE html>
<html><head><title>Callback</title></head>
<body style="font-family: Arial; padding: 50px; text-align: center;">
<h1>OAuth2 Callback Endpoint</h1>
<p>Waiting for authorization response...</p>
<p>Current URL: $url</p>
</body></html>
"@
            }
        } else {
            # Serve simple index page
            $html = @"
<!DOCTYPE html>
<html><head><title>OAuth2 Server</title></head>
<body style="font-family: Arial; padding: 50px; text-align: center;">
<h1>Rabobank OAuth2 Callback Server</h1>
<p>Server is running on port $Port</p>
<p>Callback endpoint: <a href="/callback">/callback</a></p>
<p>Generate consent URL with: <code>.\\Generate-Consent.ps1</code></p>
</body></html>
"@
        }
        
        # Send response
        $buffer = [System.Text.Encoding]::UTF8.GetBytes($html)
        $response.ContentLength64 = $buffer.Length
        $response.ContentType = "text/html; charset=utf-8"
        $response.StatusCode = 200
        $response.OutputStream.Write($buffer, 0, $buffer.Length)
        $response.OutputStream.Close()
    }
} catch [System.OperationCanceledException] {
    Write-Host "Server stopped by user" -ForegroundColor Yellow
} catch {
    Write-Host "Server error: $_" -ForegroundColor Red
} finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }
    $listener.Close()
    Write-Host "Server stopped." -ForegroundColor Gray
}