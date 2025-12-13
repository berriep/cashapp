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
    
    # Test the exact query from the reconciliation function
    days = 30
    iban_filter = None  # Empty string becomes None
    
    print(f"Testing with days={days}, iban_filter={iban_filter}")
    print(f"iban_filter is None: {iban_filter is None}")
    print(f"iban_filter == '': {iban_filter == ''}")
    
    query = """
        WITH params AS (
            SELECT
                CURRENT_DATE AS target_date,
                %s AS days_back,
                CASE WHEN %s IS NOT NULL THEN ARRAY[%s]::text[] ELSE ARRAY[]::text[] END AS ibans
        )
        SELECT (SELECT ibans FROM params) as iban_array,
               (SELECT cardinality(ibans) FROM params) as array_size
    """
    
    cur.execute(query, (days, iban_filter, iban_filter))
    result = cur.fetchone()
    print(f"\nQuery result:")
    print(f"  iban_array: {result[0]}")
    print(f"  array_size: {result[1]}")
    
    # Now test the full reconciliation query
    print("\n=== Running full reconciliation query ===")
    
    full_query = """
        WITH params AS (
            SELECT
                CURRENT_DATE AS target_date,
                %s AS days_back,
                CASE WHEN %s IS NOT NULL AND %s != '' THEN ARRAY[%s]::text[] ELSE ARRAY[]::text[] END AS ibans
        ),
        range AS (
            SELECT
                (SELECT target_date FROM params) - (SELECT days_back FROM params) * INTERVAL '1 day' AS start_date,
                (SELECT target_date FROM params) AS end_date
        ),
        ib_all AS (
            SELECT DISTINCT iban
            FROM (
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_balances
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_transactions
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_account_info
            ) all_ibans
            CROSS JOIN params p
            WHERE (
                (SELECT cardinality(p.ibans)) = 0
                OR iban = ANY(p.ibans)
            )
        )
        SELECT COUNT(*) FROM ib_all
    """
    
    cur.execute(full_query, (days, iban_filter, iban_filter, iban_filter))
    count = cur.fetchone()[0]
    print(f"Total IBANs found: {count}")
    
    conn.close()
    
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
