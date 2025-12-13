# 🔑 Rabobank Consent & Authorization Code Guide

## Overzicht
Deze guide helpt je bij het verkrijgen van een authorization code (consent code) van de Rabobank OAuth2 API om toegang te krijgen tot BAI account informatie.

## 📋 Wat heb je nodig?

1. **Geldige Rabobank API credentials**
   - Client ID
   - Client Secret  
   - mTLS certificaten

2. **Werkende development environment**
   - PowerShell (of Python)
   - Internetverbinding
   - Webbrowser

## 🚀 Stap-voor-stap proces

### **Methode 1: PowerShell Scripts (Aanbevolen)**

#### **Stap 1: Start de Callback Server**
```powershell
cd "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
.\Start-CallbackServer.ps1
```

Dit start een lokale server op `http://localhost:8080` die de OAuth2 callback afhandelt.

#### **Stap 2: Genereer Consent URL**
Open een **nieuwe PowerShell window** en run:
```powershell
cd "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank"
.\Generate-Consent.ps1
```

Dit script:
- Laadt je client configuratie
- Genereert een veilige consent URL
- Opent automatisch je browser
- Kopieert de URL naar clipboard

#### **Stap 3: Voltooi Consent in Browser**
1. **Login** met je Rabobank credentials
2. **Controleer** de permissions (BAI account information read)
3. **Klik "Akkoord"** of "Allow"
4. **Je wordt doorgestuurd** naar `http://localhost:8080/callback`

#### **Stap 4: Kopieer Authorization Code**
Na successful consent zie je een pagina met:
- ✅ Authorization Successful!
- De authorization code
- Instructies voor gebruik

#### **Stap 5: Exchange Authorization Code**
Kopieer de authorization code en run:
```powershell
.\Simple-Exchange.ps1 -AuthCode "JOUW_AUTHORIZATION_CODE"
```

### **Methode 2: Python Script**

Als alternatief kun je ook de Python versie gebruiken:
```bash
python generate_consent.py
```

### **Methode 3: Handmatige URL**

Je kunt ook handmatig de consent URL bouwen:

**Sandbox:**
```
https://oauth-sandbox.rabobank.nl/openapi/sandbox/oauth2-premium/authorize?response_type=code&client_id=50db03679d4c3297574c26b6aab1894e&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fcallback&scope=bai.accountinformation.read&state=RANDOM_STATE
```

**Productie:**
```
https://oauth.rabobank.nl/openapi/oauth2-premium/authorize?response_type=code&client_id=JOUW_CLIENT_ID&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fcallback&scope=bai.accountinformation.read&state=RANDOM_STATE
```

## 📊 URL Parameters Uitleg

| Parameter | Waarde | Beschrijving |
|-----------|---------|-------------|
| `response_type` | `code` | Geeft aan dat we authorization code flow gebruiken |
| `client_id` | `50db03679d4c3297574c26b6aab1894e` | Jouw geregistreerde client ID |
| `redirect_uri` | `http://localhost:8080/callback` | Waar Rabobank naar terug stuurt |
| `scope` | `bai.accountinformation.read` | Permissions die je vraagt |
| `state` | `RANDOM_STRING` | Security parameter tegen CSRF attacks |

## 🔧 Troubleshooting

### **"Callback server niet bereikbaar"**
- Controleer of `Start-CallbackServer.ps1` draait
- Check of port 8080 vrij is
- Probeer een andere port: `.\Start-CallbackServer.ps1 -Port 8081`

### **"Invalid client_id"**
- Verificeer client ID in `config/clients/default.json`
- Controleer of je de juiste environment gebruikt (sandbox vs productie)

### **"Invalid redirect_uri"**  
- Zorg dat redirect URI exact matcht met je app registratie
- Default: `http://localhost:8080/callback`

### **"Access denied"**
- Gebruiker heeft consent geweigerd
- Probeer opnieuw met nieuwe consent URL

### **"Authorization code expired"**
- Codes zijn maar ~10 minuten geldig
- Genereer nieuwe consent URL

## 💡 Tips & Best Practices

### **Security**
1. **State parameter**: Wordt automatisch gegenereerd voor CSRF bescherming
2. **HTTPS in productie**: Gebruik altijd HTTPS redirect URIs in productie
3. **Code expiry**: Gebruik authorization codes direct na ontvangst

### **Development**
1. **Meerdere tests**: Elke consent geeft nieuwe code
2. **Token storage**: Codes worden automatisch opgeslagen in `auth_code.txt`
3. **Log monitoring**: Check console output voor details

### **Productie**
1. **Geldige certificates**: Zorg voor valide mTLS certificaten
2. **Correcte endpoints**: Gebruik productie URLs
3. **Error handling**: Implementeer robuuste error handling

## 📁 Bestanden Overzicht

```
Rabobank/
├── Generate-Consent.ps1          # Genereert consent URL
├── Start-CallbackServer.ps1      # Lokale callback server  
├── Simple-Exchange.ps1           # Authorization code exchange
├── generate_consent.py           # Python versie
├── callback.html                 # Callback pagina
├── auth_code.txt                 # Opgeslagen authorization code
├── oauth_state.txt               # State parameter voor verificatie
└── config/clients/default.json   # Client configuratie
```

## 🎯 Complete Workflow Voorbeeld

```powershell
# Terminal 1: Start callback server
.\Start-CallbackServer.ps1

# Terminal 2: Genereer consent en open browser
.\Generate-Consent.ps1

# Browser: Voltooi consent process
# -> Login, accepteer permissions
# -> Automatische redirect naar localhost:8080/callback

# Terminal 2: Exchange authorization code
.\Simple-Exchange.ps1 -AuthCode "AAPslYZvIkKw..."

# Resultaat: Fresh tokens opgeslagen in tokens/default_tokens.json
```

## 📞 Ondersteuning

Voor problemen:
1. **Check logs** in PowerShell console
2. **Verify configuratie** in `config/clients/default.json`
3. **Test connectivity** naar Rabobank endpoints
4. **Review documentatie** op Rabobank Developer Portal

---

**🔄 Na successful setup**: Je tokens worden automatisch beheerd door de `TokenManager` en refresh automatisch wanneer nodig!