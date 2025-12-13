# Test Data Converter

Dit is een zelfstandige applicatie die CSV testdata omzet naar JSON bestanden in Rabobank API formaat voor het testen van MT940 en CAMT.053 conversie.

## Functies

- Converteert CSV transactie bestanden naar per-dag JSON bestanden
- Genereert simulierte balans data gebaseerd op transacties
- Produceert JSON in officieel Rabobank API formaat
- Geschikt voor het testen van banking file conversie

## Gebruik

### 1. Voorbereiding
Plaats CSV bestanden in de `TestData` folder met de volgende naamconventie:
```
CSV_A_[IBAN]_EUR_[STARTDATUM]_[EINDDATUM].csv
```

Bijvoorbeeld:
- `CSV_A_NL08RABO0100929575_EUR_20250829_20250901.csv`
- `CSV_A_NL31RABO0300087233_EUR_20250829_20250901.csv`

### 2. Uitvoeren
```bash
cd TestDataConverter
dotnet run
```

### 3. Output
De applicatie genereert JSON bestanden in de `Output` folder:

**Transactie bestanden:**
- `transactions_[IBAN]_[DATUM].json` - Per dag transacties in API formaat

**Balans bestanden:**
- `balance_[IBAN]_[DATUM].json` - Per dag balans informatie

## CSV Formaat

De input CSV bestanden moeten de volgende kolommen bevatten:
1. IBAN/BBAN
2. Munt  
3. BIC
4. Volgnr
5. Datum
6. Rentedatum
7. Bedrag
8. Saldo na trn
9. Tegenrekening IBAN/BBAN
10. Naam tegenpartij
11. Naam uiteindelijke partij
12. Naam initiërende partij
13. BIC tegenpartij
14. Code
15. Batch ID
16. Transactiereferentie
17. Machtigingskenmerk
18. Incassant ID
19. Betalingskenmerk
20. Omschrijving-1
21. Omschrijving-2
22. Omschrijving-3
23. Reden retour
24. Oorspr bedrag
25. Oorspr munt
26. Koers

## JSON Output Formaat

### Transacties Response
```json
{
  "account": "NL08RABO0100929575",
  "currency": "EUR", 
  "fromDate": "2025-08-29T00:00:00",
  "toDate": "2025-08-29T00:00:00",
  "totalCount": 5,
  "transactions": [
    {
      "transactionId": "000000000000091995",
      "status": "Booked",
      "amount": 185.50,
      "currency": "EUR",
      "valueDate": "2025-08-29T00:00:00",
      "bookingDate": "2025-08-29T00:00:00",
      "counterParty": {
        "iban": "NL91ABNA0417164300",
        "name": "ABN AMRO Bank N.V.",
        "bic": "ABNANL2A"
      },
      "reference": "20250829164300",
      "description": "SEPA Overboeking",
      "category": "Transfer",
      "typeDescription": "Credit Transfer"
    }
  ]
}
```

### Balans Response
```json
{
  "account": "NL08RABO0100929575",
  "currency": "EUR",
  "date": "2025-08-29T00:00:00", 
  "openingBalance": 1500000.00,
  "closingBalance": 1650000.00,
  "intradayBalances": [
    {
      "timestamp": "2025-08-29T09:00:00",
      "balance": 1520000.00
    },
    {
      "timestamp": "2025-08-29T12:00:00", 
      "balance": 1580000.00
    },
    {
      "timestamp": "2025-08-29T15:00:00",
      "balance": 1630000.00
    },
    {
      "timestamp": "2025-08-29T17:00:00",
      "balance": 1650000.00
    }
  ]
}
```

## Gebruik in Tests

De gegenereerde JSON bestanden kunnen gebruikt worden voor:

1. **MT940 Conversie Testing** - Test de omzetting van API data naar MT940 formaat
2. **CAMT.053 Conversie Testing** - Test de omzetting van API data naar CAMT.053 XML
3. **Banking Integration Testing** - Simuleer echte bank API responses
4. **Data Validation Testing** - Valideer data transformatie logica

## Vereisten

- .NET 8.0 of hoger
- CSV bestanden in de juiste format
- Windows, macOS of Linux
