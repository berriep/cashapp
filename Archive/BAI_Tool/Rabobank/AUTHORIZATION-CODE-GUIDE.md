# 🔑 Authorization Code - Quick Reference

## Wat is een Authorization Code?

De **authorization code** (ook wel consent code genoemd) is een tijdelijke code die je krijgt nadat een klant toestemming heeft gegeven voor API toegang.

### 📋 Kenmerken:
- **Eenmalig gebruik**: Kan maar 1x worden uitgewisseld
- **Korte levensduur**: ~10 minuten geldig
- **Lange string**: ~200+ karakters
- **Uniek per consent**: Elke nieuwe consent = nieuwe code

## 🔄 Authorization Code Flow

```
1. Klant geeft consent → Authorization Code
2. Code exchange → Access Token + Refresh Token  
3. API calls met Access Token
4. Auto-refresh via Refresh Token
5. Bij refresh failure → Nieuwe Authorization Code nodig
```

## 💻 Hoe te gebruiken

### **Methode 1: Command Line (Aanbevolen)**
```powershell
# Met PowerShell script
.\Exchange-AuthCode.ps1 -AuthCode "AAPdN1eL1JC5YEvoq8J2MCuBrYN4FKinohtKHnvDFuzRz5YyT8i7dFKsTwRyX79zVvKWJEaW59uGWsGS81Ra-CyMkEcnVreBGQJzyaeUWeR-2_vXVmdhZUjA9WlUONwSL-dlCCV3ED78MBoa86ojfuLiIbKwTPaQc-KV3WxTbP-i0rlXiKMpPido5v2So7jzaZnz_aUzBofKB1CiQJk1rG1kk5bZWDAXI7q9wcORLL2SzaCyYMVOXaTA-_BsgVk7riooKqgxkSsZud1UP2ib2uTA7mva-JPmWXUdz0ZwdYovhQ"

# Of direct met dotnet
dotnet run -- --auth-code="YOUR_AUTHORIZATION_CODE_HERE"
```

### **Methode 2: Programmatisch**
```csharp
var tokenResult = await tokenManager.ExchangeAuthorizationCodeAsync(clientConfig, authCode);
if (tokenResult.Success) {
    Console.WriteLine("Fresh tokens obtained!");
}
```

## 📂 Je huidige Authorization Code

Uit je Archive folder:
```
AAPdN1eL1JC5YEvoq8J2MCuBrYN4FKinohtKHnvDFuzRz5YyT8i7dFKsTwRyX79zVvKWJEaW59uGWsGS81Ra-CyMkEcnVreBGQJzyaeUWeR-2_vXVmdhZUjA9WlUONwSL-dlCCV3ED78MBoa86ojfuLiIbKwTPaQc-KV3WxTbP-i0rlXiKMpPido5v2So7jzaZnz_aUzBofKB1CiQJk1rG1kk5bZWDAXI7q9wcORLL2SzaCyYMVOXaTA-_BsgVk7riooKqgxkSsZud1UP2ib2uTA7mva-JPmWXUdz0ZwdYovhQ
```

⚠️ **Let op**: Deze code is waarschijnlijk al gebruikt of expired. Voor testing kun je het proberen, maar voor productie heb je een nieuwe nodig.

## 🚀 Test Scenario's

### **Scenario 1: Fresh Authorization Code Exchange**
```powershell
# Test met je authorization code
.\Exchange-AuthCode.ps1 -AuthCode "AAPdN1eL1JC5YEvoq8J2MCuBrYN4FKinohtKHnvDFuzRz5YyT8i7dFKsTwRyX79zVvKWJEaW59uGWsGS81Ra-CyMkEcnVreBGQJzyaeUWeR-2_vXVmdhZUjA9WlUONwSL-dlCCV3ED78MBoa86ojfuLiIbKwTPaQc-KV3WxTbP-i0rlXiKMpPido5v2So7jzaZnz_aUzBofKB1CiQJk1rG1kk5bZWDAXI7q9wcORLL2SzaCyYMVOXaTA-_BsgVk7riooKqgxkSsZud1UP2ib2uTA7mva-JPmWXUdz0ZwdYovhQ"
```

**Expected Result:**
```
✅ Fresh tokens obtained via authorization code exchange!
💾 Tokens saved and ready for future use
Access token length: 574
Operation type: AuthorizationCodeExchange
```

### **Scenario 2: Normal Token Management (na exchange)**
```powershell
# Test auto-refresh functionality
dotnet run
```

**Expected Result:**
```
✅ Existing tokens are valid, no refresh needed
Operation type: NoAction
```

### **Scenario 3: Expired Authorization Code**
Als de code al gebruikt is:
```
❌ Token exchange failed: 400 - {"error":"invalid_grant","error_description":"Authorization code invalid or expired"}
```

## 🔧 Troubleshooting

### **Error: "invalid_grant"**
- **Oorzaak**: Authorization code is al gebruikt of expired
- **Oplossing**: Nieuwe authorization code ophalen van Rabobank

### **Error: "Certificate not found"**
- **Oorzaak**: mTLS certificaat paden kloppen niet
- **Oplossing**: Check `config/clients/default.json` certificate paths

### **Error: "invalid_client"**
- **Oorzaak**: Client ID/Secret incorrect
- **Oplossing**: Verify credentials in configuratie

## 📋 Best Practices

### **Voor Development:**
1. **Test met bestaande code** eerst (kan expired zijn)
2. **Bij failure**: Vraag nieuwe authorization code
3. **Save tokens**: Voor hergebruik in testing
4. **Monitor expiry**: Tokens zijn ~24 uur geldig

### **Voor Productie:**
1. **Fresh authorization code** per klant
2. **Secure storage** van tokens
3. **Auto-refresh logic** implementeren
4. **Fallback mechanism** naar nieuwe consent

### **Voor UiPath:**
1. **Initialize tokens** via authorization code exchange
2. **Use TokenManager** voor auto-refresh
3. **Handle failures** gracefully
4. **Log operations** voor debugging

## 🎯 Next Steps

1. **Test de huidige authorization code**:
   ```powershell
   .\Exchange-AuthCode.ps1 -AuthCode "AAPdN1eL1JC5YEvoq8J2..."
   ```

2. **Als success**: Tokens zijn klaar voor gebruik
3. **Als failure**: Nieuwe authorization code nodig
4. **Implement API calls** in RabobankApiClient
5. **Deploy naar UiPath** environment

---

**💡 Pro Tip**: Save deze authorization code ergens veilig voor toekomstig gebruik, maar realize dat elke code maar 1x gebruikt kan worden.