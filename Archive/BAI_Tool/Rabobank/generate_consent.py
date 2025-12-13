#!/usr/bin/env python3
"""
Rabobank OAuth2 Consent URL Generator
Generates the authorization URL to obtain consent and authorization code.
"""

import json
import urllib.parse
import webbrowser
import secrets
import string
import os

def generate_state(length=16):
    """Generate a secure random state parameter."""
    alphabet = string.ascii_letters + string.digits
    return ''.join(secrets.choice(alphabet) for _ in range(length))

def load_client_config(client_name="default"):
    """Load client configuration from JSON file."""
    config_file = f"config/clients/{client_name}.json"
    
    if not os.path.exists(config_file):
        print(f"❌ Configuration file not found: {config_file}")
        print("Available configurations:")
        config_dir = "config/clients"
        if os.path.exists(config_dir):
            for f in os.listdir(config_dir):
                if f.endswith('.json'):
                    print(f"  {f[:-5]}")  # Remove .json extension
        return None
    
    try:
        with open(config_file, 'r') as f:
            config = json.load(f)
        return config
    except Exception as e:
        print(f"❌ Error loading configuration: {e}")
        return None

def build_consent_url(config):
    """Build the OAuth2 consent URL."""
    client_id = config['apiConfig']['clientId']
    redirect_uri = config['apiConfig']['redirectUri']
    environment = config['environment']
    
    # Determine authorization URL based on environment
    if environment == "sandbox":
        auth_url = "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/authorize"
    else:
        auth_url = "https://oauth.rabobank.nl/openapi/oauth2-premium/authorize"
    
    # Required scopes for BAI API
    scopes = "bai.accountinformation.read"
    
    # Generate secure state parameter
    state = generate_state()
    
    # Build URL parameters
    params = {
        'response_type': 'code',
        'client_id': client_id,
        'redirect_uri': redirect_uri,
        'scope': scopes,
        'state': state
    }
    
    # Build full URL
    consent_url = auth_url + '?' + urllib.parse.urlencode(params)
    
    return consent_url, state

def main():
    print("🔑 Rabobank OAuth2 Consent Generator")
    print("===================================")
    print()
    
    # Change to project directory
    project_dir = r"C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
    if os.path.exists(project_dir):
        os.chdir(project_dir)
        print(f"📁 Working directory: {project_dir}")
    else:
        print(f"⚠️  Project directory not found: {project_dir}")
        print("Running from current directory...")
    
    # Load configuration
    config = load_client_config("default")
    if not config:
        return 1
    
    print(f"✅ Loaded configuration for: {config['clientName']}")
    
    # Build consent URL
    consent_url, state = build_consent_url(config)
    
    # Display configuration details
    print()
    print("📋 Configuration Details:")
    print(f"  Client ID: {config['apiConfig']['clientId']}")
    print(f"  Redirect URI: {config['apiConfig']['redirectUri']}")
    print(f"  Environment: {config['environment']}")
    print(f"  Scopes: bai.accountinformation.read")
    print(f"  State: {state}")
    print()
    
    # Display consent URL
    print("🔗 Generated Consent URL:")
    print(consent_url)
    print()
    
    # Save state for later verification
    try:
        with open('oauth_state.txt', 'w') as f:
            f.write(state)
        print(f"💾 State parameter saved to: oauth_state.txt")
    except Exception as e:
        print(f"⚠️  Could not save state: {e}")
    
    # Copy to clipboard (Windows)
    try:
        import subprocess
        subprocess.run(['clip'], input=consent_url.encode(), check=True)
        print("📋 URL copied to clipboard!")
    except:
        print("⚠️  Could not copy to clipboard")
    
    print()
    print("📖 Next Steps:")
    print("1. The consent URL will open in your browser")
    print("2. Login with your Rabobank credentials")
    print("3. Grant permission for BAI account information access")
    print(f"4. You will be redirected to: {config['apiConfig']['redirectUri']}")
    print("5. Copy the 'code' parameter from the redirect URL")
    print("6. Use the code with: .\\Simple-Exchange.ps1 -AuthCode 'YOUR_CODE'")
    print()
    
    # Ask user if they want to open browser
    try:
        open_browser = input("🌐 Open consent URL in browser? (y/n) [default: y]: ").strip().lower()
        if open_browser in ('', 'y', 'yes'):
            webbrowser.open(consent_url)
            print("✅ Browser opened with consent URL")
        else:
            print("💡 Please copy the URL above and open it in your browser manually.")
    except KeyboardInterrupt:
        print("\n👋 Cancelled by user")
        return 0
    
    print()
    print("⏳ Waiting for authorization...")
    print("After completing consent, you will receive a redirect URL like:")
    print(f"http://localhost:8080/callback?code=YOUR_AUTH_CODE&state={state}")
    print()
    print("🔧 Extract the code parameter and run:")
    print(".\\Simple-Exchange.ps1 -AuthCode 'YOUR_AUTH_CODE'")
    
    return 0

if __name__ == "__main__":
    exit(main())