#!/usr/bin/env python3
import urllib.parse

def generate_correct_consent_url():
    client_id = "50db03679d4c3297574c26b6aab1894e"
    redirect_uri = "http://localhost:8080/callback"  # Match the working Python scripts
    scope = "ais:balances:read ais:transactions:read ais:account-details:read"  # Use AIS scope instead of BAI
    
    # Build consent URL without PKCE, using correct redirect URI and scope
    params = {
        'response_type': 'code',
        'client_id': client_id,
        'redirect_uri': redirect_uri,
        'scope': scope
    }
    
    base_url = "https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/authorize"
    consent_url = f"{base_url}?{urllib.parse.urlencode(params)}"
    
    print("=== Rabobank Consent URL (AIS Scope) ===")
    print()
    print("Please open this URL in your browser to authorize the application:")
    print()
    print(consent_url)
    print()
    print("NOTE: You will get an error page after authorization because")
    print("http://localhost:8080/callback doesn't exist, but that's OK!")
    print("Just copy the 'code' parameter from the URL in your browser.")
    print()
    
    return input("Please paste the authorization code here: ").strip()

if __name__ == "__main__":
    auth_code = generate_correct_consent_url()
    if auth_code:
        print(f"\nSaving auth code to auth_code.txt...")
        # Use run_in_terminal to save it
        print(f"Auth code to save: {auth_code}")