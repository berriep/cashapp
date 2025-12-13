' MT940 Generation Parameters
' minimum_required_fields: Boolean parameter to control field inclusion
'   True = Include only mandatory fields per SWIFT MT940 specification
'   False = Include all available optional fields (current behavior)
' full_swift_format: Boolean parameter to control SWIFT message format
'   True = Generate full SWIFT message with {1:}, {2:}, {3:}, {4:}, {5:} blocks
'   False = Generate simple MT940 format without SWIFT blocks (current behavior)

' Parameter wordt doorgegeven via invoke code arguments
Dim minimumRequiredFields As Boolean = False ' Default to full fields
Dim fullSwiftFormat As Boolean = False ' Default to simple format

' Try to get parameters if passed (for UiPath or other callers)
Try
    If Not IsNothing(minimum_required_fields) AndAlso Not IsDBNull(minimum_required_fields) Then
        minimumRequiredFields = Convert.ToBoolean(minimum_required_fields)
    End If
Catch
    ' Parameter not available, use default
End Try

Try
    If Not IsNothing(full_swift_format) AndAlso Not IsDBNull(full_swift_format) Then
        fullSwiftFormat = Convert.ToBoolean(full_swift_format)
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

' Helper function to clean text for MT940 SWIFT compliance
Dim CleanForMT940 As Func(Of String, String) = Function(input)
    If String.IsNullOrEmpty(input) Then Return ""
    ' PRESERVE ORIGINAL CASE - Rabobank uses mixed case in names (e.g. "Center Parcs" not "CENTER PARCS")
    ' SWIFT MT940 standard: A-Z, a-z, 0-9, and limited special characters: / - ? : ( ) . , ' + { } SPACE
    ' Rabobank extension: Also allow _ (underscore) as seen in actual MT940 files
    Return Regex.Replace(input, "[^A-Za-z0-9 /\-\?:\(\)\.,'_\ \{\}]", "")
End Function

' Helper function to wrap long lines for SWIFT MT940 compliance (65 character limit)
Dim WrapMT940Line As Func(Of String, List(Of String)) = Function(input)
    Dim lines As New List(Of String)
    If String.IsNullOrEmpty(input) Then Return lines
    
    ' First line starts with :86:, continuation lines have no prefix
    Dim maxFirstLine As Integer = 65 - 4 ' Account for ":86:" prefix
    Dim maxContinuation As Integer = 65
    
    If input.Length <= maxFirstLine Then
        lines.Add(input)
        Return lines
    End If
    
    ' Split at word boundaries when possible, but respect 65-char limit
    Dim remaining As String = input
    Dim isFirstLine As Boolean = True
    
    While remaining.Length > 0
        Dim maxLength As Integer = If(isFirstLine, maxFirstLine, maxContinuation)
        
        If remaining.Length <= maxLength Then
            lines.Add(remaining)
            Exit While
        End If
        
        ' Find the best break point (prefer word boundaries)
        Dim breakPoint As Integer = maxLength
        For i As Integer = maxLength - 1 To Math.Max(maxLength - 20, 0) Step -1
            If remaining(i) = " "c OrElse remaining(i) = "/"c OrElse remaining(i) = "-"c Then
                breakPoint = i
                Exit For
            End If
        Next
        
        ' Extract the line and remove from remaining
        Dim line As String = remaining.Substring(0, breakPoint)
        lines.Add(line)
        ' Don't trim - preserve exact spacing for MT940 structured fields
        remaining = remaining.Substring(breakPoint)
        isFirstLine = False
    End While
    
    Return lines
End Function

' Helper function to format amount for MT940 (Rabobank spec: exactly 15 positions)
Dim FormatAmountMT940 As Func(Of Decimal, String) = Function(amount)
    ' Rabobank spec: exactly 15 positions INCLUDING comma and decimals
    ' Example: (EUR) 000000000032,00 = 15 total positions
    Dim formattedAmount As String = Math.Abs(amount).ToString("F2").Replace(".", ",")
    Dim parts() As String = formattedAmount.Split(","c)
    ' Calculate total positions: whole part + comma + decimal part = 15
    Dim decimalPart As String = parts(1)
    Dim wholePart As String = parts(0).PadLeft(15 - 1 - decimalPart.Length, "0"c)
    Return wholePart + "," + decimalPart
End Function

' Helper function to format amount for Rabobank balance fields (15 digits total)
Dim FormatBalanceAmountMT940 As Func(Of Decimal, String) = Function(amount)
    Return FormatAmountMT940(amount)
End Function

' Helper function to format amount for Rabobank transaction fields (15 digits total)
Dim FormatTransactionAmountMT940 As Func(Of Decimal, String) = Function(amount)
    Return FormatAmountMT940(amount)
End Function

' Helper function to get Credit/Debit indicator
Dim GetCdtDbtIndicator As Func(Of Decimal, String) = Function(amount)
    Return If(amount >= 0, "C", "D")
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

' Maak output directory
Dim outputDir As String = "C:\temp"
Directory.CreateDirectory(outputDir)

' Bestandsnaam met Rabobank SWIFT format (.swi extensie)
Dim fileType As String = If(minimumRequiredFields, "minimal", "full")
Dim swiftType As String = If(fullSwiftFormat, "swift", "simple")
Dim fileName As String = String.Format("MT940_R_{0}_{1}_{2}_{3}.swi", 
    iban, 
    currency,
    reportDate.ToString("yyyyMMdd"), 
    DateTime.Now.ToString("HHmmss"))
mt940FilePath = Path.Combine(outputDir, fileName)

' Bouw MT940 met StringBuilder
Dim mt940 As New StringBuilder()
Dim messageContent As New StringBuilder()

' Generate message reference number for SWIFT blocks
Dim msgRef As String = String.Format("{0:D8}", DateTime.Now.Ticks Mod 100000000)
Dim sessionNumber As String = "0000"
Dim sequenceNumber As String = "000000"

' Add SWIFT message type indicator for Rabobank format
messageContent.AppendLine(":940:")

' Add SWIFT blocks if full format requested
If fullSwiftFormat Then
    ' {1:} Basic Header Block
    mt940.AppendLine(String.Format("{{1:F01RABONL2UAXXX{0}}}", sessionNumber))
    
    ' {2:} Application Header Block
    ' Format: O = Outgoing, 940 = MT940, 0800 = priority, date, sender BIC, session, sequence, N = no delivery monitoring
    Dim appHeader As String = String.Format("O940{0}{1}RABONL2UAXXX{2}{3}N", 
        "0800", 
        reportDate.ToString("yyMMdd"), 
        sessionNumber, 
        sequenceNumber)
    mt940.AppendLine(String.Format("{{2:{0}}}", appHeader))
    
    ' {3:} User Header Block (optional)
    Dim transactionRef As String = String.Format("940S{0}", reportDate.ToString("yyMMdd"))
    mt940.AppendLine(String.Format("{{3:{{108:{0}}}}}", transactionRef))
    
    ' {4:} Text Block starts
    mt940.AppendLine("{{4:")
End If

' :20: Transaction Reference Number (MANDATORY) - Rabobank format
Dim transactionRef2 As String = String.Format("940S{0}", reportDate.ToString("yyMMdd"))
messageContent.AppendLine(String.Format(":20:{0}", transactionRef2))

' :25: Account Identification (MANDATORY) - Rabobank format with currency
messageContent.AppendLine(String.Format(":25:{0} {1}", iban.Replace(" ", ""), currency))

' :28C: Statement/Sequence Number (MANDATORY) - Rabobank format: YYNNN
' Generate sequence number based on day of year
Dim statementSequence As String = reportDate.ToString("yy") + reportDate.DayOfYear.ToString("D3")
messageContent.AppendLine(String.Format(":28C:{0}", statementSequence))

' :60F: Opening Balance (MANDATORY) - Rabobank format with 12-digit padding
' Format: [C/D]YYMMDD[currency][amount with leading zeros]
Dim openingCdtDbt As String = GetCdtDbtIndicator(openingBalance)
' Use previous day for opening balance date (standard Rabobank practice)
Dim openingDateMT940 As String = reportDate.AddDays(-1).ToString("yyMMdd")
Dim openingAmountMT940 As String = FormatBalanceAmountMT940(openingBalance)
messageContent.AppendLine(String.Format(":60F:{0}{1}{2}{3}", openingCdtDbt, openingDateMT940, currency, openingAmountMT940))

' Process transactions - :61: Statement Lines and :86: Information lines
For Each txRow As DataRow In dt_camt053_tx.Rows
    Dim txAmount As Decimal = Convert.ToDecimal(txRow("transaction_amount"))
    Dim txValueDate As DateTime = Convert.ToDateTime(txRow("value_date"))
    Dim txBookingDate As DateTime = Convert.ToDateTime(txRow("booking_date"))
    Dim entryRef As String = GetSafeColumnValue(txRow, "entry_reference")
    Dim acctSvcrRef As String = GetSafeColumnValue(txRow, "acctsvcr_ref")
    Dim remittanceInfo As String = GetSafeColumnValue(txRow, "remittance_information_unstructured")
    Dim endToEndId As String = GetSafeColumnValue(txRow, "end_to_end_id")
    
    ' Get counterparty information
    Dim debtorName As String = GetSafeColumnValue(txRow, "debtor_name")
    If String.IsNullOrEmpty(debtorName) Then debtorName = GetSafeColumnValue(txRow, "related_party_debtor_name")
    
    Dim creditorName As String = GetSafeColumnValue(txRow, "creditor_name")
    If String.IsNullOrEmpty(creditorName) Then creditorName = GetSafeColumnValue(txRow, "related_party_creditor_name")
    
    Dim debtorIban As String = GetSafeColumnValue(txRow, "debtor_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "related_party_debtor_account_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "debtor_account_iban")
    If String.IsNullOrEmpty(debtorIban) Then debtorIban = GetSafeColumnValue(txRow, "dbtr_acct_iban")
    
    Dim creditorIban As String = GetSafeColumnValue(txRow, "creditor_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "related_party_creditor_account_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "creditor_account_iban")
    If String.IsNullOrEmpty(creditorIban) Then creditorIban = GetSafeColumnValue(txRow, "cdtr_acct_iban")
    
    ' Get Rabobank transaction type
    Dim proprietaryCode As String = GetSafeColumnValue(txRow, "rabo_detailed_transaction_type")
    If String.IsNullOrEmpty(proprietaryCode) Then
        proprietaryCode = GetSafeColumnValue(txRow, "proprietary_code")
    End If
    If String.IsNullOrEmpty(proprietaryCode) Then
        If txAmount >= 0 Then
            proprietaryCode = "100" ' Credit default
        Else
            proprietaryCode = "586" ' Debit default
        End If
    End If
    
    ' Get reference data FIRST - we need this to determine :61: format
    Dim actualEref As String = GetSafeColumnValue(txRow, "end_to_end_id")
    If String.IsNullOrEmpty(actualEref) OrElse actualEref = "NOTPROVIDED" Then
        actualEref = GetSafeColumnValue(txRow, "payment_information_identification")
        If String.IsNullOrEmpty(actualEref) Then
            actualEref = GetSafeColumnValue(txRow, "instruction_id")
            If String.IsNullOrEmpty(actualEref) Then
                actualEref = entryRef
            End If
        End If
    End If
    
    Dim actualPref As String = GetSafeColumnValue(txRow, "payment_information_identification")
    If String.IsNullOrEmpty(actualPref) Then
        actualPref = GetSafeColumnValue(txRow, "batch_entry_reference")  
        If String.IsNullOrEmpty(actualPref) Then
            actualPref = GetSafeColumnValue(txRow, "instruction_id")
            If String.IsNullOrEmpty(actualPref) Then
                actualPref = actualEref
            End If
        End If
    End If
    
    ' Get batch_entry_reference for OM1T detection (used in type 2065/626)
    Dim batchRef As String = GetSafeColumnValue(txRow, "batch_entry_reference")
    
    ' :61: Statement Line (MANDATORY for each transaction)
    ' Format: YYMMDD[C/D][amount][N][transaction_type][reference] - Rabobank uses only value date
    Dim valueDateMT940 As String = txValueDate.ToString("yyMMdd")
    ' Rabobank does NOT use entry date in :61: field - always 6 digits only
    
    Dim cdtDbtIndicator As String = GetCdtDbtIndicator(txAmount)
    Dim amountMT940 As String = FormatAmountMT940(txAmount)
    
    ' Build transaction type code (Nxxx format) - Rabobank specific
    Dim transactionTypeCode As String = String.Format("N{0}", proprietaryCode)
    
    ' Reference field - prioritize batch_entry_reference for MT940 compatibility
    Dim batchEntryRef As String = GetSafeColumnValue(txRow, "batch_entry_reference")
    Dim instructionId As String = GetSafeColumnValue(txRow, "instruction_id")
    
    Dim referenceField As String = ""
    ' Priority: entry_reference > batch_entry_reference > instruction_id > end_to_end_id
    ' entry_reference is unique for Rabobank and always filled
    If Not String.IsNullOrEmpty(entryRef) Then
        referenceField = entryRef  ' Rabobank unique reference - always use this first
    ElseIf Not String.IsNullOrEmpty(batchEntryRef) Then
        referenceField = batchEntryRef  ' This contains OO9T... references
    ElseIf Not String.IsNullOrEmpty(instructionId) Then
        referenceField = instructionId
    ElseIf Not String.IsNullOrEmpty(endToEndId) AndAlso endToEndId <> "NOTPROVIDED" Then
        referenceField = endToEndId
    End If
    
    ' Handle specific Rabobank transaction patterns based on actual database patterns
    Dim raboRefSuffix As String = ""
    Select Case proprietaryCode
        Case "64"
            ' For 064 transactions, Rabobank uses MARF format (Mandate Reference)
            transactionTypeCode = "N064MARF"
        Case "593"
            ' Return/Reversal transactions - uses EREF format
            transactionTypeCode = "N593EREF"
        Case "113"
            ' Cash deposit transactions
            transactionTypeCode = "N113EREF"
        Case "193"
            ' Interest credit transactions
            transactionTypeCode = "N193NONREF"
        Case "93"
            ' Interest credit transactions (variant)
            transactionTypeCode = "N093NONREF"
        Case "2033"
            ' International payment (similar to 2065)
            If Not String.IsNullOrEmpty(batchRef) AndAlso (batchRef.StartsWith("OM1B") OrElse batchRef.StartsWith("OM1T")) Then
                Dim om1tRef As String = batchRef.Replace("OM1B", "OM1T")
                transactionTypeCode = String.Format("N033{0}", om1tRef.Substring(0, Math.Min(16, om1tRef.Length)))
            ElseIf Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("OO9T") Then
                transactionTypeCode = String.Format("N033{0}", referenceField)
            Else
                transactionTypeCode = "N033EREF"
            End If
        Case "540"
            ' Foreign currency transactions - special FT format
            transactionTypeCode = "N540FT"
        Case "544"
            ' For 544 transactions: sweeping like 626, uses OO9T reference if available
            If Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("OO9T") Then
                transactionTypeCode = String.Format("N544{0}", referenceField)
            Else
                transactionTypeCode = "N544EREF"
            End If
        Case "586"
            ' For 586 transactions, Rabobank uses PREF format with OM1B pattern
            transactionTypeCode = "N586PREF"
            ' OM1B000135###### pattern found in database
            raboRefSuffix = ""
        Case "085", "1085"
            ' For Smart Pay transactions, special format with reference but NO embedded date
            ' Date embedding causes :61: line format issues (extra digits)
            If Not String.IsNullOrEmpty(referenceField) Then
                transactionTypeCode = String.Format("N085{0}", referenceField.Substring(0, Math.Min(8, referenceField.Length)))
            Else
                transactionTypeCode = "N08500203158"
            End If
        Case "541"
            ' Credit transactions with multiple patterns: numeric, IBAN-like, complex bank refs
            transactionTypeCode = "N541EREF"
        Case "626"
            ' Internal transfers - updated for POYTCPE pattern found in database
            If Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("POYTCPE") Then
                ' POYTCPE2025000003### pattern for internal transfers
                transactionTypeCode = "N626NONREF"
            ElseIf Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("OO9T") Then
                ' Legacy OO9T pattern (not found in current database)
                transactionTypeCode = String.Format("N626{0}", referenceField)
            ElseIf Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("NP8A") Then
                transactionTypeCode = "N626NONREF"
            Else
                transactionTypeCode = "N626NONREF"
            End If
        Case "2065"
            ' International transactions with C-date pattern (NEW TYPE FOUND)
            If Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("C") Then
                ' C20251105-##########-############## pattern - truncate for :61: line
                ' Use only first 6 characters for transaction code to avoid line length issues
                transactionTypeCode = String.Format("N2065{0}", referenceField.Substring(0, Math.Min(6, referenceField.Length)))
            Else
                transactionTypeCode = "N2065"
            End If
        Case "065"
            ' Standard international transactions
            If Not String.IsNullOrEmpty(referenceField) Then
                transactionTypeCode = String.Format("N065{0}", referenceField)
            Else
                transactionTypeCode = "N065"
            End If
        Case "501"
            ' 501 transactions with EREF
            transactionTypeCode = "N501EREF"
        Case Else
            ' Default format - add EREF suffix for unknown transaction types
            transactionTypeCode = String.Format("N{0}EREF", proprietaryCode)
    End Select
    
    ' Format amount with Rabobank 15-position format
    Dim rabobankAmountMT940 As String = FormatTransactionAmountMT940(txAmount)
    
    ' Build transaction type code per spec (page 9-10)
    ' Sub 6: 1!a3!c format = N + 3-digit code (may be truncated for 4-digit codes)
    Dim correctTransactionTypeCode As String = ""
    
    Select Case proprietaryCode
        Case "64"
            correctTransactionTypeCode = "N064"
        Case "593"
            correctTransactionTypeCode = "N593"
        Case "113"
            correctTransactionTypeCode = "N113"
        Case "193"
            correctTransactionTypeCode = "N193"
        Case "93"
            correctTransactionTypeCode = "N093"
        Case "2033"
            correctTransactionTypeCode = "N033"
        Case "540"
            correctTransactionTypeCode = "N540"
        Case "544"
            correctTransactionTypeCode = "N544"
        Case "586"
            correctTransactionTypeCode = "N586"
        Case "2065"
            ' Per spec: 4-digit codes truncated in field 61, full code in field 86
            correctTransactionTypeCode = "N065"
        Case "1085"
            correctTransactionTypeCode = "N085"
        Case "541"
            correctTransactionTypeCode = "N541"
        Case "626"
            correctTransactionTypeCode = "N626"
        Case "501"
            correctTransactionTypeCode = "N501"
        Case "065"
            correctTransactionTypeCode = "N065"
        Case Else
            ' Default: N + first 3 digits
            If proprietaryCode.Length >= 3 Then
                correctTransactionTypeCode = "N" + proprietaryCode.Substring(0, 3)
            Else
                correctTransactionTypeCode = "N" + proprietaryCode.PadRight(3, "0"c)
            End If
    End Select
    
    ' Sub 7: Reference for account owner per spec (page 10)
    ' Use :86: field data to determine correct :61: reference format
    Dim referenceType As String = ""
    Dim use86RefIn61 As Boolean = False  ' Flag to use :86: reference in :61: field
    
    Select Case proprietaryCode
        Case "64"
            referenceType = "MARF"  ' Mandate reference for direct debit
        Case "593"
            referenceType = "EREF"  ' End-to-end reference for returns
        Case "113"
            referenceType = "EREF"  ' Cash deposit reference
        Case "193", "93"
            referenceType = "NONREF"  ' No reference for interest
        Case "2033"
            ' Check batch_entry_reference for OM1B/OM1T pattern
            If Not String.IsNullOrEmpty(batchRef) AndAlso (batchRef.StartsWith("OM1B") OrElse batchRef.StartsWith("OM1T")) Then
                Dim om1tRef As String = batchRef.Replace("OM1B", "OM1T")
                referenceType = om1tRef.Substring(0, Math.Min(16, om1tRef.Length))
                use86RefIn61 = True
            ElseIf Not String.IsNullOrEmpty(referenceField) AndAlso referenceField.StartsWith("OO9T") Then
                referenceType = referenceField.Substring(0, Math.Min(20, referenceField.Length))
                use86RefIn61 = True
            Else
                referenceType = "EREF"
            End If
        Case "540"
            referenceType = "FT"  ' Foreign transaction marker
        Case "544"
            referenceType = "EREF"  ' End-to-end reference for sweeping (but also has PREF in :86:)
        Case "586"
            referenceType = "PREF"  ' Payment reference - no institution ref in :61:
        Case "544", "626", "625"
            ' Type 544/626: OO9B/OO9T pattern in payment_information_identification (sweeping), or NONREF/EREF if POYTCPE/POYBCPE
            If Not String.IsNullOrEmpty(actualPref) AndAlso (actualPref.StartsWith("OO9B") OrElse actualPref.StartsWith("OO9T")) Then
                ' Sweeping transactions with OO9B/OO9T reference
                ' Convert OO9B to OO9T for :61: field (Rabobank standard)
                Dim oo9tRef As String = actualPref.Replace("OO9B", "OO9T")
                referenceType = oo9tRef.Substring(0, Math.Min(16, oo9tRef.Length))
                use86RefIn61 = True
            ElseIf Not String.IsNullOrEmpty(actualEref) AndAlso (actualEref.StartsWith("POYTCPE") OrElse actualEref.StartsWith("POYBCPE")) Then
                ' Internal transfers are NONREF
                referenceType = "NONREF"
            Else
                ' Default to NONREF for other type 626
                referenceType = "NONREF"
            End If
        Case "2065", "065"
            ' Check batch_entry_reference for OM1B/OM1T pattern (actual Rabobank field)
            If Not String.IsNullOrEmpty(batchRef) AndAlso (batchRef.StartsWith("OM1B") OrElse batchRef.StartsWith("OM1T")) Then
                ' International transaction with OM1B/OM1T reference from batch field
                ' Convert OM1B to OM1T for :61: field (Rabobank standard)
                Dim om1tRef As String = batchRef.Replace("OM1B", "OM1T")
                referenceType = om1tRef.Substring(0, Math.Min(16, om1tRef.Length))
                use86RefIn61 = True
            Else
                referenceType = "EREF"
            End If
        Case "501", "541", "1085"
            referenceType = "EREF"  ' End-to-end reference
        Case Else
            referenceType = "EREF"  ' Default to EREF for unknown types
    End Select
    
    ' Build :61: line per Rabobank specification - NO entry date
    ' For type 2065/065/626: Use batch_entry_reference if it contains OM1T/OO9T pattern
    Dim statementLine As String = ""
    
    If proprietaryCode = "1085" Then
        ' Type 1085: N08500203158 2025110//NP8A... (embedded reference + date fragment + NP8A ref)
        Dim smartPayRef As String = "00203158"  ' Fixed reference per Rabobank spec
        Dim dateFragment As String = txBookingDate.ToString("yyyyMMdd").Substring(0, 7) ' First 7 digits: 2025110
        
        statementLine = String.Format("{0}{1}{2}N085{3} {4}//{5}", 
            valueDateMT940, 
            cdtDbtIndicator, 
            rabobankAmountMT940,
            smartPayRef,
            dateFragment,
            referenceField)
    ElseIf ((proprietaryCode = "2065" OrElse proprietaryCode = "065") AndAlso use86RefIn61) OrElse _
           ((proprietaryCode = "626" OrElse proprietaryCode = "625" OrElse proprietaryCode = "544") AndAlso use86RefIn61) Then
        ' Type 2065/065 with OM1T pattern: N065OM1T003816126483//OM1T003816126483
        ' Type 626/544 with OO9T pattern: N626OO9T005185395573//OO9T005185395573
        Dim shortRef As String = referenceType  ' Already truncated to 16 chars
        Dim fullRef As String = If(Not String.IsNullOrEmpty(batchRef), batchRef, referenceField)
        
        statementLine = String.Format("{0}{1}{2}{3}{4}//{5}", 
            valueDateMT940, 
            cdtDbtIndicator, 
            rabobankAmountMT940, 
            correctTransactionTypeCode,
            shortRef,
            fullRef)
    ElseIf proprietaryCode = "501" Then
        ' Type 501: N501EREF//OO9T... (uses batch_entry_reference if available, else entry_reference)
        Dim ref501 As String = If(Not String.IsNullOrEmpty(batchRef), batchRef, referenceField)
        statementLine = String.Format("{0}{1}{2}{3}{4}//{5}", 
            valueDateMT940, 
            cdtDbtIndicator, 
            rabobankAmountMT940, 
            correctTransactionTypeCode,
            referenceType,
            ref501)
    ElseIf proprietaryCode = "541" Then
        ' Type 541: N541EREF//numeric_ref (uses end_to_end_id or special numeric reference)
        ' Original shows references like 43246532957 which aren't in our database export
        statementLine = String.Format("{0}{1}{2}{3}{4}//{5}", 
            valueDateMT940, 
            cdtDbtIndicator, 
            rabobankAmountMT940, 
            correctTransactionTypeCode,
            referenceType,
            referenceField)
    Else
        ' Standard format for all other types
        statementLine = String.Format("{0}{1}{2}{3}{4}", 
            valueDateMT940, 
            cdtDbtIndicator, 
            rabobankAmountMT940, 
            correctTransactionTypeCode,
            referenceType)
            
        ' Sub 8: Institution reference - use entry_reference (not original bank ref)
        If proprietaryCode <> "586" AndAlso Not use86RefIn61 Then
            If Not String.IsNullOrEmpty(referenceField) Then
                statementLine += String.Format("//{0}", referenceField)
            End If
        End If
    End If
    
    messageContent.AppendLine(String.Format(":61:{0}", statementLine))
    
    ' Sub 9: Additional information (counterparty IBAN) on new line per SWIFT spec
    Dim counterpartyIban As String = ""
    If txAmount >= 0 AndAlso Not String.IsNullOrEmpty(debtorIban) Then
        counterpartyIban = debtorIban
    ElseIf txAmount < 0 AndAlso Not String.IsNullOrEmpty(creditorIban) Then
        counterpartyIban = creditorIban
    End If
    
    If Not String.IsNullOrEmpty(counterpartyIban) Then
        messageContent.AppendLine(counterpartyIban)
    Else
        ' Per spec: If no counterparty account, fill with 10 zeros
        messageContent.AppendLine("0000000000")
    End If
    
    ' Skip old case-based processing
    If False Then
        Select Case proprietaryCode
            ' Placeholder - old case processing skipped
        End Select
    End If
    
    ' :86: Information to Account Owner per specification (page 17-20)
    If Not minimumRequiredFields Then
        Dim structuredInfo As New StringBuilder()
        
        ' actualEref and actualPref are already declared earlier at line 330
        ' No need to redeclare them here - just use the existing variables
        
        ' Build structured info per Rabobank code word order (page 19-20):
        ' CORRECT ORDER: /EREF/ or /PREF/ → /PREF/ (for 501) → /TRCD/ → /RTRN/ → /BENM//NAME/ or /ORDP//NAME/ → /REMI/ → /OCMT/ → /EXCH/ → /CHGS/ → /INIT//NAME/ → /ISDT/
        
        ' 1. Reference field - depends on transaction type per Rabobank specification
        If proprietaryCode = "64" Then
            ' Transaction type 064 uses /MARF/ (Mandate Reference) AND /EREF/
            ' Get mandate reference from appropriate field
            Dim mandateRef As String = GetSafeColumnValue(txRow, "mandate_id")
            If String.IsNullOrEmpty(mandateRef) Then
                mandateRef = GetSafeColumnValue(txRow, "mandate_reference")
            End If
            If Not String.IsNullOrEmpty(mandateRef) Then
                structuredInfo.Append(String.Format("/MARF/{0}", mandateRef))
            End If
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
        ElseIf proprietaryCode = "544" Then
            ' Transaction type 544 uses BOTH /EREF/ and /PREF/ (sweeping transactions like 626)
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
            If Not String.IsNullOrEmpty(actualPref) Then
                structuredInfo.Append(String.Format("/PREF/{0}", actualPref))
            End If
        ElseIf proprietaryCode = "541" Then
            ' Transaction type 541 can have BOTH /EREF/ and /PREF/ for OO9B references
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
            ' Add PREF if it's an OO9B reference (same as EREF for these transactions)
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref.StartsWith("OO9B") Then
                structuredInfo.Append(String.Format("/PREF/{0}", actualEref))
            End If
        ElseIf proprietaryCode = "586" Then
            ' Transaction type 586 uses /PREF/ instead of /EREF/ per Rabobank practice
            If Not String.IsNullOrEmpty(actualPref) Then
                structuredInfo.Append(String.Format("/PREF/{0}", actualPref))
            End If
        ElseIf proprietaryCode = "501" Then
            ' Transaction type 501 uses BOTH /EREF/ and /PREF/ BEFORE /TRCD/
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
            If Not String.IsNullOrEmpty(actualPref) Then
                structuredInfo.Append(String.Format("/PREF/{0}", actualPref))
            End If
        ElseIf proprietaryCode = "626" Then
            ' Transaction type 626 (internal transfers) use /EREF/ but are NONREF in :61: field
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
        Else
            ' All other transaction types use /EREF/
            If Not String.IsNullOrEmpty(actualEref) AndAlso actualEref <> "NOTPROVIDED" Then
                structuredInfo.Append(String.Format("/EREF/{0}", actualEref))
            End If
        End If
        
        ' 2. Transaction Type Code (/TRCD/) - Always include full code here with 3-digit padding
        Dim trcdCode As String = proprietaryCode.PadLeft(3, "0"c)
        structuredInfo.Append(String.Format("/TRCD/{0}", trcdCode))
        
        ' 4. Return Code (/RTRN/) - Only if applicable (especially for type 593)
        Dim returnCode As String = GetSafeColumnValue(txRow, "return_reason_code")
        If String.IsNullOrEmpty(returnCode) Then returnCode = GetSafeColumnValue(txRow, "reason_code")
        If Not String.IsNullOrEmpty(returnCode) Then
            structuredInfo.Append(String.Format("/RTRN/{0}", returnCode))
        End If
        
        ' 5. Counterparty Information (/BENM//NAME/ or /ORDP//NAME/)
        ' Skip counterparty for interest transactions (193, 93) - bank internal
        If proprietaryCode <> "193" AndAlso proprietaryCode <> "93" Then
            If proprietaryCode = "64" Then
                ' Type 064 (direct debit) uses ORDP even though it's a debit transaction
                If Not String.IsNullOrEmpty(creditorName) Then
                    structuredInfo.Append("/ORDP//NAME/" & CleanForMT940(creditorName))
                End If
            ElseIf txAmount >= 0 AndAlso Not String.IsNullOrEmpty(debtorName) Then
                ' Credit transaction - ordering party (debtor)
                structuredInfo.Append("/ORDP//NAME/" & CleanForMT940(debtorName))
            ElseIf txAmount < 0 AndAlso Not String.IsNullOrEmpty(creditorName) Then
                ' Debit transaction - beneficiary (creditor)  
                structuredInfo.Append("/BENM//NAME/" & CleanForMT940(creditorName))
            End If
        End If
        
        ' 6. Remittance Information (/REMI/)
        If Not String.IsNullOrEmpty(remittanceInfo) Then
            Dim cleanedRemi As String = CleanForMT940(remittanceInfo)
            
            ' Check for Booking.com pattern: /ID.354803/ at END of REMI (KEEP THE DOT)
            Dim idDotMatch As Match = Regex.Match(cleanedRemi, "/ID\.(\d+)/?$")
            If idDotMatch.Success Then
                ' Booking.com pattern: /REMI/NO.CJyuRWZLp7o6UMGt/ID.354803/
                cleanedRemi = Regex.Replace(cleanedRemi, "/ID\.\d+/?$", "").TrimEnd("/"c)
                structuredInfo.Append(String.Format("/REMI/{0}/ID.{1}", cleanedRemi, idDotMatch.Groups(1).Value))
            Else
                structuredInfo.Append(String.Format("/REMI/{0}", cleanedRemi))
            End If
        End If
        
        ' 7. Original Currency/Amount (/OCMT/) - For international transactions
        Dim originalCurrency As String = GetSafeColumnValue(txRow, "original_currency")
        If String.IsNullOrEmpty(originalCurrency) Then originalCurrency = GetSafeColumnValue(txRow, "instructed_amount_currency")
        If String.IsNullOrEmpty(originalCurrency) Then originalCurrency = GetSafeColumnValue(txRow, "currency_exchange_source_currency")
        
        Dim originalAmount As String = GetSafeColumnValue(txRow, "original_amount")
        If String.IsNullOrEmpty(originalAmount) Then originalAmount = GetSafeColumnValue(txRow, "instructed_amount")
        
        ' For international types (2033, 2065, 540), ALWAYS include OCMT even if same currency
        ' For other types, only include if currency differs
        Dim forceOcmt As Boolean = (proprietaryCode = "2033" OrElse proprietaryCode = "2065" OrElse proprietaryCode = "540")
        
        If Not String.IsNullOrEmpty(originalCurrency) AndAlso Not String.IsNullOrEmpty(originalAmount) AndAlso (forceOcmt OrElse originalCurrency <> currency) Then
            structuredInfo.Append(String.Format("/OCMT/{0}{1}", originalCurrency, originalAmount.Replace(".", ",")))
        End If
        
        ' 8. Exchange Rate (/EXCH/)
        Dim exchangeRate As String = GetSafeColumnValue(txRow, "exchange_rate")
        If String.IsNullOrEmpty(exchangeRate) Then exchangeRate = GetSafeColumnValue(txRow, "currency_exchange_rate")
        If Not String.IsNullOrEmpty(exchangeRate) AndAlso Not String.IsNullOrEmpty(originalCurrency) Then
            ' Format to max 5 decimals to match Rabobank MT940 format
            Dim rateDecimal As Decimal
            If Decimal.TryParse(exchangeRate, rateDecimal) Then
                ' Round to 5 decimals for Rabobank format
                exchangeRate = Math.Round(rateDecimal, 5).ToString("0.#####")
            End If
            structuredInfo.Append(String.Format("/EXCH/{0}", exchangeRate.Replace(".", ",")))
        End If
        
        ' 9. Charges (/CHGS1/, /CHGS2/, etc.) - Include all available charges
        Dim chargeFields() As String = {"charge_1", "charge_2", "charge_3", "charge_4", "charge_5", 
                                        "charges_eur", "charges_foreign", "transaction_charges"}
        Dim chargeIndex As Integer = 1
        For Each chargeFieldName As String In chargeFields
            Dim chargeValue As String = GetSafeColumnValue(txRow, chargeFieldName)
            If Not String.IsNullOrEmpty(chargeValue) Then
                structuredInfo.Append(String.Format("/CHGS{0}/{1}", chargeIndex, chargeValue))
                chargeIndex += 1
                If chargeIndex > 5 Then Exit For
            End If
        Next
        
        ' 10. Initiating Party (/INIT//NAME/) - Only for specific transaction types
        If proprietaryCode = "586" OrElse proprietaryCode = "2065" OrElse proprietaryCode = "065" OrElse proprietaryCode = "625" Then
            Dim initiatingParty As String = GetSafeColumnValue(txRow, "initiating_party_name")
            If Not String.IsNullOrEmpty(initiatingParty) Then
                structuredInfo.Append(String.Format("/INIT//NAME/{0}", CleanForMT940(initiatingParty)))
            End If
        End If
        
        ' 10a. Purpose Code (/PURP/) - Optional field before ISDT
        Dim purposeCode As String = GetSafeColumnValue(txRow, "purpose_code")
        If Not String.IsNullOrEmpty(purposeCode) Then
            structuredInfo.Append(String.Format("/PURP//CD/{0}", purposeCode))
        End If
        
        ' 11. Interbank Settlement Date (/ISDT/) - Per Rabobank specification
        If proprietaryCode = "586" Then
            ' Type 586: Always include date for all PREF formats (per actual Rabobank files)
            ' Both numeric PREF (1010854019) and C-format PREF include booking date
            structuredInfo.Append(String.Format("/ISDT/{0}", txBookingDate.ToString("yyyy-MM-dd")))
        ElseIf proprietaryCode = "626" OrElse proprietaryCode = "625" OrElse proprietaryCode = "1085" OrElse proprietaryCode = "64" OrElse proprietaryCode = "193" OrElse proprietaryCode = "93" OrElse proprietaryCode = "2033" OrElse proprietaryCode = "2065" OrElse proprietaryCode = "540" Then
            ' These types do NOT include /ISDT/ per Rabobank specification
            ' Type 64 (direct debit) uses /CSID/ instead at the end
            ' Types 193/93 (interest) are bank internal and don't include ISDT
            ' Types 2033/2065/540 (international/forex) use /OCMT/ instead
            ' Do nothing - no /ISDT/ field
        Else
            ' All other types include /ISDT/ with date
            ' This includes: 593 (returns), 113 (cash), etc.
            structuredInfo.Append(String.Format("/ISDT/{0}", txBookingDate.ToString("yyyy-MM-dd")))
        End If
        
        ' 12. Creditor Scheme Identification (/CSID/) - Only for type 064 (direct debit)
        If proprietaryCode = "64" Then
            Dim creditorSchemeId As String = GetSafeColumnValue(txRow, "creditor_id")
            If String.IsNullOrEmpty(creditorSchemeId) Then
                creditorSchemeId = GetSafeColumnValue(txRow, "creditor_identifier")
            End If
            If Not String.IsNullOrEmpty(creditorSchemeId) Then
                structuredInfo.Append(String.Format("/CSID/{0}", creditorSchemeId))
            End If
        End If
        
        ' Output with SWIFT MT940 line wrapping per spec (6 lines × 65 chars)
        Dim structuredText As String = structuredInfo.ToString()
        If structuredText.Length > 0 Then
            Dim wrappedLines As List(Of String) = WrapMT940Line(structuredText)
            For i As Integer = 0 To Math.Min(wrappedLines.Count - 1, 5) ' Maximum 6 lines per spec
                If i = 0 Then
                    ' First line with :86: prefix
                    messageContent.AppendLine(String.Format(":86:{0}", wrappedLines(i)))
                Else
                    ' Continuation lines without prefix
                    ' Per spec page 17: Replace ':' or '–' at start of continuation lines with space
                    Dim line As String = wrappedLines(i)
                    If line.StartsWith(":") OrElse line.StartsWith("–") Then
                        line = " " + line.Substring(1)
                    End If
                    messageContent.AppendLine(line)
                End If
            Next
        End If
    End If
Next

' :62F: Closing Balance (MANDATORY) - Rabobank format with 12-digit padding
' Format: [C/D]YYMMDD[currency][amount with leading zeros]
Dim closingCdtDbt As String = GetCdtDbtIndicator(closingBalance)
Dim closingDateMT940 As String = reportDate.ToString("yyMMdd")
Dim closingAmountMT940 As String = FormatBalanceAmountMT940(closingBalance)
messageContent.AppendLine(String.Format(":62F:{0}{1}{2}{3}", closingCdtDbt, closingDateMT940, currency, closingAmountMT940))

' Optional closing available balance - alleen in volledige versie (Rabobank format)
If Not minimumRequiredFields Then
    ' :64: Closing Available Balance (OPTIONAL)
    messageContent.AppendLine(String.Format(":64:{0}{1}{2}{3}", closingCdtDbt, closingDateMT940, currency, closingAmountMT940))
    
    ' :65: Forward Available Balance (OPTIONAL) - for next 4 days (Rabobank style)
    For i As Integer = 1 To 4
        Dim forwardDate As String = reportDate.AddDays(i).ToString("yyMMdd")
        messageContent.AppendLine(String.Format(":65:{0}{1}{2}{3}", closingCdtDbt, forwardDate, currency, closingAmountMT940))
    Next
End If

' End of message indicator - Rabobank does NOT use the trailing dash
' messageContent.AppendLine("-")

' Combine message content with SWIFT blocks if needed
If fullSwiftFormat Then
    ' Add the message content to {4:} block
    mt940.Append(messageContent.ToString())
    
    ' Close {4:} Text Block
    mt940.AppendLine("}")
    
    ' {5:} Trailer Block with MAC/CHK
    ' Generate simple checksum for demonstration (in production, use proper MAC)
    Dim checksum As String = Math.Abs(messageContent.ToString().GetHashCode()).ToString("X8")
    mt940.AppendLine(String.Format("{{5:{{CHK:{0}}}}}", checksum))
Else
    ' Simple format - just append the message content
    mt940.Append(messageContent.ToString())
End If

' Schrijf naar bestand met UTF-8 encoding (compatible with SWIFT MT940)
File.WriteAllText(mt940FilePath, mt940.ToString(), System.Text.Encoding.UTF8)

Console.WriteLine("MT940 file created: " + mt940FilePath)

' Validation: Check balance equation
Dim calculatedClosing As Decimal = openingBalance
For Each txRow As DataRow In dt_camt053_tx.Rows
    calculatedClosing += Convert.ToDecimal(txRow("transaction_amount"))
Next

If Math.Abs(calculatedClosing - closingBalance) > 0.01 Then
    Console.WriteLine("WARNING: Balance mismatch detected!")
    Console.WriteLine(String.Format("Opening: {0}, Calculated: {1}, Closing: {2}", 
        openingBalance.ToString("F2"), 
        calculatedClosing.ToString("F2"), 
        closingBalance.ToString("F2")))
Else
    Console.WriteLine("Balance validation: PASSED")
End If

' Output statistics
Console.WriteLine(String.Format("Processed {0} transactions for account {1}", dt_camt053_tx.Rows.Count, iban))
Console.WriteLine(String.Format("Period: {0} | Opening: {1} {2} | Closing: {3} {4}", 
    reportDate.ToString("yyyy-MM-dd"),
    If(openingBalance >= 0, "C", "D"), 
    FormatAmountMT940(openingBalance),
    If(closingBalance >= 0, "C", "D"), 
    FormatAmountMT940(closingBalance)))