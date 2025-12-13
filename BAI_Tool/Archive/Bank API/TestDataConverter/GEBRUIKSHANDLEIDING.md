# Test Data Converter - Gebruikshandleiding

## Overzicht

De Test Data Converter is een zelfstandige PowerShell applicatie die CSV transactiebestanden omzet naar JSON bestanden in Rabobank API formaat. Deze tool is speciaal ontworpen voor het testen van MT940 en CAMT.053 conversie systemen.

## Wat doet de converter?

### Input
- **CSV bestanden** met banktransacties in Nederlands exportformaat
- Ondersteunt Rabobank CSV export formaat
- Verwacht bestandsnamen: `CSV_A_[IBAN]_EUR_[STARTDATUM]_[EINDDATUM].csv`

### Output
- **Per-dag transactie JSON bestanden** in Rabobank API formaat
- **Per-dag balans JSON bestanden** met opening/closing balances en intraday bewegingen
- Bestandsnamen: `transactions_[IBAN]_[YYYYMMDD].json` en `balance_[IBAN]_[YYYYMMDD].json`

## Mappenstructuur

```
TestDataConverter/
├── Convert-TestData.ps1     # PowerShell conversie script
├── Program.cs               # C# versie (optioneel, vereist .NET SDK)
├── TestDataConverter.csproj # Project bestand
├── README.md               # Documentatie
├── TestData/               # Plaats hier CSV bestanden
│   ├── CSV_A_NL08RABO0100929575_EUR_20250829_20250901.csv
│   └── CSV_A_NL31RABO0300087233_EUR_20250829_20250901.csv
└── Output/                 # Gegenereerde JSON bestanden
    ├── transactions_NL08RABO0100929575_20250829.json
    ├── transactions_NL31RABO0300087233_20250829.json
    ├── balance_NL08RABO0100929575_20250829.json
    └── balance_NL31RABO0300087233_20250829.json
```

## Gebruik

### Stap 1: CSV bestanden plaatsen
1. Kopieer je CSV bestanden naar de `TestData` folder
2. Zorg dat de bestandsnamen het juiste formaat hebben

### Stap 2: Script uitvoeren
```powershell
# Navigeer naar de folder
cd TestDataConverter

# Voer het conversie script uit
powershell -ExecutionPolicy Bypass -File .\Convert-TestData.ps1
```

### Stap 3: Resultaten controleren
- Bekijk de `Output` folder voor gegenereerde JSON bestanden
- Elk CSV bestand wordt omgezet naar meerdere JSON bestanden (per dag)

## Resultaat bestanden

### Transactie JSON (transactions_[IBAN]_[DATUM].json)
```json
{
  "account": "NL31RABO0300087233",
  "currency": "EUR",
  "fromDate": "2025-08-29T00:00:00",
  "toDate": "2025-08-29T00:00:00",
  "totalCount": 43,
  "transactions": [
    {
      "transactionId": "000000000000091999",
      "status": "Booked",
      "amount": 8510.00,
      "currency": "EUR",
      "valueDate": "2025-08-29T00:00:00",
      "bookingDate": "2025-08-29T00:00:00",
      "counterParty": {
        "iban": "CH3104835149980361000",
        "name": "Metam Solutions AG",
        "bic": "CRESCHZZ80A"
      },
      "reference": "C20250827-9505727653-45041917191446",
      "description": "RE-02600",
      "category": "International Transfer",
      "typeDescription": "Debit International Wire",
      "creditor": {
        "iban": "NL31RABO0300087233",
        "name": "CENTER PARCS EUROPE NV"
      },
      "debtor": {
        "iban": "CH3104835149980361000",
        "name": "Metam Solutions AG"
      }
    }
  ]
}
```

### Balans JSON (balance_[IBAN]_[DATUM].json)
```json
{
  "account": "NL31RABO0300087233",
  "currency": "EUR",
  "date": "2025-08-29T00:00:00",
  "openingBalance": 2217975.31,
  "closingBalance": 2230248.54,
  "intradayBalances": [
    {
      "timestamp": "2025-08-29T09:00:00",
      "balance": 2220429.95
    },
    {
      "timestamp": "2025-08-29T12:00:00",
      "balance": 2224111.92
    },
    {
      "timestamp": "2025-08-29T15:00:00",
      "balance": 2227793.89
    },
    {
      "timestamp": "2025-08-29T17:00:00",
      "balance": 2230248.54
    }
  ]
}
```

## Gebruik voor testing

### MT940 Conversie Testing
- Gebruik transactie JSON bestanden als input voor MT940 generator
- Vergelijk gegenereerde MT940 bestanden met verwachte output
- Test verschillende transactie types (credit, debit, international, etc.)

### CAMT.053 Conversie Testing
- Gebruik combinatie van transactie en balans JSON bestanden
- Test volledige bank statement generatie
- Valideer XML structuur en content

### API Simulatie
- Gebruik JSON bestanden om Rabobank API responses te simuleren
- Test error handling en edge cases
- Valideer data mapping en transformatie logica

## Ondersteunde transactie types

De converter herkent de volgende Nederlandse bankcodes:

| Code | Category | Type Description |
|------|----------|------------------|
| cb | Card Payment | Credit/Debit Card Payment |
| tb | Transfer | Credit/Debit Transfer |
| db | Direct Debit | Credit/Debit Direct Debit |
| bg | Bank Charges | Credit/Debit Bank Charges |
| wb | International Transfer | Credit/Debit International Wire |
| ok | Online Payment | Credit/Debit Online Payment |
| ei | Electronic Invoice | Credit/Debit Electronic Invoice |
| sb | SEPA Transfer | Credit/Debit SEPA Transfer |

## Technische details

### CSV Parsing
- Ondersteunt quoted fields met escape characters
- Herkent Nederlandse decimaal formaten (comma als decimaal teken)
- Handelt verschillende datum formaten af
- Robuuste error handling voor malformed data

### JSON Generatie
- Volledige compatibiliteit met Rabobank API schema
- Automatische creditor/debtor mapping gebaseerd op transactie richting
- Wisselkoers informatie voor internationale transacties
- Realistische balans simulatie gebaseerd op transactie data

### Prestaties
- Verwerkt grote CSV bestanden efficiënt
- Genereert per-dag bestanden voor optimale performance
- Minimaal geheugengebruik door streaming approach

## Troubleshooting

### Geen CSV bestanden gevonden
- Controleer of bestanden in `TestData` folder staan
- Verifieer bestandsnaam format
- Zorg dat bestanden .csv extensie hebben

### Parsing errors
- Controleer CSV structuur (komma gescheiden, quoted fields)
- Valideer datum formaten in CSV
- Bekijk PowerShell output voor specifieke error details

### Execution Policy errors
- Gebruik altijd `-ExecutionPolicy Bypass` parameter
- Of pas systeem execution policy aan voor de sessie

## Uitbreidingen

De converter kan eenvoudig worden uitgebreid voor:
- Andere bank CSV formaten
- Aanvullende transactie metadata
- Verschillende output formaten (XML, custom JSON schemas)
- Integratie met externe APIs
- Automatische validatie tegen banking standards
