' MT940 Multi-Account Generation Script
' Based on Rabobank MT940S Structured specification v1.5.3
' Generates MT940 files for multiple accounts in one run
' 
' Parameters:
' - minimum_required_fields: Boolean - Include only mandatory fields (default: False)
' - full_swift_format: Boolean - Generate full SWIFT blocks (default: False)
' - output_directory: String - Custom output directory (default: "C:\temp")
' - combined_output: Boolean - Generate one combined file with all accounts (default: False)

' Parameter handling
Dim minimumRequiredFields As Boolean = False ' Default to full fields
Dim fullSwiftFormat As Boolean = False ' Default to simple format
Dim customOutputDir As String = "" ' Default to C:\temp
Dim combinedOutput As Boolean = False ' Default to separate files per account

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

Try
    If Not IsNothing(output_directory) AndAlso Not IsDBNull(output_directory) AndAlso Not String.IsNullOrEmpty(output_directory.ToString()) Then
        customOutputDir = output_directory.ToString()
    End If
Catch
    ' Parameter not available, use default
End Try

Try
    If Not IsNothing(combined_output) AndAlso Not IsDBNull(combined_output) Then
        combinedOutput = Convert.ToBoolean(combined_output)
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

' Helper function to wrap long lines for SWIFT MT940 compliance (69/65 character limit per Rabobank)
Dim WrapMT940Line As Func(Of String, List(Of String)) = Function(input)
    Dim lines As New List(Of String)
    If String.IsNullOrEmpty(input) Then Return lines
    
    ' First line starts with :86:, continuation lines have no prefix
    Dim maxFirstLine As Integer = 69 - 4 ' Account for ":86:" prefix = 65 chars content
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
        
        ' Break exactly at character limit (Rabobank style - no word boundary preference)
        Dim breakPoint As Integer = maxLength
        
        ' Extract the line and remove from remaining
        Dim line As String = remaining.Substring(0, breakPoint)
        lines.Add(line)
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

' Validate input data
If dt_camt053_data Is Nothing OrElse dt_camt053_data.Rows.Count = 0 Then
    Throw New Exception("dt_camt053_data is empty - no account data provided")
End If

If dt_camt053_tx Is Nothing OrElse dt_camt053_tx.Rows.Count = 0 Then
    Throw New Exception("dt_camt053_tx is empty - no transaction data provided")
End If

' Setup output directory
Dim outputDir As String = If(String.IsNullOrEmpty(customOutputDir), "C:\temp", customOutputDir)
Directory.CreateDirectory(outputDir)

' Initialize collections for tracking
Dim generatedFiles As New List(Of String)
Dim processingStats As New List(Of String)
Dim accountsWithErrors As New List(Of String)
Dim combinedMT940Content As New StringBuilder() ' For combined output mode

' Add :940: header for combined file (only once at the beginning)
If combinedOutput Then
    combinedMT940Content.AppendLine(":940:")
End If

Console.WriteLine("=== MT940 MULTI-ACCOUNT GENERATION STARTED ===")
Console.WriteLine($"Processing {dt_camt053_data.Rows.Count} accounts...")
Console.WriteLine($"Output directory: {outputDir}")
Console.WriteLine($"Mode: {If(minimumRequiredFields, "Minimal fields", "Full fields")}")
Console.WriteLine($"Format: {If(fullSwiftFormat, "Full SWIFT blocks", "Simple format")}")
Console.WriteLine($"Output: {If(combinedOutput, "Combined file (all accounts)", "Separate files per account")}")
Console.WriteLine("")

' MULTI-ACCOUNT PROCESSING: Loop through each account
For Each balanceRow As DataRow In dt_camt053_data.Rows
    Try
        ' Extract account-specific data
        Dim iban As String = balanceRow("iban").ToString()
        Dim ownerName As String = balanceRow("owner_name").ToString()
        Dim reportDate As DateTime = Convert.ToDateTime(balanceRow("day"))
        Dim currency As String = balanceRow("currency").ToString()
        Dim openingBalance As Decimal = Convert.ToDecimal(balanceRow("opening_balance"))
        Dim closingBalance As Decimal = Convert.ToDecimal(balanceRow("closing_balance"))
        Dim transactionCount As Integer = Convert.ToInt32(balanceRow("transaction_count"))
        
        Console.WriteLine($"Processing account: {iban} ({ownerName}) - {currency}")
        
        ' Filter transactions for this specific account
        Dim accountTransactions As DataRow() = dt_camt053_tx.Select($"iban = '{iban}'")
        
        If accountTransactions.Length = 0 Then
            Console.WriteLine($"  WARNING: No transactions found for account {iban}, creating MT940 with balances only...")
        End If
        
        ' Generate unique filename with timestamp to avoid conflicts
        Dim currentDateTime As String = DateTime.Now.ToString("yyyyMMdd")
        Dim timestamp As String = DateTime.Now.ToString("HHmmss")
        Dim fileName As String = String.Format("MT940_R_{0}_{1}_{2}_{3}.swi", 
            iban, 
            reportDate.ToString("yyyyMMdd"),
            currentDateTime,
            timestamp)
        Dim currentMt940FilePath As String = Path.Combine(outputDir, fileName)
        
        ' Build MT940 content for this account
        Dim mt940 As New StringBuilder()
        Dim messageContent As New StringBuilder()
        
        ' Generate SWIFT block parameters
        Dim msgRef As String = String.Format("{0:D8}", DateTime.Now.Ticks Mod 100000000)
        Dim sessionNumber As String = "0000"
        Dim sequenceNumber As String = String.Format("{0:D6}", Array.IndexOf(dt_camt053_data.Rows.Cast(Of DataRow).ToArray(), balanceRow))
        
        ' SWIFT MT940 Header - only add :940: for separate files, not for combined output
        If Not combinedOutput Then
            messageContent.AppendLine(":940:")
        End If
        
        ' Add SWIFT blocks if full format requested
        If fullSwiftFormat Then
            ' {1:} Basic Header Block
            mt940.AppendLine(String.Format("{{1:F01RABONL2UAXXX{0}}}", sessionNumber))
            
            ' {2:} Application Header Block
            Dim appHeader As String = String.Format("O940{0}{1}RABONL2UAXXX{2}{3}N", 
                "0800", 
                reportDate.ToString("yyMMdd"), 
                sessionNumber, 
                sequenceNumber)
            mt940.AppendLine(String.Format("{{2:{0}}}", appHeader))
            
            ' {3:} User Header Block
            Dim transactionRef As String = String.Format("940S{0}", reportDate.ToString("yyMMdd"))
            mt940.AppendLine(String.Format("{{3:{{108:{0}}}}}", transactionRef))
            
            ' {4:} Text Block starts
            mt940.AppendLine("{{4:")
        End If
        
        ' :20: Transaction Reference Number (MANDATORY)
        Dim transactionRef2 As String = String.Format("940S{0}", reportDate.ToString("yyMMdd"))
        messageContent.AppendLine(String.Format(":20:{0}", transactionRef2))
        
        ' :25: Account Identification (MANDATORY)
        messageContent.AppendLine(String.Format(":25:{0} {1}", iban.Replace(" ", ""), currency))
        
        ' :28C: Statement/Sequence Number (MANDATORY)
        Dim statementSequence As String = reportDate.ToString("yy") + reportDate.DayOfYear.ToString("D3")
        messageContent.AppendLine(String.Format(":28C:{0}", statementSequence))
        
        ' :60F: Opening Balance (MANDATORY)
        Dim openingCdtDbt As String = GetCdtDbtIndicator(openingBalance)
        Dim openingDateMT940 As String = reportDate.AddDays(-1).ToString("yyMMdd")
        Dim openingAmountMT940 As String = FormatBalanceAmountMT940(openingBalance)
        messageContent.AppendLine(String.Format(":60F:{0}{1}{2}{3}", openingCdtDbt, openingDateMT940, currency, openingAmountMT940))
        
        ' Process transactions for this account
        Dim txProcessed As Integer = 0
        For Each txRow As DataRow In accountTransactions
            Try
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
                
                ' :61: Statement Line formatting - Rabobank uses only value date
                Dim valueDateMT940 As String = txValueDate.ToString("yyMMdd")
                ' Rabobank does NOT use entry date in :61: field - always 6 digits only
                
                Dim cdtDbtIndicator As String = GetCdtDbtIndicator(txAmount)
                Dim amountMT940 As String = FormatAmountMT940(txAmount)
                
                ' Reference field priority
                Dim batchEntryRef As String = GetSafeColumnValue(txRow, "batch_entry_reference")
                Dim instructionId As String = GetSafeColumnValue(txRow, "instruction_id")
                
                Dim referenceField As String = ""
                ' Priority varies by transaction type - for 065/2065 use batch_entry_reference (OM1T pattern) first
                If proprietaryCode = "065" OrElse proprietaryCode = "2065" Then
                    ' For international transactions, prioritize batch_entry_reference (OM1T pattern)
                    If Not String.IsNullOrEmpty(batchEntryRef) Then
                        referenceField = batchEntryRef  ' OM1T### pattern for international transactions
                    ElseIf Not String.IsNullOrEmpty(entryRef) Then
                        referenceField = entryRef
                    ElseIf Not String.IsNullOrEmpty(instructionId) Then
                        referenceField = instructionId
                    ElseIf Not String.IsNullOrEmpty(endToEndId) AndAlso endToEndId <> "NOTPROVIDED" Then
                        referenceField = endToEndId
                    End If
                Else
                    ' For other transaction types, use standard priority: entry_reference first
                    If Not String.IsNullOrEmpty(entryRef) Then
                        referenceField = entryRef  ' Rabobank unique reference - usually use this first
                    ElseIf Not String.IsNullOrEmpty(batchEntryRef) Then
                        referenceField = batchEntryRef  
                    ElseIf Not String.IsNullOrEmpty(instructionId) Then
                        referenceField = instructionId
                    ElseIf Not String.IsNullOrEmpty(endToEndId) AndAlso endToEndId <> "NOTPROVIDED" Then
                        referenceField = endToEndId
                    End If
                End If
                
                ' Build transaction type code per Rabobank specification
                Dim correctTransactionTypeCode As String = ""
                
                Select Case proprietaryCode
                    Case "64"
                        correctTransactionTypeCode = "N064"
                    Case "100"
                        ' Standard credit - most common type (81.93% of all transactions)
                        correctTransactionTypeCode = "N100"
                    Case "572"
                        ' Transaction type 572 (0.06% of transactions)
                        correctTransactionTypeCode = "N572"
                    Case "593"
                        correctTransactionTypeCode = "N593"
                    Case "625"
                        ' Transaction type 625 - ICCN-ICCT (Instant Credit)
                        correctTransactionTypeCode = "N625"
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
                
                ' Reference type per specification
                Dim referenceType As String = ""
                
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
                        If Not String.IsNullOrEmpty(batchEntryRef) AndAlso (batchEntryRef.StartsWith("OM1B") OrElse batchEntryRef.StartsWith("OM1T")) Then
                            Dim om1tRef As String = batchEntryRef.Replace("OM1B", "OM1T")
                            referenceType = om1tRef.Substring(0, Math.Min(16, om1tRef.Length))
                        Else
                            referenceType = "EREF"
                        End If
                    Case "540"
                        referenceType = "FT"  ' Foreign transaction marker
                    Case "544"
                        referenceType = "EREF"  ' End-to-end reference for sweeping
                    Case "586"
                        referenceType = "PREF"  ' Payment reference
                    Case "544", "626", "625"
                        ' Type 544/626: OO9B/OO9T pattern for sweeping, or NONREF for internal
                        Dim actualPref As String = GetSafeColumnValue(txRow, "payment_information_identification")
                        If String.IsNullOrEmpty(actualPref) Then
                            actualPref = GetSafeColumnValue(txRow, "batch_entry_reference")  
                        End If
                        Dim actualEref As String = GetSafeColumnValue(txRow, "end_to_end_id")
                        
                        If Not String.IsNullOrEmpty(actualPref) AndAlso (actualPref.StartsWith("OO9B") OrElse actualPref.StartsWith("OO9T")) Then
                            Dim oo9tRef As String = actualPref.Replace("OO9B", "OO9T")
                            referenceType = oo9tRef.Substring(0, Math.Min(16, oo9tRef.Length))
                        ElseIf Not String.IsNullOrEmpty(actualEref) AndAlso (actualEref.StartsWith("POYTCPE") OrElse actualEref.StartsWith("POYBCPE")) Then
                            referenceType = "NONREF"
                        Else
                            referenceType = "NONREF"
                        End If
                    Case "2065", "065"
                        ' Check batch_entry_reference for OM1B/OM1T pattern
                        If Not String.IsNullOrEmpty(batchEntryRef) AndAlso (batchEntryRef.StartsWith("OM1B") OrElse batchEntryRef.StartsWith("OM1T")) Then
                            Dim om1tRef As String = batchEntryRef.Replace("OM1B", "OM1T")
                            referenceType = om1tRef.Substring(0, Math.Min(16, om1tRef.Length))
                        Else
                            referenceType = "EREF"
                        End If
                    Case "501", "541", "1085"
                        referenceType = "EREF"  ' End-to-end reference
                    Case Else
                        referenceType = "EREF"  ' Default to EREF for unknown types
                End Select
                
                ' Build :61: line - NO entry date for Rabobank compliance
                Dim statementLine As String = String.Format("{0}{1}{2}{3}{4}", 
                    valueDateMT940, 
                    cdtDbtIndicator, 
                    amountMT940, 
                    correctTransactionTypeCode,
                    referenceType)
                    
                ' Add institution reference if available - except for transaction type 586
                If proprietaryCode <> "586" Then
                    If Not String.IsNullOrEmpty(referenceField) Then
                        statementLine += String.Format("//{0}", referenceField)
                    ElseIf Not String.IsNullOrEmpty(acctSvcrRef) Then
                        statementLine += String.Format("//{0}", acctSvcrRef)
                    End If
                End If
                
                messageContent.AppendLine(String.Format(":61:{0}", statementLine))
                
                ' Add counterparty IBAN on separate line
                Dim counterpartyIban As String = ""
                If txAmount >= 0 AndAlso Not String.IsNullOrEmpty(debtorIban) Then
                    counterpartyIban = debtorIban
                ElseIf txAmount < 0 AndAlso Not String.IsNullOrEmpty(creditorIban) Then
                    counterpartyIban = creditorIban
                End If
                
                If Not String.IsNullOrEmpty(counterpartyIban) Then
                    messageContent.AppendLine(counterpartyIban)
                Else
                    messageContent.AppendLine("0000000000")
                End If
                
                ' :86: Information to Account Owner (if full format)
                If Not minimumRequiredFields Then
                    Dim structuredInfo As New StringBuilder()
                    
                    ' Get reference data for :86: field
                    Dim actualEref As String = GetSafeColumnValue(txRow, "end_to_end_id")
                    If String.IsNullOrEmpty(actualEref) OrElse actualEref = "NOTPROVIDED" Then
                        actualEref = GetSafeColumnValue(txRow, "payment_information_identification")
                        If String.IsNullOrEmpty(actualEref) Then
                            actualEref = GetSafeColumnValue(txRow, "instruction_id")
                            If String.IsNullOrEmpty(actualEref) Then
                                actualEref = referenceField
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
                    
        ' Build structured info per Rabobank specification order
        
        ' 1. Reference field - depends on transaction type per Rabobank specification
        If proprietaryCode = "64" Then
            ' Transaction type 064 uses /MARF/ (Mandate Reference) AND /EREF/
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
            ' Add PREF if it's an OO9B reference
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
        
        ' 3. Payment Information ID - No longer needed here as Type 501 is handled above                    Dim returnCode As String = GetSafeColumnValue(txRow, "return_reason_code")
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
                    
                    ' 9. Charges - Include all available charge fields
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
                        ' Type 586: Always include date for all PREF formats
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
                    
                    ' Output with line wrapping
                    Dim structuredText As String = structuredInfo.ToString()
                    If structuredText.Length > 0 Then
                        Dim wrappedLines As List(Of String) = WrapMT940Line(structuredText)
                        For i As Integer = 0 To Math.Min(wrappedLines.Count - 1, 5)
                            If i = 0 Then
                                messageContent.AppendLine(String.Format(":86:{0}", wrappedLines(i)))
                            Else
                                Dim line As String = wrappedLines(i)
                                If line.StartsWith(":") OrElse line.StartsWith("–") Then
                                    line = " " + line.Substring(1)
                                End If
                                messageContent.AppendLine(line)
                            End If
                        Next
                    End If
                End If
                
                txProcessed += 1
                
            Catch ex As Exception
                Console.WriteLine($"  ERROR processing transaction for {iban}: {ex.Message}")
            End Try
        Next ' End transaction processing
        
        ' :62F: Closing Balance (MANDATORY)
        Dim closingCdtDbt As String = GetCdtDbtIndicator(closingBalance)
        Dim closingDateMT940 As String = reportDate.ToString("yyMMdd")
        Dim closingAmountMT940 As String = FormatBalanceAmountMT940(closingBalance)
        messageContent.AppendLine(String.Format(":62F:{0}{1}{2}{3}", closingCdtDbt, closingDateMT940, currency, closingAmountMT940))
        
        ' Optional fields for full format
        If Not minimumRequiredFields Then
            ' :64: Closing Available Balance
            messageContent.AppendLine(String.Format(":64:{0}{1}{2}{3}", closingCdtDbt, closingDateMT940, currency, closingAmountMT940))
            
            ' :65: Forward Available Balances (4 days)
            For i As Integer = 1 To 4
                Dim forwardDate As String = reportDate.AddDays(i).ToString("yyMMdd")
                messageContent.AppendLine(String.Format(":65:{0}{1}{2}{3}", closingCdtDbt, forwardDate, currency, closingAmountMT940))
            Next
        End If
        
        ' End of message indicator
        messageContent.AppendLine("-")
        
        ' Combine with SWIFT blocks if needed
        If fullSwiftFormat Then
            mt940.Append(messageContent.ToString())
            mt940.AppendLine("}")
            
            ' Trailer block with checksum
            Dim checksum As String = Math.Abs(messageContent.ToString().GetHashCode()).ToString("X8")
            mt940.AppendLine(String.Format("{{5:{{CHK:{0}}}}}", checksum))
        Else
            mt940.Append(messageContent.ToString())
        End If
        
        ' Write MT940 file for this account (or add to combined content)
        If combinedOutput Then
            ' Add to combined content
            combinedMT940Content.Append(mt940.ToString())
            combinedMT940Content.AppendLine() ' Blank line between accounts
        Else
            ' Write separate file per account
            File.WriteAllText(currentMt940FilePath, mt940.ToString(), System.Text.Encoding.UTF8)
            generatedFiles.Add(currentMt940FilePath)
        End If
        
        ' Balance validation
        Dim calculatedClosing As Decimal = openingBalance
        For Each txRow As DataRow In accountTransactions
            calculatedClosing += Convert.ToDecimal(txRow("transaction_amount"))
        Next
        
        Dim balanceValid As Boolean = Math.Abs(calculatedClosing - closingBalance) <= 0.01
        Dim validationStatus As String = If(balanceValid, "PASSED", "FAILED")
        
        ' Account summary
        Dim accountSummary As String = String.Format(
            "  ✓ {0} | {1} txs | {2} {3} → {4} {5} | Balance: {6}",
            iban,
            txProcessed,
            If(openingBalance >= 0, "C", "D"),
            FormatAmountMT940(openingBalance),
            If(closingBalance >= 0, "C", "D"),
            FormatAmountMT940(closingBalance),
            validationStatus)
        
        Console.WriteLine(accountSummary)
        processingStats.Add(accountSummary)
        
        If Not balanceValid Then
            accountsWithErrors.Add($"{iban}: Balance mismatch - Opening: {openingBalance:F2}, Calculated: {calculatedClosing:F2}, Closing: {closingBalance:F2}")
        End If
        
    Catch ex As Exception
        Console.WriteLine($"  ✗ ERROR processing account {balanceRow("iban")}: {ex.Message}")
        accountsWithErrors.Add($"{balanceRow("iban")}: {ex.Message}")
    End Try
Next ' End account loop

' Write combined file if combined output mode
If combinedOutput AndAlso dt_camt053_data.Rows.Count > 0 Then
    Dim firstRow As DataRow = dt_camt053_data.Rows(0)
    Dim reportDate As DateTime = Convert.ToDateTime(firstRow("day"))
    Dim combinedFileName As String = String.Format("MT940_R_Combined_{0}_{1}_{2}.swi",
        reportDate.ToString("yyyyMMdd"),
        DateTime.Now.ToString("yyyyMMdd"),
        DateTime.Now.ToString("HHmmss"))
    Dim combinedFilePath As String = Path.Combine(outputDir, combinedFileName)
    File.WriteAllText(combinedFilePath, combinedMT940Content.ToString(), System.Text.Encoding.UTF8)
    generatedFiles.Add(combinedFilePath)
    Console.WriteLine($"Combined file created: {combinedFileName}")
End If

' Final summary
Console.WriteLine("")
Console.WriteLine("=== MT940 MULTI-ACCOUNT GENERATION COMPLETE ===")
Console.WriteLine($"Accounts processed: {dt_camt053_data.Rows.Count}")
Console.WriteLine($"Files generated: {generatedFiles.Count}")
Console.WriteLine($"Errors encountered: {accountsWithErrors.Count}")
Console.WriteLine($"Output directory: {outputDir}")

If generatedFiles.Count > 0 Then
    Console.WriteLine("")
    Console.WriteLine("Generated files:")
    For Each filePath As String In generatedFiles
        Dim fileInfo As New FileInfo(filePath)
        Console.WriteLine($"  - {fileInfo.Name} ({fileInfo.Length:N0} bytes)")
    Next
End If

If accountsWithErrors.Count > 0 Then
    Console.WriteLine("")
    Console.WriteLine("Errors/Warnings:")
    For Each errorMsg As String In accountsWithErrors
        Console.WriteLine($"  ! {errorMsg}")
    Next
End If

' Set output variables for UiPath (backward compatibility)
If generatedFiles.Count > 0 Then
    mt940FilePath = generatedFiles(0) ' First file for single-file workflows
    Console.WriteLine($"Primary output file: {Path.GetFileName(mt940FilePath)}")
End If

' Optional: Create summary report (always create for multiple accounts)
If dt_camt053_data.Rows.Count > 1 Then
    Dim firstRow As DataRow = dt_camt053_data.Rows(0)
    Dim reportDate As DateTime = Convert.ToDateTime(firstRow("day"))
    Dim summaryPath As String = Path.Combine(outputDir, $"MT940_R_Multi_{reportDate:yyyyMMdd}_{DateTime.Now:yyyyMMdd_HHmmss}.txt")
    Dim summaryContent As New StringBuilder()
    summaryContent.AppendLine("MT940 Multi-Account Generation Summary")
    summaryContent.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
    summaryContent.AppendLine($"Output directory: {outputDir}")
    summaryContent.AppendLine("")
    summaryContent.AppendLine("Processing Results:")
    For Each stat As String In processingStats
        summaryContent.AppendLine(stat)
    Next
    summaryContent.AppendLine("")
    summaryContent.AppendLine("Generated Files:")
    For Each filePath As String In generatedFiles
        summaryContent.AppendLine($"  {Path.GetFileName(filePath)}")
    Next
    
    File.WriteAllText(summaryPath, summaryContent.ToString(), System.Text.Encoding.UTF8)
    Console.WriteLine($"Summary report: {Path.GetFileName(summaryPath)}")
End If

<function_calls>
<invoke name="manage_todo_list">
<parameter name="todoList">[{"title": "Maak multi-account MT940 script gebaseerd op huidige versie", "id": 1, "status": "completed"}]