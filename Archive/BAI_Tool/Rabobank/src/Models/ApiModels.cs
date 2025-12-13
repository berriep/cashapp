using Newtonsoft.Json;

namespace RabobankBAI.Models;

/// <summary>
/// Generic API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public Exception? Exception { get; set; }

    public static ApiResponse<T> Successful(T data, int statusCode = 200)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            StatusCode = statusCode
        };
    }

    public static ApiResponse<T> Failed(string errorMessage, int statusCode = 0, Exception? exception = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            Exception = exception
        };
    }
}

/// <summary>
/// Balance response model
/// </summary>
public class BalanceResponse
{
    [JsonProperty("balances")]
    public List<AccountBalance> Balances { get; set; } = new();
}

/// <summary>
/// Account balance model
/// </summary>
public class AccountBalance
{
    [JsonProperty("accountId")]
    public string? AccountId { get; set; }

    [JsonProperty("iban")]
    public string? Iban { get; set; }

    [JsonProperty("currency")]
    public string? Currency { get; set; }

    [JsonProperty("balance")]
    public decimal Balance { get; set; }

    [JsonProperty("availableBalance")]
    public decimal? AvailableBalance { get; set; }

    [JsonProperty("balanceDate")]
    public DateTime? BalanceDate { get; set; }

    [JsonProperty("accountName")]
    public string? AccountName { get; set; }
}

/// <summary>
/// Transaction response model
/// </summary>
public class TransactionResponse
{
    [JsonProperty("transactions")]
    public List<Transaction> Transactions { get; set; } = new();

    [JsonProperty("nextPage")]
    public string? NextPage { get; set; }
}

/// <summary>
/// Transaction model
/// </summary>
public class Transaction
{
    [JsonProperty("transactionId")]
    public string? TransactionId { get; set; }

    [JsonProperty("accountId")]
    public string? AccountId { get; set; }

    [JsonProperty("iban")]
    public string? Iban { get; set; }

    [JsonProperty("amount")]
    public decimal Amount { get; set; }

    [JsonProperty("currency")]
    public string? Currency { get; set; }

    [JsonProperty("transactionDate")]
    public DateTime TransactionDate { get; set; }

    [JsonProperty("valueDate")]
    public DateTime? ValueDate { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("counterPartyName")]
    public string? CounterPartyName { get; set; }

    [JsonProperty("counterPartyIban")]
    public string? CounterPartyIban { get; set; }

    [JsonProperty("transactionType")]
    public string? TransactionType { get; set; }

    [JsonProperty("reference")]
    public string? Reference { get; set; }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("details")]
    public object? Details { get; set; }
}