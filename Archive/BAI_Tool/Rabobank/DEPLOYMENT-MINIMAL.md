# Minimale Deployment - Alleen URL Generatie

Voor alleen het genereren van Rabobank consent URLs heb je deze bestanden nodig:

## 📁 Benodigde Bestanden:

### **🛠️ Scripts:**
- `Generate-Consent.ps1` - Hoofdscript voor URL generatie
- `Switch-Environment.ps1` - Wissel tussen sandbox/production

### **⚙️ Configuratie:**
- `config/appsettings.json` - Basis instellingen
- `config/clients/default.json` - Actieve client configuratie
- `config/clients/production.json` - Production instellingen  
- `config/clients/sandbox-template.json` - Sandbox template

### **📚 Documentatie (optioneel):**
- `README.md`
- `CONSENT-GUIDE.md`

## 🚫 NIET Nodig:
- Geen C# source code (`src/` directory)
- Geen project files (`RabobankBAI.csproj`)
- Geen certificaten (alleen voor URL generatie)
- Geen token storage
- Geen .NET SDK vereist

## 🎯 Gebruik:
```powershell
# Genereer URL met redirect URI
.\Generate-Consent.ps1

# Genereer URL zonder redirect URI  
.\Generate-Consent.ps1 -SkipRedirectUri

# Wissel naar production
.\Switch-Environment.ps1 -ToProduction

# Wissel naar sandbox
.\Switch-Environment.ps1 -ToSandbox
```

## 📋 Deployment Stappen:
1. Kopieer de 6 bestanden naar nieuwe locatie
2. Zet PowerShell execution policy: `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser`
3. Run script: `.\Generate-Consent.ps1`
4. Kopieer de gegenereerde URL uit je scherm