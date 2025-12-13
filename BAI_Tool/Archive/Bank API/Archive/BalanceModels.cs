using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RabobankZero
{
    /// <summary>
    /// Response model for Balance API calls
    /// </summary>
    public class BalanceResponse
    {
        [JsonProperty("account")]
        public AccountReference Account { get; set; } = new();

        [JsonProperty("balances")]
        public List<Balance> Balances { get; set; } = new();

        [JsonProperty("piggyBanks")]
        public List<PiggyBank>? PiggyBanks { get; set; }
    }

    /// <summary>
    /// Individual balance entry with type and amount
    /// </summary>
    public class Balance
    {
        [JsonProperty("balanceAmount")]
        public Amount BalanceAmount { get; set; } = new();

        [JsonProperty("balanceType")]
        public string BalanceType { get; set; } = string.Empty;

        [JsonProperty("lastChangeDateTime")]
        public DateTime? LastChangeDateTime { get; set; }

        [JsonProperty("referenceDate")]
        public string? ReferenceDate { get; set; }
    }

    /// <summary>
    /// Account reference with IBAN and currency
    /// </summary>
    public class AccountReference
    {
        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonProperty("iban")]
        public string Iban { get; set; } = string.Empty;
    }

    /// <summary>
    /// Amount with value and currency
    /// </summary>
    public class Amount
    {
        [JsonProperty("amount")]
        public string Value { get; set; } = string.Empty;

        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// Convert string amount to decimal for calculations
        /// </summary>
        public decimal GetDecimalValue()
        {
            if (decimal.TryParse(Value, out var result))
                return result;
            return 0m;
        }
    }

    /// <summary>
    /// Piggy bank information (optional in response)
    /// </summary>
    public class PiggyBank
    {
        [JsonProperty("piggyBankName")]
        public string PiggyBankName { get; set; } = string.Empty;

        [JsonProperty("piggyBankBalance")]
        public string PiggyBankBalance { get; set; } = string.Empty;
    }

    /// <summary>
    /// Balance types as defined in the API
    /// </summary>
    public static class BalanceTypes
    {
        public const string Expected = "expected";
        public const string InterimBooked = "interimBooked";
        public const string ClosingBooked = "closingBooked";
    }

    /// <summary>
    /// Complete CAMT-ready dataset with opening balance, transactions, and closing balance
    /// </summary>
    public class CamtDataSet
    {
        public AccountReference Account { get; set; } = new();
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        
        // Balance information
        public Balance? OpeningBalance { get; set; }
        public Balance? ClosingBalance { get; set; }
        public Balance? CurrentExpected { get; set; }
        public Balance? CurrentInterimBooked { get; set; }
        
        // Transaction data
        public TransactionResponse? Transactions { get; set; }
        
        // Calculated values
        public decimal OpeningAmount => OpeningBalance?.BalanceAmount.GetDecimalValue() ?? 0m;
        public decimal ClosingAmount => ClosingBalance?.BalanceAmount.GetDecimalValue() ?? 0m;
        public decimal TransactionSum { get; set; }
        public int TransactionCount { get; set; }
        
        // Validation
        public bool IsBalanceValid => Math.Abs((OpeningAmount + TransactionSum) - ClosingAmount) < 0.01m;
        
        /// <summary>
        /// Summary for CAMT generation
        /// </summary>
        public string GetSummary()
        {
            return $"CAMT Dataset Summary:\n" +
                   $"Account: {Account.Iban} ({Account.Currency})\n" +
                   $"Period: {DateFrom:yyyy-MM-dd} to {DateTo:yyyy-MM-dd}\n" +
                   $"Opening Balance: {OpeningAmount:F2} {Account.Currency}\n" +
                   $"Closing Balance: {ClosingAmount:F2} {Account.Currency}\n" +
                   $"Transaction Sum: {TransactionSum:F2} {Account.Currency}\n" +
                   $"Transaction Count: {TransactionCount}\n" +
                   $"Balance Validation: {(IsBalanceValid ? "✅ Valid" : "❌ Invalid")}";
        }
    }

    /// <summary>
    /// Transaction response models (existing from transaction API)
    /// </summary>
    public class TransactionResponse
    {
        [JsonProperty("account")]
        public AccountReference Account { get; set; } = new();

        [JsonProperty("transactions")]
        public TransactionContainer Transactions { get; set; } = new();
    }

    public class TransactionContainer
    {
        [JsonProperty("_links")]
        public Links? Links { get; set; }

        [JsonProperty("booked")]
        public List<Transaction> Booked { get; set; } = new();
    }

    public class Links
    {
        [JsonProperty("account")]
        public string? Account { get; set; }

        [JsonProperty("next")]
        public string? Next { get; set; }
    }

    public class Transaction
    {
        [JsonProperty("bookingDate")]
        public string BookingDate { get; set; } = string.Empty;

        [JsonProperty("creditorAccount")]
        public AccountReference? CreditorAccount { get; set; }

        [JsonProperty("creditorAgent")]
        public string? CreditorAgent { get; set; }

        [JsonProperty("debtorAccount")]
        public DebtorAccount? DebtorAccount { get; set; }

        [JsonProperty("debtorName")]
        public string? DebtorName { get; set; }

        [JsonProperty("entryReference")]
        public string? EntryReference { get; set; }

        [JsonProperty("initiatingPartyName")]
        public string? InitiatingPartyName { get; set; }

        [JsonProperty("raboBookingDateTime")]
        public DateTime? RaboBookingDateTime { get; set; }

        [JsonProperty("raboDetailedTransactionType")]
        public string? RaboDetailedTransactionType { get; set; }

        [JsonProperty("raboTransactionTypeName")]
        public string? RaboTransactionTypeName { get; set; }

        [JsonProperty("reasonCode")]
        public string? ReasonCode { get; set; }

        [JsonProperty("remittanceInformationUnstructured")]
        public string? RemittanceInformationUnstructured { get; set; }

        [JsonProperty("transactionAmount")]
        public Amount? TransactionAmount { get; set; }

        [JsonProperty("valueDate")]
        public string? ValueDate { get; set; }

        [JsonProperty("balanceAfterBooking")]
        public BalanceAfterBooking? BalanceAfterBooking { get; set; }

        [JsonProperty("bankTransactionCode")]
        public string? BankTransactionCode { get; set; }

        [JsonProperty("numberOfTransactions")]
        public int? NumberOfTransactions { get; set; }

        [JsonProperty("batchEntryReference")]
        public string? BatchEntryReference { get; set; }

        [JsonProperty("paymentInformationIdentification")]
        public string? PaymentInformationIdentification { get; set; }
    }

    public class DebtorAccount
    {
        [JsonProperty("iban")]
        public string Iban { get; set; } = string.Empty;
    }

    public class BalanceAfterBooking
    {
        [JsonProperty("balanceType")]
        public string BalanceType { get; set; } = string.Empty;

        [JsonProperty("balanceAmount")]
        public Amount BalanceAmount { get; set; } = new();
    }
}