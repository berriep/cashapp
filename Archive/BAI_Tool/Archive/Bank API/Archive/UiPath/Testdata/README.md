# TestData - Daily Closing Test Scenarios

Deze map bevat test JSON bestanden voor het valideren van de Daily Closing reconciliatie logica.

## 📁 Test Scenarios

### 🎯 **Perfect Day** (`perfect`)
**Datum:** 2025-08-31  
**Verwacht resultaat:** ✅ RECONCILED

- **Balance:** €1,250.00 closing
- **Transactions:** 4 transacties (salaris + uitgaven)
- **Net Movement:** +€1,250.00
- **Piggy Banks:** €800.00 (Car + Vacation)
- **Warnings:** Mogelijk HIGH_SAVINGS_RATIO

**Files:**
- `balance_perfect_day.json`
- `transactions_perfect_day.json`

---

### 🚨 **Issues Day** (`issues`)
**Datum:** 2025-08-30  
**Verwacht resultaat:** ❌ ISSUES_FOUND

- **Balance:** €0.00 closing (ZERO_CLOSING_BALANCE)
- **Expected:** €500.00 (PENDING_LARGE_INCOMING)
- **Transactions:** 2 grote uitgaven
- **Net Movement:** -€1,335.50
- **Piggy Banks:** €4,000.00 (HIGH_SAVINGS_RATIO)

**Files:**
- `balance_issues_day.json`
- `transactions_issues_day.json`

---

### 📈 **High Activity Day** (`highactivity`)
**Datum:** 2025-08-29  
**Verwacht resultaat:** ✅ RECONCILED

- **Balance:** €3,456.78 closing
- **Transactions:** 12 transacties (freelance inkomsten + diverse uitgaven)
- **Net Movement:** +€3,140.81
- **Piggy Banks:** €475.50 (Coffee + Books + Charity)
- **Highlights:** Hoge transactie volume, diverse categorieën

**Files:**
- `balance_highactivity_day.json`
- `transactions_highactivity_day.json`

---

## 🧪 **Gebruik in UiPath**

### **Stap 1: Test Data Laden**
```csharp
// Use 10_TestDailyClosing.cs
strTestScenario = "perfect"  // of "issues" of "highactivity"
strTestDataPath = "C:\Users\bpeijmen\Downloads\Zero\Zero\UiPath\TestData\"
```

### **Stap 2: Daily Closing Uitvoeren**
```csharp
// Load test JSON files into JObject variables
jobjBalanceResponse = (from test script)
jobjTransactionResponse = (from test script)
balanceAmount = (extracted closing balance)
strCloseDate = (scenario specific date)

// Run 9_DailyClosing.cs
```

### **Stap 3: Resultaat Valideren**
Vergelijk output met verwachte resultaten per scenario.

---

## 🎛️ **Test Parameters**

| Scenario | Date | Balance | Transactions | Expected Flags | Expected Warnings |
|----------|------|---------|--------------|----------------|-------------------|
| perfect | 2025-08-31 | €1,250 | 4 | None | HIGH_SAVINGS_RATIO |
| issues | 2025-08-30 | €0 | 2 | ZERO_CLOSING_BALANCE, PENDING_LARGE_INCOMING | HIGH_SAVINGS_RATIO |
| highactivity | 2025-08-29 | €3,457 | 12 | None | None |

---

## 🔧 **Aanpassen Test Data**

Om nieuwe scenarios toe te voegen:
1. Maak nieuwe JSON files: `balance_[scenario]_day.json` en `transactions_[scenario]_day.json`
2. Pas test datum aan in `10_TestDailyClosing.cs`
3. Voeg verwachte resultaten toe aan deze README

---

## ✅ **Validatie Checklist**

- [ ] Balance extraction (closingBooked)
- [ ] Transaction parsing (booked array)
- [ ] Net movement calculation
- [ ] Piggy bank totals
- [ ] Reconciliation flags
- [ ] Warning detection
- [ ] Date alignment
- [ ] Currency handling
