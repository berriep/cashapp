' CAMT.053 Generation Parameters
' minimum_required_fields: Boolean parameter to control field inclusion
'   True = Include only mandatory fields per ISO 20022 specification
'   False = Include all available optional fields (current behavior)
' include_clav_in_export: Boolean parameter to control CLAV export inclusion
'   True = Include CLAV balance in XML export (default)
'   False = Exclude CLAV from XML export (calculation still performed for validation)

' Parameter wordt doorgegeven via invoke code arguments
Dim minimumRequiredFields As Boolean = False ' Default to full fields
Dim includeClavInExport As Boolean = True ' Default to include CLAV

' Try to get parameters if passed (for UiPath or other callers)
Try
    If Not IsNothing(minimum_required_fields) AndAlso Not IsDBNull(minimum_required_fields) Then
        minimumRequiredFields = Convert.ToBoolean(minimum_required_fields)
    End If
Catch
    ' Parameter not available, use default
End Try

Try
    If Not IsNothing(include_clav_in_export) AndAlso Not IsDBNull(include_clav_in_export) Then
        includeClavInExport = Convert.ToBoolean(include_clav_in_export)
    End If
Catch
    ' Parameter not available, use default
End Try

' Helper function to safely get column value
Dim GetSafeColumnValue As Func(Of DataRow, String, String) = Function(row, columnName)
    If row.Table.Columns.Contains(columnName) AndAlso row(columnName) IsNot Nothing Then
        Return row(columnName).ToString()
    Else
        Return ""
    End If
End Function

' Controleer input
If dt_camt053_data Is Nothing OrElse dt_camt053_data.Rows.Count = 0 Then
    Throw New Exception("dt_camt053_data is leeg")
End If

If dt_camt053_tx Is Nothing OrElse dt_camt053_tx.Rows.Count = 0 Then
    Throw New Exception("dt_camt053_tx is leeg")
End If

' Haal data op uit eerste rij
Dim balanceRow As DataRow = dt_camt053_data.Rows(0)
Dim iban As String = balanceRow("iban").ToString()
Dim ownerName As String = balanceRow("owner_name").ToString()
Dim reportDate As DateTime = Convert.ToDateTime(balanceRow("day"))
Dim currency As String = balanceRow("currency").ToString()
Dim openingBalance As Decimal = Convert.ToDecimal(balanceRow("opening_balance"))
Dim closingBalance As Decimal = Convert.ToDecimal(balanceRow("closing_balance"))
Dim transactionCount As Integer = Convert.ToInt32(balanceRow("transaction_count"))

' Calculate Closing Available Balance (CLAV) according to bank guidance
' CLAV = closing_balance - pending transactions with value_date > report_date
Dim clavBalance As Decimal = closingBalance
Dim pendingTransactionsSum As Decimal = 0
Dim pendingTransactionsCount As Integer = 0

' Process all transactions to find pending ones (value date > report date)
For Each txRow As DataRow In dt_camt053_tx.Rows
    Try
        Dim txValueDate As DateTime = Convert.ToDateTime(txRow("value_date"))
        Dim txAmount As Decimal = Convert.ToDecimal(txRow("transaction_amount"))
        
        ' Bank guidance: transactions with value date > report date are "pending"
        ' These should be subtracted from closing balance to get available balance
        If txValueDate > reportDate Then
            pendingTransactionsSum += txAmount
            pendingTransactionsCount += 1
        End If
    Catch ex As Exception
        ' Skip invalid transaction data and continue
        Console.WriteLine("Warning: Error processing transaction for CLAV calculation: " & ex.Message)
    End Try
Next

' Calculate CLAV: closing balance minus pending transactions
clavBalance = closingBalance - pendingTransactionsSum

' Log CLAV calculation for audit trail
Console.WriteLine(String.Format("CLAV Calculation for {0} on {1}:", iban, reportDate.ToString("yyyy-MM-dd")))
Console.WriteLine(String.Format("  Closing Balance: {0:F2} {1}", closingBalance, currency))
Console.WriteLine(String.Format("  Pending Transactions ({2} items): {0:F2} {1}", pendingTransactionsSum, currency, pendingTransactionsCount))
Console.WriteLine(String.Format("  Calculated CLAV: {0:F2} {1}", clavBalance, currency))
Console.WriteLine(String.Format("  Include CLAV in Export: {0}", includeClavInExport))

' Maak output directory
Dim outputDir As String = "C:\temp"
Directory.CreateDirectory(outputDir)

' Bestandsnaam met indicator voor minimale vs volledige versie en CLAV export status
Dim fileType As String = If(minimumRequiredFields, "minimal", "full")
If Not minimumRequiredFields AndAlso Not includeClavInExport Then
    fileType = "full_no_clav"
End If
Dim fileName As String = String.Format("camt053_{0}_{1}_{2}_{3}.xml", 
    fileType,
    iban, 
    reportDate.ToString("yyyyMMdd"), 
    DateTime.Now.ToString("HHmmss"))
camt053FilePath = Path.Combine(outputDir, fileName)

' Bouw XML met StringBuilder
Dim xml As New StringBuilder()
xml.AppendLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
xml.AppendLine("<Document xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns=""urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"">")
xml.AppendLine("  <BkToCstmrStmt>")

' Header
xml.AppendLine("    <GrpHdr>")
' MsgId: CAMT053RBB + 12 digits (total 22 chars)
Dim msgIdSuffix As String = DateTime.Now.ToString("yyyyMMddHHmm")
xml.AppendLine(String.Format("      <MsgId>CAMT053RBB{0}</MsgId>", msgIdSuffix))
' CreDtTm: Report date with time and timezone +01:00
xml.AppendLine(String.Format("      <CreDtTm>{0}T00:00:00.000000+01:00</CreDtTm>", reportDate.ToString("yyyy-MM-dd")))
xml.AppendLine("    </GrpHdr>")

' Statement
xml.AppendLine("    <Stmt>")
' Statement Id: CAMT053 + 17 digits (total 24 chars)
Dim stmtIdSuffix As String = reportDate.ToString("yyyyMMdd") & DateTime.Now.ToString("HHmmssff") & "1"
xml.AppendLine(String.Format("      <Id>CAMT053{0}</Id>", stmtIdSuffix))
xml.AppendLine("      <ElctrncSeqNb>1</ElctrncSeqNb>")
' CreDtTm: Report date with time and timezone +01:00
xml.AppendLine(String.Format("      <CreDtTm>{0}T00:00:00.000000+01:00</CreDtTm>", reportDate.ToString("yyyy-MM-dd")))

' Account (verplichte velden: Id, optionele velden: Ccy, Nm)
xml.AppendLine("      <Acct>")
xml.AppendLine("        <Id>")
xml.AppendLine(String.Format("          <IBAN>{0}</IBAN>", iban))
xml.AppendLine("        </Id>")
If Not minimumRequiredFields Then
    xml.AppendLine(String.Format("        <Ccy>{0}</Ccy>", currency))
    xml.AppendLine(String.Format("        <Nm>{0}</Nm>", Security.SecurityElement.Escape(ownerName)))
End If
xml.AppendLine("      </Acct>")

' Balances - Minimale versie: alleen OPBD en CLBD (verplicht)
' Volledige versie: PRCD, OPBD, CLBD, CLAV, FWAV (optioneel)

If Not minimumRequiredFields Then
    ' Previous Day Closing Balance (PRCD) - alleen in volledige versie
    xml.AppendLine("      <Bal>")
    xml.AppendLine("        <Tp>")
    xml.AppendLine("          <CdOrPrtry>")
    xml.AppendLine("            <Cd>PRCD</Cd>")
    xml.AppendLine("          </CdOrPrtry>")
    xml.AppendLine("        </Tp>")
    xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, openingBalance.ToString("F2")))
    xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(openingBalance >= 0, "CRDT", "DBIT")))
    xml.AppendLine("        <Dt>")
    xml.AppendLine(String.Format("          <Dt>{0}</Dt>", reportDate.AddDays(-1).ToString("yyyy-MM-dd")))
    xml.AppendLine("        </Dt>")
    xml.AppendLine("      </Bal>")
End If

' Opening Balance (OPBD) - verplicht
xml.AppendLine("      <Bal>")
xml.AppendLine("        <Tp>")
xml.AppendLine("          <CdOrPrtry>")
xml.AppendLine("            <Cd>OPBD</Cd>")
xml.AppendLine("          </CdOrPrtry>")
xml.AppendLine("        </Tp>")
xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, openingBalance.ToString("F2")))
xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(openingBalance >= 0, "CRDT", "DBIT")))
xml.AppendLine("        <Dt>")
xml.AppendLine(String.Format("          <Dt>{0}</Dt>", reportDate.ToString("yyyy-MM-dd")))
xml.AppendLine("        </Dt>")
xml.AppendLine("      </Bal>")

' Closing Balance (CLBD) - verplicht
xml.AppendLine("      <Bal>")
xml.AppendLine("        <Tp>")
xml.AppendLine("          <CdOrPrtry>")
xml.AppendLine("            <Cd>CLBD</Cd>")
xml.AppendLine("          </CdOrPrtry>")
xml.AppendLine("        </Tp>")
xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, closingBalance.ToString("F2")))
xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(closingBalance >= 0, "CRDT", "DBIT")))
xml.AppendLine("        <Dt>")
xml.AppendLine(String.Format("          <Dt>{0}</Dt>", reportDate.ToString("yyyy-MM-dd")))
xml.AppendLine("        </Dt>")
xml.AppendLine("      </Bal>")

If Not minimumRequiredFields AndAlso includeClavInExport Then
    ' Closing Available Balance (CLAV) - alleen in volledige versie en als export enabled
    ' Uses calculated CLAV balance based on bank guidance (excluding pending transactions)
    xml.AppendLine("      <Bal>")
    xml.AppendLine("        <Tp>")
    xml.AppendLine("          <CdOrPrtry>")
    xml.AppendLine("            <Cd>CLAV</Cd>")
    xml.AppendLine("          </CdOrPrtry>")
    xml.AppendLine("        </Tp>")
    xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, clavBalance.ToString("F2")))
    xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(clavBalance >= 0, "CRDT", "DBIT")))
    xml.AppendLine("        <Dt>")
    xml.AppendLine(String.Format("          <Dt>{0}</Dt>", reportDate.ToString("yyyy-MM-dd")))
    xml.AppendLine("        </Dt>")
    xml.AppendLine("      </Bal>")

    ' Forward Available Balances (FWAV) - alleen in volledige versie
    For i As Integer = 1 To 4
        xml.AppendLine("      <Bal>")
        xml.AppendLine("        <Tp>")
        xml.AppendLine("          <CdOrPrtry>")
        xml.AppendLine("            <Cd>FWAV</Cd>")
        xml.AppendLine("          </CdOrPrtry>")
        xml.AppendLine("        </Tp>")
        xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, closingBalance.ToString("F2")))
        xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(closingBalance >= 0, "CRDT", "DBIT")))
        xml.AppendLine("        <Dt>")
        xml.AppendLine(String.Format("          <Dt>{0}</Dt>", reportDate.AddDays(i).ToString("yyyy-MM-dd")))
        xml.AppendLine("        </Dt>")
        xml.AppendLine("      </Bal>")
    Next
End If

' Transaction Summary - volledig optioneel, alleen in volledige versie
If Not minimumRequiredFields Then
    ' Calculate totals for transaction summary
    Dim totalEntries As Integer = dt_camt053_tx.Rows.Count
    Dim totalCreditEntries As Integer = 0
    Dim totalDebitEntries As Integer = 0
    Dim totalCreditSum As Decimal = 0
    Dim totalDebitSum As Decimal = 0
    Dim totalSum As Decimal = 0

    For Each row As DataRow In dt_camt053_tx.Rows
        Dim amount As Decimal = Convert.ToDecimal(row("transaction_amount"))
        totalSum += Math.Abs(amount)
        If amount >= 0 Then
            totalCreditEntries += 1
            totalCreditSum += amount
        Else
            totalDebitEntries += 1
            totalDebitSum += Math.Abs(amount)
        End If
    Next

    Dim netAmount As Decimal = totalCreditSum - totalDebitSum

    xml.AppendLine("      <TxsSummry>")
    xml.AppendLine("        <TtlNtries>")
    xml.AppendLine(String.Format("          <NbOfNtries>{0}</NbOfNtries>", totalEntries))
    xml.AppendLine(String.Format("          <Sum>{0}</Sum>", totalSum.ToString("F2")))
    xml.AppendLine(String.Format("          <TtlNetNtryAmt>{0}</TtlNetNtryAmt>", Math.Abs(netAmount).ToString("F2")))
    xml.AppendLine(String.Format("          <CdtDbtInd>{0}</CdtDbtInd>", If(netAmount >= 0, "CRDT", "DBIT")))
    xml.AppendLine("        </TtlNtries>")
    If totalCreditEntries > 0 Then
        xml.AppendLine("        <TtlCdtNtries>")
        xml.AppendLine(String.Format("          <NbOfNtries>{0}</NbOfNtries>", totalCreditEntries))
        xml.AppendLine(String.Format("          <Sum>{0}</Sum>", totalCreditSum.ToString("F2")))
        xml.AppendLine("        </TtlCdtNtries>")
    End If
    If totalDebitEntries > 0 Then
        xml.AppendLine("        <TtlDbtNtries>")
        xml.AppendLine(String.Format("          <NbOfNtries>{0}</NbOfNtries>", totalDebitEntries))
        xml.AppendLine(String.Format("          <Sum>{0}</Sum>", totalDebitSum.ToString("F2")))
        xml.AppendLine("        </TtlDbtNtries>")
    End If

    ' Generate TtlNtriesPerBkTxCd summaries by grouping transactions by proprietary code
    Dim txCodeGroups As New Dictionary(Of String, Tuple(Of Integer, Decimal, Decimal))
    For Each row As DataRow In dt_camt053_tx.Rows
        Dim amount As Decimal = Convert.ToDecimal(row("transaction_amount"))
        Dim proprietaryCode As String = GetSafeColumnValue(row, "rabo_detailed_transaction_type")
        If String.IsNullOrEmpty(proprietaryCode) Then
            proprietaryCode = GetSafeColumnValue(row, "proprietary_code")
        End If
        If String.IsNullOrEmpty(proprietaryCode) Then
            If amount >= 0 Then
                proprietaryCode = "100" ' Credit default
            Else
                proprietaryCode = "586" ' Debit default
            End If
        End If
        
        If Not txCodeGroups.ContainsKey(proprietaryCode) Then
            txCodeGroups(proprietaryCode) = New Tuple(Of Integer, Decimal, Decimal)(0, 0D, 0D)
        End If
        
        Dim group As Tuple(Of Integer, Decimal, Decimal) = txCodeGroups(proprietaryCode)
        txCodeGroups(proprietaryCode) = New Tuple(Of Integer, Decimal, Decimal)(
            group.Item1 + 1,
            group.Item2 + Math.Abs(amount),
            group.Item3 + amount
        )
    Next

    ' Output TtlNtriesPerBkTxCd for each group
    For Each kvp As KeyValuePair(Of String, Tuple(Of Integer, Decimal, Decimal)) In txCodeGroups
        Dim code As String = kvp.Key
        Dim group As Tuple(Of Integer, Decimal, Decimal) = kvp.Value
        xml.AppendLine("        <TtlNtriesPerBkTxCd>")
        xml.AppendLine(String.Format("          <NbOfNtries>{0}</NbOfNtries>", group.Item1))
        xml.AppendLine(String.Format("          <Sum>{0}</Sum>", group.Item2.ToString("F2")))
        xml.AppendLine(String.Format("          <TtlNetNtryAmt>{0}</TtlNetNtryAmt>", Math.Abs(group.Item3).ToString("F2")))
        xml.AppendLine(String.Format("          <CdtDbtInd>{0}</CdtDbtInd>", If(group.Item3 >= 0, "CRDT", "DBIT")))
        xml.AppendLine("          <BkTxCd>")
        xml.AppendLine("            <Domn>")
        xml.AppendLine("              <Cd>PMNT</Cd>")
        xml.AppendLine("              <Fmly>")
        
        ' Set family code based on proprietary code and amount direction
        Dim familyCode As String = "RCDT"
        Dim subFamilyCode As String = "ESCT"
        Select Case code
            Case "586"
                familyCode = "ICDT"
                subFamilyCode = "ESCT"
            Case "625"
                familyCode = "ICCN"
                subFamilyCode = "ICCT"
            Case Else
                familyCode = "RCDT"
                subFamilyCode = "ESCT"
        End Select
        
        xml.AppendLine(String.Format("                <Cd>{0}</Cd>", familyCode))
        xml.AppendLine(String.Format("                <SubFmlyCd>{0}</SubFmlyCd>", subFamilyCode))
        xml.AppendLine("              </Fmly>")
        xml.AppendLine("            </Domn>")
        xml.AppendLine("            <Prtry>")
        xml.AppendLine(String.Format("              <Cd>{0}</Cd>", code))
        xml.AppendLine("              <Issr>RABOBANK</Issr>")
        xml.AppendLine("            </Prtry>")
        xml.AppendLine("          </BkTxCd>")
        xml.AppendLine("        </TtlNtriesPerBkTxCd>")
    Next

    xml.AppendLine("      </TxsSummry>")
End If

' Transactions
Dim entrySeq As Integer = 1
For Each txRow As DataRow In dt_camt053_tx.Rows
    Dim txAmount As Decimal = Convert.ToDecimal(txRow("transaction_amount"))
    Dim txValueDate As DateTime = Convert.ToDateTime(txRow("value_date"))
    Dim txBookingDate As DateTime = Convert.ToDateTime(txRow("booking_date"))
    Dim entryRef As String = GetSafeColumnValue(txRow, "entry_reference")
    Dim endToEndId As String = If(String.IsNullOrEmpty(GetSafeColumnValue(txRow, "end_to_end_id")), "NOTPROVIDED", GetSafeColumnValue(txRow, "end_to_end_id"))
    Dim remittanceInfo As String = GetSafeColumnValue(txRow, "remittance_information_unstructured")
    
    xml.AppendLine("      <Ntry>")
    If Not String.IsNullOrEmpty(entryRef) Then
        ' Entry Reference - alleen in volledige versie
        If Not minimumRequiredFields Then
            xml.AppendLine(String.Format("        <NtryRef>{0}</NtryRef>", entryRef))
        End If
    End If
    xml.AppendLine(String.Format("        <Amt Ccy=""{0}"">{1}</Amt>", currency, Math.Abs(txAmount).ToString("F2")))
    xml.AppendLine(String.Format("        <CdtDbtInd>{0}</CdtDbtInd>", If(txAmount >= 0, "CRDT", "DBIT")))
    xml.AppendLine("        <Sts>BOOK</Sts>")
    xml.AppendLine("        <BookgDt>")
    xml.AppendLine(String.Format("          <Dt>{0}</Dt>", txBookingDate.ToString("yyyy-MM-dd")))
    xml.AppendLine("        </BookgDt>")
    xml.AppendLine("        <ValDt>")
    xml.AppendLine(String.Format("          <Dt>{0}</Dt>", txValueDate.ToString("yyyy-MM-dd")))
    xml.AppendLine("        </ValDt>")
    
    ' Try to get actual AcctSvcrRef from database first, then fallback to generated value
    Dim acctSvcrRef As String = GetSafeColumnValue(txRow, "acctsvcr_ref")
    If String.IsNullOrEmpty(acctSvcrRef) AndAlso Not String.IsNullOrEmpty(entryRef) Then
        ' Generate proper AcctSvcrRef format based on transaction type as fallback
        If txAmount >= 0 Then ' Credit transaction
            acctSvcrRef = String.Format("{0}:CI49CT", entryRef.PadLeft(11, "0"c))
        Else ' Debit transaction  
            acctSvcrRef = String.Format("{0}:CI23DI", entryRef.PadLeft(10, "0"c))
        End If
    End If
    
    If Not String.IsNullOrEmpty(acctSvcrRef) Then
        xml.AppendLine(String.Format("        <AcctSvcrRef>{0}</AcctSvcrRef>", acctSvcrRef))
    End If
    
    ' Get Rabobank detailed transaction type from BAI data
    Dim proprietaryCode As String = GetSafeColumnValue(txRow, "rabo_detailed_transaction_type")
    If String.IsNullOrEmpty(proprietaryCode) Then
        ' Fallback to old column name for backward compatibility
        proprietaryCode = GetSafeColumnValue(txRow, "proprietary_code")
    End If
    If String.IsNullOrEmpty(proprietaryCode) Then
        If txAmount >= 0 Then
            proprietaryCode = "100" ' Credit default
        Else
            proprietaryCode = "586" ' Debit default
        End If
    End If
    
    ' Bank Transaction Code
    xml.AppendLine("        <BkTxCd>")
    xml.AppendLine("          <Domn>")
    xml.AppendLine("            <Cd>PMNT</Cd>")
    xml.AppendLine("            <Fmly>")
    xml.AppendLine("              <Cd>RCDT</Cd>")
    xml.AppendLine("              <SubFmlyCd>ESCT</SubFmlyCd>")
    xml.AppendLine("            </Fmly>")
    xml.AppendLine("          </Domn>")
    xml.AppendLine("          <Prtry>")
    xml.AppendLine(String.Format("            <Cd>{0}</Cd>", proprietaryCode))
    xml.AppendLine("            <Issr>RABOBANK</Issr>")
    xml.AppendLine("          </Prtry>")
    xml.AppendLine("        </BkTxCd>")
    
    xml.AppendLine("        <NtryDtls>")
    xml.AppendLine("          <TxDtls>")
            xml.AppendLine("            <Refs>")
    
    ' Try to get actual AcctSvcrRef from database first, then fallback to generated value  
    Dim txAcctSvcrRef As String = GetSafeColumnValue(txRow, "acctsvcr_ref")
    If String.IsNullOrEmpty(txAcctSvcrRef) AndAlso Not String.IsNullOrEmpty(entryRef) Then
        ' Generate transaction AcctSvcrRef (different format) as fallback
        txAcctSvcrRef = String.Format("OO9T{0}", entryRef.PadLeft(12, "0"c))
    End If
    
    If Not String.IsNullOrEmpty(txAcctSvcrRef) Then
        xml.AppendLine(String.Format("              <AcctSvcrRef>{0}</AcctSvcrRef>", txAcctSvcrRef))
        
        ' PmtInfId - alleen in volledige versie
        If Not minimumRequiredFields Then
            Dim pmtInfId As String = GetSafeColumnValue(txRow, "payment_information_identification")
            If Not String.IsNullOrEmpty(pmtInfId) Then
                xml.AppendLine(String.Format("              <PmtInfId>{0}</PmtInfId>", pmtInfId))
            End If
        End If
        
        ' Try to get instruction_id from database, fallback to generated value
        Dim instructionId As String = GetSafeColumnValue(txRow, "instruction_id")
        If String.IsNullOrEmpty(instructionId) Then
            instructionId = txAcctSvcrRef
        End If
        xml.AppendLine(String.Format("              <InstrId>{0}</InstrId>", instructionId))
        
        ' TxId - alleen in volledige versie
        If Not minimumRequiredFields Then
            Dim batchRef As String = GetSafeColumnValue(txRow, "batch_entry_reference")
            If Not String.IsNullOrEmpty(batchRef) Then
                xml.AppendLine(String.Format("              <TxId>{0}</TxId>", batchRef))
            End If
        End If
    Else
        xml.AppendLine(String.Format("              <InstrId>{0}</InstrId>", endToEndId))
    End If
    
    ' Only include EndToEndId if explicitly available in database (following CAMT.053 specification)
    Dim dbEndToEndId As String = GetSafeColumnValue(txRow, "end_to_end_id")
    If Not String.IsNullOrEmpty(dbEndToEndId) AndAlso dbEndToEndId <> "NOTPROVIDED" Then
        xml.AppendLine(String.Format("              <EndToEndId>{0}</EndToEndId>", dbEndToEndId))
    End If
    xml.AppendLine("            </Refs>")
    
    ' Amount Details
    xml.AppendLine("            <AmtDtls>")
    xml.AppendLine("              <TxAmt>")
    xml.AppendLine(String.Format("                <Amt Ccy=""{0}"">{1}</Amt>", currency, Math.Abs(txAmount).ToString("F2")))
    xml.AppendLine("              </TxAmt>")
    xml.AppendLine("            </AmtDtls>")
    
    ' Bank Transaction Code (repeated at transaction level)
    xml.AppendLine("            <BkTxCd>")
    xml.AppendLine("              <Domn>")
    xml.AppendLine("                <Cd>PMNT</Cd>")
    xml.AppendLine("                <Fmly>")
    xml.AppendLine("                  <Cd>RCDT</Cd>")
    xml.AppendLine("                  <SubFmlyCd>ESCT</SubFmlyCd>")
    xml.AppendLine("                </Fmly>")
    xml.AppendLine("              </Domn>")
    xml.AppendLine("              <Prtry>")
    xml.AppendLine(String.Format("                <Cd>{0}</Cd>", proprietaryCode))
    xml.AppendLine("                <Issr>RABOBANK</Issr>")
    xml.AppendLine("              </Prtry>")
    xml.AppendLine("            </BkTxCd>")
    
    ' Related Parties with enhanced field mapping
    Dim creditorName As String = GetSafeColumnValue(txRow, "creditor_name")
    If String.IsNullOrEmpty(creditorName) Then creditorName = GetSafeColumnValue(txRow, "related_party_creditor_name")
    If String.IsNullOrEmpty(creditorName) Then creditorName = "Unknown Creditor"
    
    Dim creditorIban As String = GetSafeColumnValue(txRow, "creditor_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "related_party_creditor_account_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "creditor_account_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "cdtr_acct_iban")
    
    Dim creditorBIC As String = GetSafeColumnValue(txRow, "creditor_agent_bic")
    If String.IsNullOrEmpty(creditorBIC) Then creditorBIC = GetSafeColumnValue(txRow, "creditor_agent")
    
    Dim debtorName As String = GetSafeColumnValue(txRow, "debtor_name")
    If String.IsNullOrEmpty(debtorName) Then debtorName = GetSafeColumnValue(txRow, "related_party_debtor_name")
    If String.IsNullOrEmpty(debtorName) Then debtorName = "Unknown Debtor"
    
    Dim debtorIban As String = GetSafeColumnValue(txRow, "debtor_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "related_party_debtor_account_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "debtor_account_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "dbtr_acct_iban")
    
    Dim debtorBIC As String = GetSafeColumnValue(txRow, "debtor_agent_bic")
    If String.IsNullOrEmpty(debtorBIC) Then debtorBIC = GetSafeColumnValue(txRow, "debtor_agent")
    If String.IsNullOrEmpty(debtorBIC) Then debtorBIC = "RABONL2U" ' Default to Rabobank
    
    ' Related Parties - alleen in volledige versie
    If Not minimumRequiredFields Then
        xml.AppendLine("            <RltdPties>")
        If txAmount >= 0 Then ' Credit transaction - show debtor info
            xml.AppendLine("              <Dbtr>")
            xml.AppendLine(String.Format("                <Nm>{0}</Nm>", Security.SecurityElement.Escape(debtorName)))
            xml.AppendLine("              </Dbtr>")
            If Not String.IsNullOrEmpty(debtorIban) Then
                xml.AppendLine("              <DbtrAcct>")
                xml.AppendLine("                <Id>")
                xml.AppendLine(String.Format("                  <IBAN>{0}</IBAN>", debtorIban))
                xml.AppendLine("                </Id>")
                xml.AppendLine("              </DbtrAcct>")
            End If
        Else ' Debit transaction - show creditor info
            xml.AppendLine("              <Cdtr>")
            xml.AppendLine(String.Format("                <Nm>{0}</Nm>", Security.SecurityElement.Escape(creditorName)))
            xml.AppendLine("              </Cdtr>")
            If Not String.IsNullOrEmpty(creditorIban) Then
                xml.AppendLine("              <CdtrAcct>")
                xml.AppendLine("                <Id>")
                xml.AppendLine(String.Format("                  <IBAN>{0}</IBAN>", creditorIban))
                xml.AppendLine("                </Id>")
                xml.AppendLine("              </CdtrAcct>")
            End If
        End If
        xml.AppendLine("            </RltdPties>")
    End If
    
    ' Related Agents - use enhanced BIC mapping from database
    xml.AppendLine("            <RltdAgts>")
    If txAmount >= 0 AndAlso Not String.IsNullOrEmpty(debtorBIC) Then
        ' For credit transactions, show debtor agent BIC
        xml.AppendLine("              <DbtrAgt>")
        xml.AppendLine("                <FinInstnId>")
        xml.AppendLine(String.Format("                  <BIC>{0}</BIC>", debtorBIC))
        xml.AppendLine("                </FinInstnId>")
        xml.AppendLine("              </DbtrAgt>")
    ElseIf txAmount < 0 AndAlso Not String.IsNullOrEmpty(creditorBIC) Then
        ' For debit transactions, show creditor agent BIC
        xml.AppendLine("              <CdtrAgt>")
        xml.AppendLine("                <FinInstnId>")
        xml.AppendLine(String.Format("                  <BIC>{0}</BIC>", creditorBIC))
        xml.AppendLine("                </FinInstnId>")
        xml.AppendLine("              </CdtrAgt>")
    Else
        ' Default to Rabobank BIC
        xml.AppendLine("              <DbtrAgt>")
        xml.AppendLine("                <FinInstnId>")
        xml.AppendLine("                  <BIC>RABONL2U</BIC>")
        xml.AppendLine("                </FinInstnId>")
        xml.AppendLine("              </DbtrAgt>")
    End If
    xml.AppendLine("            </RltdAgts>")
    
    ' Purpose (if available)
    Dim purposeCode As String = GetSafeColumnValue(txRow, "purpose_code")
    If Not String.IsNullOrEmpty(purposeCode) Then
        xml.AppendLine("            <Purp>")
        xml.AppendLine(String.Format("              <Cd>{0}</Cd>", purposeCode))
        xml.AppendLine("            </Purp>")
    End If
    
    ' Remittance Information - alleen in volledige versie
    If Not minimumRequiredFields AndAlso Not String.IsNullOrEmpty(remittanceInfo) Then
        xml.AppendLine("            <RmtInf>")
        xml.AppendLine(String.Format("              <Ustrd>{0}</Ustrd>", Security.SecurityElement.Escape(remittanceInfo)))
        xml.AppendLine("            </RmtInf>")
    End If
    
    ' Related Dates - alleen in volledige versie
    If Not minimumRequiredFields Then
        xml.AppendLine("            <RltdDts>")
        
        ' Try to get interbank_settlement_date from database, fallback to booking_date
        Dim interbankSettlementDate As String = GetSafeColumnValue(txRow, "interbank_settlement_date")
        If Not String.IsNullOrEmpty(interbankSettlementDate) Then
            xml.AppendLine(String.Format("              <IntrBkSttlmDt>{0}</IntrBkSttlmDt>", Convert.ToDateTime(interbankSettlementDate).ToString("yyyy-MM-dd")))
        Else
            xml.AppendLine(String.Format("              <IntrBkSttlmDt>{0}</IntrBkSttlmDt>", txBookingDate.ToString("yyyy-MM-dd")))
        End If
        
        xml.AppendLine("            </RltdDts>")
    End If
    
    xml.AppendLine("          </TxDtls>")
    xml.AppendLine("        </NtryDtls>")
    xml.AppendLine("      </Ntry>")
    
    entrySeq += 1
Next

xml.AppendLine("    </Stmt>")
xml.AppendLine("  </BkToCstmrStmt>")
xml.AppendLine("</Document>")

' Schrijf naar bestand
File.WriteAllText(camt053FilePath, xml.ToString(), Encoding.UTF8)

Console.WriteLine("CAMT.053 file created: " + camt053FilePath)
Console.WriteLine(String.Format("Export Settings: Minimal Fields = {0}, Include CLAV = {1}", minimumRequiredFields, includeClavInExport))
Console.WriteLine(String.Format("Balance Summary: Opening = {0:F2}, Closing = {1:F2}, CLAV = {2:F2} {3}", openingBalance, closingBalance, clavBalance, currency))