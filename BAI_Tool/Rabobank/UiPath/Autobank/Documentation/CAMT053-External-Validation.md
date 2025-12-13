# CAMT.053 Mapping & Validatie - Rabobank Review

## BAI JSON to CAMT.053 Mapping

| BAI JSON Path                             | CAMT.053 XPath                                        | Status            | Remark                                                                           |
| ----------------------------------------- | ----------------------------------------------------- | ----------------- | -------------------------------------------------------------------------------- |
| **Basic Identification**                  |                                                       |                   |                                                                                  |
| `account.iban`                            | `Stmt/Acct/Id/IBAN`                                   | **MANDATORY**     | Account identification                                                           |
| `account.currency`                        | `Stmt/Acct/Ccy`, `Ntry/Amt/@Ccy`                      | Optional          | EUR, USD, etc.                                                                   |
| `entryReference`                          | `Ntry/NtryRef`                                        | Optional          | Unique entry ID from bank                                                        |
| `batchEntryReference`                     | `TxDtls/Refs/TxId`                                    | Optional          | Batch reference                                                                  |
| `accountServicerReference`                | `Ntry/AcctSvcrRef`, `TxDtls/Refs/AcctSvcrRef`         | Optional          | Strongest unique reference                                                       |
| **Header & Statement**                    |                                                       |                   |                                                                                  |
| Not available                             | `GrpHdr/MsgId`                                        | **MANDATORY**     | Message identification - System generated                                        |
| Not available                             | `GrpHdr/CreDtTm`                                      | **MANDATORY**     | Creation date time - System generated                                            |
| Not available                             | `Stmt/Id`                                             | **MANDATORY**     | Statement identification  - System generated                                     |
| Not available                             | `Stmt/CreDtTm`                                        | **MANDATORY**     | Statement creation date time - System generated                                  |
| **Dates**                                 |                                                       |                   |                                                                                  |
| `bookingDate` OR `raboBookingDateTime`    | `Ntry/BookgDt/Dt`                                     | **OPEN QUESTION** | **Which field to use? See question below**                                      |
| `valueDate`                               | `Ntry/ValDt/Dt`                                       | Optional          | Interest date/value date                                                         |
| `raboBookingDateTime`                     | Query basis for transaction selection                 | **USED**          | Source for date filtering (normalized to Europe/Amsterdam)                      |
| Calculated/DB                             | `TxDtls/RltdDts/IntrBkSttlmDt`                        | Optional          | Settlement between banks                                                         |
| **Amounts**                               |                                                       |                   |                                                                                  |
| `transactionAmount.value`                 | `Ntry/Amt`, `TxDtls/AmtDtls/TxAmt/Amt`                | **MANDATORY**     | Amount (always positive in CAMT)                                                |
| `transactionAmount.currency`              | `Ntry/Amt/@Ccy`                                       | **MANDATORY**     | Currency                                                                         |
| Derived from amount                       | `Ntry/CdtDbtInd`                                      | **MANDATORY**     | Credit/Debit indicator (CRDT/DBIT)                                              |
| `balanceAfterBooking.balanceAmount.value` | Not used in CAMT                                      | BAI-specific      | Running balance (only available in BAI)                                         |
| **Balances**                              |                                                       |                   |                                                                                  |
| DB calculated                             | `Bal[Tp/CdOrPrtry/Cd='OPBD']`                         | **MANDATORY**     | Opening booked balance                                                           |
| DB calculated                             | `Bal[Tp/CdOrPrtry/Cd='CLBD']`                         | **MANDATORY**     | Closing booked balance                                                           |
| DB calculated                             | `Bal[Tp/CdOrPrtry/Cd='PRCD']`                         | Optional          | Previous day closing balance                                                     |
| DB calculated                             | `Bal[Tp/CdOrPrtry/Cd='CLAV']`                         | Optional          | Closing available balance                                                        |
| DB calculated                             | `Bal[Tp/CdOrPrtry/Cd='FWAV']`                         | Optional          | Forward available balance                                                        |
| **Transaction Core**                      |                                                       |                   |                                                                                  |
| Fixed: "BOOK"                             | `Ntry/Sts`                                            | **MANDATORY**     | Entry status                                                                     |
| `bankTransactionCode`                     | `Ntry/BkTxCd`, `TxDtls/BkTxCd`                        | **MANDATORY**     | Domain + Family + SubFamily (e.g. PMNT-RCDT-ESCT)                               |
| `raboDetailedTransactionType`             | `BkTxCd/Prtry/Cd`                                     | Optional          | Rabobank codes (100, 541, 586, 625, 699)                                        |
| `purposeCode`                             | `TxDtls/Purp/Cd`                                      | Optional          | SALA, CBFF, EPAY, etc.                                                           |
| Fallback logic                            | `TxDtls/Refs/InstrId`                                 | **MANDATORY**     | Instruction reference                                                            |
| **Counterparties**                        |                                                       |                   |                                                                                  |
| `debtorName`                              | `TxDtls/RltdPties/Dbtr/Nm`                            | Optional          | Name of payer                                                                    |
| `debtorAccount.iban`                      | `TxDtls/RltdPties/DbtrAcct/Id/IBAN`                   | Optional          | IBAN of payer                                                                    |
| `debtorAgent`                             | `TxDtls/RltdAgts/DbtrAgt/FinInstnId/BIC`              | Optional          | BIC (INGBNL2A, ABNANL2A, RABONL2U, SNSBNL2A)                                    |
| `creditorName`                            | `TxDtls/RltdPties/Cdtr/Nm`                            | Optional          | Name of receiver                                                                 |
| `creditorAccount.iban`                    | `TxDtls/RltdPties/CdtrAcct/Id/IBAN`                   | Optional          | IBAN of receiver                                                                 |
| `creditorAgent`                           | `TxDtls/RltdAgts/CdtrAgt/FinInstnId/BIC`              | Optional          | BIC of receiver's bank                                                           |
| **References & Descriptions**             |                                                       |                   |                                                                                  |
| `endToEndId`                              | `TxDtls/Refs/EndToEndId`                              | Optional          | End-to-end reference                                                             |
| `instructionId`                           | `TxDtls/Refs/InstrId`                                 | Optional          | Instruction reference                                                            |
| `remittanceInformationUnstructured`       | `TxDtls/RmtInf/Ustrd`                                 | Optional          | Free description text                                                            |

## Open Questions for Validation

### 1. **HOOFDVRAAG: Welk datumveld is leidend voor BookgDt/Dt?**

**Achtergrond**: BAI API levert twee datumvelden per transactie, en er is een verschil gedetecteerd.

**Concrete casus IBAN NL48RABO0300002343 Entry Reference: 2911595:**
```json
{
  "entryReference": "2911595",
  "bookingDate": "2025-10-05",
  "raboBookingDateTime": "2025-10-05T22:08:03.184628Z",
  "valueDate": "2025-10-06"
}
```

**Tijdzone conversie:**
- `raboBookingDateTime`: 2025-10-05T22:08:03Z (UTC) = **2025-10-06 00:08:03** (Europe/Amsterdam)

**Resultaten:**
- **Origineel Rabobank CAMT.053**: `<BookgDt><Dt>2025-10-06</Dt></BookgDt>`
- **BAI Json**:   "bookingDate": "2025-10-05"
- **Ons gegenereerde CAMT.053**: `<BookgDt><Dt>2025-10-05</Dt></BookgDt>` (gebruikt JSON bookingDate)

**Welke regel moet leidend zijn voor `Ntry/BookgDt/Dt` in CAMT.053?**

**Optie A: JSON bookingDate letterlijk**
- Gebruik: `"bookingDate": "2025-10-05"` → BookgDt = 2025-10-05
- Rationale: Officiële boekingsdatum zoals API levert

**Optie B: raboBookingDateTime genormaliseerd naar Europe/Amsterdam**
- Gebruik: `2025-10-05T22:08:03Z` → Europe/Amsterdam = 2025-10-06 00:08 → BookgDt = 2025-10-06
- Rationale: Daadwerkelijke verwerkingsdatum in Nederlandse tijd (zoals origineel CAMT)

**Verzoek**: Bevestig welke regel Rabobank hanteert voor BookgDt in CAMT.053.

### 2. **BkTxCd family/subfamily combinaties**
Bevestig of de toegepaste BkTxCd family/subfamily combinaties voor de proprietary codes (bijv. 100→PMNT/RCDT/ESCT, 586, 625) aansluiten bij Rabobank richtlijnen.

### 3. **IBAN weergave**
Is het gewenst om Dbtr/Cdtr IBANs altijd te tonen wanneer beschikbaar, of alleen voor specifieke transactiefamilies?

### 4. **Default BIC**
Zijn er richtlijnen voor default BIC wanneer tegenpartij-BIC ontbreekt (bijv. gebruik van eigen bank-BIC)?

### 5. **Aanvullende velden**
Zijn er aanvullende optionele velden die Rabobank verwacht op statement- of transactie-detailniveau?

## Examples 
**CAMT053**
```xml
<Ntry>
  <NtryRef>2911595</NtryRef>
  <Amt Ccy="EUR">384.10</Amt>
  <CdtDbtInd>CRDT</CdtDbtInd>
  <Sts>BOOK</Sts>
  <BookgDt>
    <Dt>2025-10-06</Dt>
  </BookgDt>
  <ValDt>
    <Dt>2025-10-06</Dt>
  </ValDt>
  <AcctSvcrRef>43011075189:CI49CT</AcctSvcrRef>
  <BkTxCd>
    <Domn>
      <Cd>PMNT</Cd>
      <Fmly>
        <Cd>RCDT</Cd>
        <SubFmlyCd>ESCT</SubFmlyCd>
      </Fmly>
    </Domn>
    <Prtry>
      <Cd>100</Cd>
      <Issr>RABOBANK</Issr>
    </Prtry>
  </BkTxCd>
  <NtryDtls>
    <TxDtls>
      <Refs>
        <AcctSvcrRef>OO9T005069862466</AcctSvcrRef>
        <InstrId>OO9T005069862466</InstrId>
        <EndToEndId>06-10-2025 00:07 7020056313469725</EndToEndId>
      </Refs>
      <AmtDtls>
        <TxAmt>
          <Amt Ccy="EUR">384.10</Amt>
        </TxAmt>
        <PrtryAmt>
          <Tp>IBS</Tp>
          <Amt Ccy="EUR">384.10</Amt>
        </PrtryAmt>
      </AmtDtls>
      <BkTxCd>
        <Domn>
          <Cd>PMNT</Cd>
          <Fmly>
            <Cd>RCDT</Cd>
            <SubFmlyCd>ESCT</SubFmlyCd>
          </Fmly>
        </Domn>
        <Prtry>
          <Cd>100</Cd>
          <Issr>RABOBANK</Issr>
        </Prtry>
      </BkTxCd>
      <RltdPties>
        <Dbtr>
          <Nm>I.L.B.M. Elzinga</Nm>
        </Dbtr>
        <DbtrAcct>
          <Id>
            <IBAN>NL85RABO0105474967</IBAN>
          </Id>
        </DbtrAcct>
      </RltdPties>
      <RltdAgts>
        <DbtrAgt>
          <FinInstnId>
            <BIC>RABONL2U</BIC>
          </FinInstnId>
        </DbtrAgt>
      </RltdAgts>
      <Purp>
        <Cd>EPAY</Cd>
      </Purp>
      <RmtInf>
        <Ustrd>8988204469 7020056313469725 KV xui-16389167</Ustrd>
      </RmtInf>
      <RltdDts>
        <IntrBkSttlmDt>2025-10-06</IntrBkSttlmDt>
      </RltdDts>
    </TxDtls>
  </NtryDtls>
</Ntry>
```

**BAI_JSON**
```json
{
  "bookingDate": "2025-10-05",
  "creditorAccount": {
    "currency": "EUR",
    "iban": "NL48RABO0300002343"
  },
  "debtorAccount": {
    "iban": "NL85RABO0105474967"
  },
  "debtorAgent": "RABONL2U",
  "debtorName": "I.L.B.M. Elzinga",
  "endToEndId": "06-10-2025 00:07 7020056313469725",
  "entryReference": "2911595",
  "purposeCode": "EPAY",
  "raboBookingDateTime": "2025-10-05T22:08:03.184628Z",
  "raboDetailedTransactionType": "100",
  "raboTransactionTypeName": "id",
  "remittanceInformationUnstructured": "8988204469 7020056313469725 KV xui-16389167",
  "transactionAmount": {
    "value": "384.10",
    "currency": "EUR"
  },
  "valueDate": "2025-10-06",
  "balanceAfterBooking": {
    "balanceType": "InterimBooked",
    "balanceAmount": {
      "value": "540882.94",
      "currency": "EUR"
    }
  },
  "bankTransactionCode": "PMNT-RCDT-ESCT"
}
```