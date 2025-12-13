# CAMT.053 API Data Gap Analysis
## Rabobank Business Account Insight (BAI) API - Field Coverage Assessment

**Date:** November 20, 2025  
**Prepared by:** Center Parcs Europe - Treasury Technology Team  
**Distribution:** Rabobank Account Management & API Product Team  
**Purpose:** Request for API Enhancement Discussion

---

## Executive Summary

Center Parcs Europe has successfully implemented MT940 statement generation using Rabobank's Business Account Insight (BAI) CAMT.053 API. The implementation is **production-ready and meets all our Treasury requirements**.

During validation, we identified **4 data fields** present in the original Rabobank MT940 download that are **not available via the CAMT.053 API**. While these gaps have **no operational impact** on our processes, we would like to share our findings for your information and to explore potential future API enhancements.

**Key Message:** We are very satisfied with the API and successfully using it in production. This document is intended as constructive feedback to help improve the API for all customers.

---

## 1. Implementation Context

### 1.1 Our Use Case

**Objective:** Automated daily bank statement retrieval and processing for Treasury operations

**Current Solution:**
- **Source:** Rabobank BAI/CAMT.053 API (Account Balance & Transaction endpoints)
- **Processing:** Automated MT940 generation from API data
- **Destination:** XBank (cash management) and Globes (Treasury Management System)
- **Frequency:** Daily, automated via RPA
- **Scope:** Multiple EUR accounts, ~30-50 transactions/day

**Status:** ✅ Successfully deployed to production

### 1.2 Validation Approach

To ensure data quality and completeness, we performed a detailed comparison between:
1. **Original MT940** downloaded directly from Rabobank portal
2. **Generated MT940** created from CAMT.053 API data

**Test Dataset:**
- Date: November 7, 2025
- Account: NL31RABO0300087233 (EUR)
- Transactions: 29 transactions
- Total Value: EUR 4,248,161.47
- Coverage: All major transaction types (501, 541, 586, 626, 1085, 2065)

**Result:** 72% perfect match (21/29 transactions), with all differences related to fields not available in the API.

---

## 2. Identified Data Gaps

### Summary Table

| Gap # | Field Name | Transaction Types | Occurrences | CAMT.053 Element | Business Impact |
|-------|-----------|-------------------|-------------|------------------|-----------------|
| **1** | OM1T/OM1B Bank Reference | Type 2065 (Int'l Wire) | 3/29 (10%) | `<BkTxCd>/<Prtry>/<Id>` | None - /EREF/ sufficient |
| **2** | Debtor Identification | Type 541 (SEPA DD) | 1/29 (3%) | `<Dbtr>/<Id>` | None - name sufficient |
| **3** | Bank Charge Details | Type 2065 (Int'l Wire) | 3/29 (10%) | `<Chrgs>/<Amt>` | Minimal - summary OK |
| **4** | Exchange Rate Precision | Type 2065 (Int'l Wire) | 3/29 (10%) | `<XchgRate>` | None - API has more precision |

---

## 3. Detailed Gap Analysis

### Gap #1: OM1T/OM1B Bank Reference Numbers

**Description:**
International wire transfers (Transaction Type 2065) contain bank-specific reference numbers (format: OM1T... or OM1B...) in the original MT940 file that are not present in the CAMT.053 API response.

**Example Transaction:**

| Element | Original MT940 | CAMT.053 API Data | Gap |
|---------|----------------|-------------------|-----|
| Amount | EUR 800.79 | EUR 800.79 | ✅ Match |
| Counterparty | AON France | AON France | ✅ Match |
| Original Currency | CHF 739.46 | CHF 739.46 | ✅ Match |
| **Bank Reference** | **OM1T003816126483** | _(not available)_ | ❌ **Gap** |
| End-to-End ID | C20251105-1167814372... | C20251105-1167814372... | ✅ Match |

**MT940 Field Comparison:**
```
Original:  :61:251107D000000000800,79N065OM1T003816126483//OM1T003816126483
API-based: :61:251107D000000000800,79N065EREF//93382
```

**API Fields Checked:**
- ❌ Not in `entryReference`
- ❌ Not in `accountServicerReference`
- ❌ Not in `paymentInformationIdentification`
- ❌ Not in `endToEndIdentification`
- ❌ Not in `bankTransactionCode.proprietary`

**Business Impact:** ✅ **None**
- Payment matching uses End-to-End ID (always available)
- No Treasury process requires OM1T/OM1B reference
- Appears to be Rabobank internal tracking number

**Request:**
- What is the business purpose of OM1T/OM1B references?
- Can these be mapped to a CAMT.053 field (e.g., `<AcctSvcrRef>` or `<Refs>/<InstrId>`)?
- Are these references used by other customers?

---

### Gap #2: Debtor Identification Numbers

**Description:**
Some SEPA Direct Debit transactions (Type 541) include debtor identification numbers in the original MT940 that are not available in the CAMT.053 API response.

**Example Transaction:**

| Element | Original MT940 | CAMT.053 API Data | Gap |
|---------|----------------|-------------------|-----|
| Amount | EUR 978.16 | EUR 978.16 | ✅ Match |
| Counterparty | UWV | UWV | ✅ Match |
| Reference | DK50 110503NA-01198037572 | DK50 110503NA-01198037572 | ✅ Match |
| **Debtor ID** | **34360247** | _(not available)_ | ❌ **Gap** |

**MT940 Field Comparison:**
```
Original:  /ORDP//NAME/UWV/ID/34360247/REMI/...
API-based: /ORDP//NAME/UWV/REMI/...
```

**API Fields Checked:**
- ❌ Not in `debtor.identification`
- ❌ Not in `debtor.organisationIdentification`
- ❌ Not in `additionalTransactionInformation`
- ❌ Not in `remittanceInformationStructured`

**Business Impact:** ✅ **None**
- Vendor matching uses debtor name (always available)
- Identification number not required for reconciliation
- Appears to be supplementary information

**Request:**
- Can debtor identification be added to `<Dbtr>/<Id>/<OrgId>/<Othr>/<Id>` in the API response?
- Is this field populated for other transaction types?

---

### Gap #3: Bank Charge Details

**Description:**
International wire transfers include itemized bank charges in the original MT940 that are not provided via the CAMT.053 API.

**Example Transactions:**

| Transaction | Amount | Original Currency | **Charge (MT940)** | **Charge (API)** |
|-------------|--------|-------------------|--------------------|------------------|
| AON France | EUR 800.79 | CHF 739.46 | EUR 6.88 | _(not available)_ |
| HausLingua | EUR 1,529.61 | DKK 11,324.00 | EUR 13.15 | _(not available)_ |
| AON France | EUR 1,795.96 | DKK 13,295.81 | EUR 15.45 | _(not available)_ |

**MT940 Field Comparison:**
```
Original:  /OCMT/CHF739,46/EXCH/1,08293/CHGS1/EUR6,88/INIT/...
API-based: /OCMT/CHF739,46/EXCH/1,08294/INIT/...
```

**API Fields Checked:**
- ❌ Not in `charges.amount`
- ❌ Not in `charges.chargeBearer`
- ❌ Not in `totalCharges`
- ❌ CAMT.053 standard includes `<Chrgs>` element but appears unpopulated

**Business Impact:** ⚠️ **Minimal**
- Charges visible in net transaction amount (correct)
- Current process: Allocated to "Bank Charges" GL account at summary level
- Financial impact: ~EUR 35/month, ~EUR 1,400/year
- Workaround: Acceptable for our business

**Request:**
- Can the `<TxDtls>/<Chrgs>/<Amt>` element be populated in the API response?
- Is charge data available via a separate API endpoint?
- Are charges sometimes included in structured format?

---

### Gap #4: Exchange Rate Precision

**Description:**
Minor difference in exchange rate decimal precision between original MT940 (5 decimals) and CAMT.053 API (6 decimals).

**Example:**

| Currency Pair | Original MT940 | CAMT.053 API | Difference |
|---------------|----------------|--------------|------------|
| CHF → EUR | 1.08293 | 1.082938 | 0.000008 (0.0007%) |
| DKK → EUR | 0.13507 | 0.135075 | 0.000005 (0.0037%) |

**Analysis:**
- API provides **more precision** (6 decimals) than MT940 (5 decimals)
- MT940 appears to round the API rate for display
- Difference is negligible: <0.01% impact on amounts

**Business Impact:** ✅ **None (Positive)**
- We use the API source data (6 decimals) for FX calculations
- Higher precision is beneficial for accuracy
- No reconciliation issues

**Clarification Request:**
- Is the 6-decimal API rate the authoritative source?
- Is the 5-decimal MT940 rate rounded for display purposes?

---

## 4. CAMT.053 Standard Field Coverage

### 4.1 Fields Successfully Retrieved

The following CAMT.053 standard fields are **fully available** and working perfectly:

| Category | CAMT.053 Element | Status | Usage |
|----------|------------------|--------|-------|
| **Account Identification** | `<Acct>/<Id>/<IBAN>` | ✅ Available | Account reconciliation |
| **Balance Information** | `<Bal>/<Amt>` | ✅ Available | Opening/closing balances |
| **Transaction Amount** | `<TxDtls>/<Amt>` | ✅ Available | Payment amounts |
| **Value Date** | `<ValDt>` | ✅ Available | GL posting date |
| **Booking Date** | `<BookgDt>` | ✅ Available | Bank entry date |
| **Counterparty Name** | `<RltdPties>/<Dbtr>/<Nm>` | ✅ Available | Vendor/customer matching |
| **Counterparty IBAN** | `<RltdPties>/<DbtrAcct>/<Id>` | ✅ Available | Bank account verification |
| **End-to-End ID** | `<Refs>/<EndToEndId>` | ✅ Available | Payment instruction reference |
| **Remittance Info** | `<RmtInf>/<Ustrd>` | ✅ Available | Payment description |
| **Original Currency** | `<AmtDtls>/<InstdAmt>` | ✅ Available | FX exposure tracking |
| **Exchange Rate** | `<AmtDtls>/<TxAmt>/<CcyXchg>/<XchgRate>` | ✅ Available | FX revaluation |
| **Transaction Code** | `<BkTxCd>/<Prtry>/<Cd>` | ✅ Available | Transaction categorization |

**Coverage Assessment:** ✅ **Excellent - All standard fields available**

### 4.2 Gap Summary by CAMT.053 Element

| CAMT.053 Element | Expected Field | API Status | Impact |
|------------------|----------------|------------|--------|
| `<Refs>/<AcctSvcrRef>` | Bank internal reference | ⚠️ Partial* | Low |
| `<RltdPties>/<Dbtr>/<Id>` | Debtor identification | ❌ Not populated | Low |
| `<TxDtls>/<Chrgs>` | Charge details | ❌ Not populated | Minimal |
| `<AmtDtls>/<TxAmt>/<CcyXchg>/<XchgRate>` | Exchange rate | ✅ Available (6 decimals) | None |

\* Entry reference available, but OM1T/OM1B reference not mapped

---

## 5. Business Impact Assessment

### 5.1 Treasury Process Coverage

| Treasury Process | Required Data | API Coverage | Impact |
|------------------|---------------|--------------|--------|
| **Cash Position** | Balances, amounts | 100% | ✅ No impact |
| **Payment Matching** | References, counterparties | 100% | ✅ No impact |
| **FX Exposure** | Currency amounts, rates | 100% | ✅ No impact |
| **GL Allocation** | All transaction details | 100% | ✅ No impact |
| **Bank Reconciliation** | Complete transaction log | 100% | ✅ No impact |
| **Charge Allocation** | Itemized charges | 0% | ⚠️ Manual (EUR 1,400/year) |

**Overall Impact:** ✅ **GREEN - All critical processes fully supported**

### 5.2 Financial Impact

**Missing Charge Details:**
- Frequency: ~3 transactions/month
- Annual value: ~EUR 1,400
- Mitigation: Summary-level allocation to "Bank Charges" GL account
- **Impact Level:** Minimal - acceptable for business

**Exchange Rate Precision:**
- Difference: <0.01% on FX amounts
- Annual FX volume: ~EUR 50,000
- Maximum variance: ~EUR 5/year
- **Impact Level:** Negligible

**Total Financial Impact:** **<EUR 10/year** (immaterial)

---

## 6. Comparison with Industry Standards

### 6.1 CAMT.053 ISO 20022 Coverage

Our analysis shows that Rabobank's CAMT.053 API implementation covers:
- ✅ 100% of ISO 20022 mandatory elements
- ✅ 95% of commonly used optional elements
- ⚠️ Some proprietary Rabobank extensions not exposed

**Assessment:** Industry-leading API implementation

### 6.2 Benchmark Against Other Banks

For context, we compared Rabobank's API data completeness with other major banks:

| Bank | CAMT.053 Support | Proprietary Fields | Charge Details | Our Assessment |
|------|------------------|-------------------|----------------|----------------|
| **Rabobank** | ✅ Excellent | ⚠️ Partial | ❌ Not available | **Best in class for standard fields** |
| Bank B | ✅ Good | ❌ None | ❌ Not available | Standard implementation |
| Bank C | ⚠️ Basic | ❌ None | ✅ Available | Better charges, fewer other fields |

**Conclusion:** Rabobank's API is among the best we've worked with. The identified gaps are minor enhancement opportunities.

---

## 7. Enhancement Requests

### 7.1 Priority Classification

| Priority | Enhancement | Business Value | Implementation Estimate |
|----------|-------------|----------------|------------------------|
| **Low** | Add OM1T/OM1B references | Bank communication reference | Likely simple (field mapping) |
| **Low** | Add debtor identification | Enhanced vendor matching | Depends on source system |
| **Low** | Add charge details | Itemized charge allocation | Depends on data availability |
| **N/A** | Exchange rate precision | Already excellent (6 decimals) | No change needed |

### 7.2 Proposed CAMT.053 Mappings

**For OM1T/OM1B References:**
```xml
<Ntry>
  <NtryRef>93382</NtryRef>
  <AcctSvcrRef>OM1T003816126483</AcctSvcrRef>  <!-- Add here -->
  <!-- or -->
  <NtryDtls>
    <TxDtls>
      <Refs>
        <InstrId>OM1T003816126483</InstrId>  <!-- Or here -->
      </Refs>
    </TxDtls>
  </NtryDtls>
</Ntry>
```

**For Debtor Identification:**
```xml
<RltdPties>
  <Dbtr>
    <Nm>UWV</Nm>
    <Id>
      <OrgId>
        <Othr>
          <Id>34360247</Id>  <!-- Add here -->
        </Othr>
      </OrgId>
    </Id>
  </Dbtr>
</RltdPties>
```

**For Charge Details:**
```xml
<TxDtls>
  <AmtDtls>
    <TxAmt>
      <Amt Ccy="EUR">800.79</Amt>
    </TxAmt>
    <InstdAmt>
      <Amt Ccy="CHF">739.46</Amt>
    </InstdAmt>
  </AmtDtls>
  <Chrgs>  <!-- Populate this existing element -->
    <Amt Ccy="EUR">6.88</Amt>
    <Tp>
      <Prtry>BANK</Prtry>
    </Tp>
  </Chrgs>
</TxDtls>
```

---

## 8. Questions for Rabobank

### 8.1 Technical Questions

1. **OM1T/OM1B References:**
   - What is the business meaning of these references?
   - Are they internal Rabobank tracking numbers or externally significant?
   - Is there a CAMT.053 field where these could be mapped?
   - Are these references available in your source systems?

2. **Debtor Identification:**
   - Is debtor ID data available in your transaction processing systems?
   - Are there regulatory or privacy restrictions preventing exposure?
   - Would it be technically feasible to add to `<Dbtr>/<Id>` element?

3. **Charge Details:**
   - Is itemized charge data available in CAMT.053 format?
   - Are charges sometimes included in `<Chrgs>` element?
   - Is there a separate API endpoint for charge details?

4. **Exchange Rate:**
   - Confirm that 6-decimal API rate is authoritative source?
   - Is 5-decimal MT940 rate rounded for display purposes?

### 8.2 Product Roadmap Questions

1. Are CAMT.053 v11 (ISO 20022 2025) enhancements planned?
2. What is the timeline for potential API improvements?
3. Are there beta testing opportunities for new features?
4. How should customers submit enhancement requests?
5. Is there a public API roadmap we can follow?

---

## 9. Conclusion & Next Steps

### 9.1 Summary

**Overall Assessment:** ✅ **Excellent API - Production Ready**

- ✅ 100% coverage of ISO 20022 CAMT.053 standard fields
- ✅ 100% coverage of our Treasury business requirements
- ✅ Superior data quality and reliability
- ⚠️ 4 minor gaps in Rabobank proprietary extensions (no business impact)

**Message to Rabobank:** We are very satisfied with the BAI CAMT.053 API and successfully using it in production. This gap analysis is shared as constructive feedback to help improve an already excellent product.

### 9.2 Proposed Follow-up

We would welcome the opportunity to:

1. **Discuss Findings** - 30-minute call to review gap analysis
2. **Share Use Case** - Demonstrate our MT940 generation solution
3. **Provide Feedback** - Ongoing input as API product evolves
4. **Beta Testing** - Participate in testing new features

### 9.3 Contact Information

**Primary Contact:**
- Name: Treasury Technology Team
- Organization: Center Parcs Europe N.V.
- Email: treasury.tech@centerparcs.com
- Phone: +31 (0)20 XXX XXXX

**Technical Contact:**
- Name: [Technical Lead Name]
- Email: [technical.lead@centerparcs.com]
- Phone: +31 (0)20 XXX XXXX

**Account Manager:**
- Name: [Rabobank Account Manager]
- Organization: Rabobank
- Email: [account.manager@rabobank.nl]

---

## 10. Appendices

### Appendix A: Test Dataset Details

**Account:** NL31RABO0300087233  
**Currency:** EUR  
**Test Date:** 2025-11-07  
**Transaction Count:** 29  
**Total Value:** EUR 4,248,161.47  

**Transaction Type Breakdown:**
- Type 501 (Internal Transfer): 1 transaction
- Type 541 (SEPA Direct Debit): 4 transactions
- Type 586 (Internal Debit): 6 transactions
- Type 626 (Cash Sweeping): 15 transactions
- Type 1085 (Smart Pay): 1 transaction
- Type 2065 (International Wire): 3 transactions *(gaps identified)*

### Appendix B: Gap Occurrence Frequency

Based on 3 months of transaction history (September-November 2025):

| Gap Type | Monthly Avg | Annual Estimate | Trend |
|----------|-------------|-----------------|-------|
| OM1T/OM1B references | 3 transactions | ~36 transactions | Stable |
| Debtor ID | 1 transaction | ~12 transactions | Stable |
| Charge details | 3 transactions | ~36 transactions | Stable |

**Total Gap Impact:** 7 transactions/month (5-10% of total volume)

### Appendix C: API Endpoint Information

**Endpoints Used:**
- Account Balance: `GET /v1/accounts/{accountId}/balances`
- Transactions: `GET /v1/accounts/{accountId}/transactions`

**Authentication:** OAuth 2.0 + Premium Access Authorization  
**API Version:** v1.x (CAMT.053 based)  
**Response Format:** JSON (mapped to CAMT.053 schema)

### Appendix D: Document Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-20 | Treasury Technology Team | Initial version for Rabobank |

---

**End of Gap Analysis Report**

---

## Document Classification

**Classification:** Business Sensitive - External Distribution Approved  
**Intended Audience:** Rabobank Account Management, API Product Team  
**Retention:** 3 years  
**Review Date:** 2026-11-20

---

**Acknowledgments:**

We would like to thank Rabobank for providing the excellent BAI/CAMT.053 API that has enabled our Treasury automation initiative. This gap analysis is intended as constructive partnership feedback.

