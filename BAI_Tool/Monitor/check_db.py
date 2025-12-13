import psycopg2
import sys

try:
    conn = psycopg2.connect(
        host='psql-acc-weu-rpa-02.postgres.database.azure.com',
        port=5432,
        database='a5pgrpacp',
        user='rpa_pvcp_acc',
        password='h_!T3,.O&QSM>`,4yv-.~3Vwi',
        sslmode='require'
    )
    
    cur = conn.cursor()
    
    print("=== IBAN count per table ===")
    cur.execute("SELECT COUNT(DISTINCT iban) FROM rpa_data.bai_rabobank_balances")
    print(f"bai_rabobank_balances: {cur.fetchone()[0]} IBANs")
    
    cur.execute("SELECT COUNT(DISTINCT iban) FROM rpa_data.bai_rabobank_transactions")
    print(f"bai_rabobank_transactions: {cur.fetchone()[0]} IBANs")
    
    cur.execute("SELECT COUNT(DISTINCT iban) FROM rpa_data.bai_rabobank_account_info")
    print(f"bai_rabobank_account_info: {cur.fetchone()[0]} IBANs")
    
    print("\n=== Balance types in database ===")
    cur.execute("""
        SELECT balance_type, COUNT(*) 
        FROM rpa_data.bai_rabobank_balances 
        GROUP BY balance_type 
        ORDER BY COUNT(*) DESC
    """)
    for balance_type, count in cur.fetchall():
        print(f"  {balance_type}: {count} records")
    
    print("\n=== Sample IBANs from each source ===")
    cur.execute("SELECT DISTINCT iban FROM rpa_data.bai_rabobank_balances LIMIT 5")
    print("From balances:")
    for row in cur.fetchall():
        print(f"  {row[0]}")
    
    cur.execute("SELECT DISTINCT iban FROM rpa_data.bai_rabobank_transactions LIMIT 5")
    print("\nFrom transactions:")
    for row in cur.fetchall():
        print(f"  {row[0]}")
    
    print("\n=== Testing reconciliation query ===")
    cur.execute("""
        WITH all_ibans AS (
            SELECT DISTINCT iban FROM rpa_data.bai_rabobank_balances
            UNION
            SELECT DISTINCT iban FROM rpa_data.bai_rabobank_transactions
            UNION
            SELECT DISTINCT iban FROM rpa_data.bai_rabobank_account_info
        )
        SELECT COUNT(*) FROM all_ibans
    """)
    print(f"\nTotal unique IBANs across all tables: {cur.fetchone()[0]}")
    
    conn.close()
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
