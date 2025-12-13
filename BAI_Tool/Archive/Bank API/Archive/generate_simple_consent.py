#!/usr/bin/env python3
import urllib.parse

def generate_simple_consent_url():
    client_id = "50db03679d4c3297574c26b6aab1894e"
    redirect_uri = "https://developer.rabobank.nl/oauth-tool"
    scope = "ais:balances:read ais:transactions:read ais:account-details:read"
    
    # Build consent URL without PKCE
    params = {
        'response_type': 'code',
        'client_id': client_id,
        'redirect_uri': redirect_uri,
        'scope': scope
    }
    
    base_url = "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/authorize"
    consent_url = f"{base_url}?{urllib.parse.urlencode(params)}"
    
    print("=== Rabobank Simple Consent URL (No PKCE) ===")
    print()
    print("Please open this URL in your browser to authorize the application:")
    print()
    print(consent_url)
    print()
    print("After authorization, you will be redirected to a URL with a 'code' parameter.")
    print("Copy that authorization code and paste it when prompted.")
    print()
    
    return input("Please paste the authorization code here: ").strip()

if __name__ == "__main__":
    auth_code = generate_simple_consent_url()
    if auth_code:
        with open("auth_code.txt", "w") as f:
            f.write(auth_code)
        print(f"Authorization code saved to auth_code.txt")