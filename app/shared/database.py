import psycopg2
from psycopg2.extras import RealDictCursor
from config.config import Config
from datetime import datetime, timedelta

class Database:
    """Database connection and query management"""

    def __init__(self, db_type='bai'):
        """Initialize database connection
        
        Args:
            db_type (str): Either 'bai' (production) or 'recon' (accept)
        """
        self.db_type = db_type
        self.conn = None

    def connect(self):
        """Establish database connection"""
        if self.conn is None or self.conn.closed:
            self.conn = psycopg2.connect(
                Config.get_db_connection_string(self.db_type),
                cursor_factory=RealDictCursor
            )
        return self.conn

    def close(self):
        """Close database connection"""
        if self.conn and not self.conn.closed:
            self.conn.close()
    
    def execute_query(self, query, params=None):
        """Execute SELECT query and return results"""
        conn = self.connect()
        try:
            with conn.cursor() as cur:
                cur.execute(query, params)
                return cur.fetchall()
        except Exception as e:
            conn.rollback()
            raise e
    
    def execute_update(self, query, params=None):
        """Execute INSERT/UPDATE/DELETE query without fetching results"""
        conn = self.connect()
        try:
            with conn.cursor() as cur:
                cur.execute(query, params)
                conn.commit()
        except Exception as e:
            conn.rollback()
            raise e
    
    def get_transaction_summary(self, days=7, iban_filter=None):
        """Get transaction summary for last N days"""
        if iban_filter:
            query = """
            SELECT 
                booking_date as date,
                iban,
                COUNT(*) as transaction_count,
                SUM(CASE WHEN transaction_amount > 0 THEN transaction_amount ELSE 0 END) as total_credit,
                SUM(CASE WHEN transaction_amount < 0 THEN ABS(transaction_amount) ELSE 0 END) as total_debit,
                SUM(transaction_amount) as net_amount
            FROM rpa_data.bai_rabobank_transactions
            WHERE booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND booking_date < CURRENT_DATE
                AND iban = %s
            GROUP BY booking_date, iban
            ORDER BY booking_date DESC, iban
            """
            return self.execute_query(query, (days, iban_filter))
        else:
            query = """
            SELECT 
                booking_date as date,
                iban,
                COUNT(*) as transaction_count,
                SUM(CASE WHEN transaction_amount > 0 THEN transaction_amount ELSE 0 END) as total_credit,
                SUM(CASE WHEN transaction_amount < 0 THEN ABS(transaction_amount) ELSE 0 END) as total_debit,
                SUM(transaction_amount) as net_amount
            FROM rpa_data.bai_rabobank_transactions
            WHERE booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND booking_date < CURRENT_DATE
            GROUP BY booking_date, iban
            ORDER BY booking_date DESC, iban
            """
            return self.execute_query(query, (days,))
    
    def get_balance_data(self, days=7, iban_filter=None):
        """Get balance data for reconciliation"""
        if iban_filter:
            query = """
            SELECT 
                reference_date as date,
                iban,
                balance_type,
                amount as balance,
                currency
            FROM rpa_data.bai_rabobank_balances
            WHERE reference_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND reference_date < CURRENT_DATE
                AND reference_date IS NOT NULL
                AND iban = %s
            ORDER BY reference_date DESC, iban, balance_type
            """
            return self.execute_query(query, (days, iban_filter))
        else:
            query = """
            SELECT 
                reference_date as date,
                iban,
                balance_type,
                amount as balance,
                currency
            FROM rpa_data.bai_rabobank_balances
            WHERE reference_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND reference_date < CURRENT_DATE
                AND reference_date IS NOT NULL
            ORDER BY reference_date DESC, iban, balance_type
            """
            return self.execute_query(query, (days,))
    
    def get_account_list(self):
        """Get list of all accounts"""
        query = """
        SELECT DISTINCT iban
        FROM rpa_data.bai_rabobank_transactions
        ORDER BY iban
        """
        return self.execute_query(query)
    
    def get_transaction_types(self, days=7, iban_filter=None):
        """Get transaction type breakdown"""
        if iban_filter:
            query = """
            SELECT 
                rabo_detailed_transaction_type,
                COUNT(*) as count,
                SUM(ABS(transaction_amount)) as total_amount
            FROM rpa_data.bai_rabobank_transactions
            WHERE booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND booking_date < CURRENT_DATE
                AND iban = %s
            GROUP BY rabo_detailed_transaction_type
            ORDER BY count DESC
            """
            return self.execute_query(query, (days, iban_filter))
        else:
            query = """
            SELECT 
                rabo_detailed_transaction_type,
                COUNT(*) as count,
                SUM(ABS(transaction_amount)) as total_amount
            FROM rpa_data.bai_rabobank_transactions
            WHERE booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND booking_date < CURRENT_DATE
            GROUP BY rabo_detailed_transaction_type
            ORDER BY count DESC
            """
            return self.execute_query(query, (days,))
    
    def get_data_quality_status(self, days=7):
        """Get data quality status per day/IBAN (for dashboard overview)"""
        query = """
        WITH date_range AS (
            SELECT generate_series(
                CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day',
                CURRENT_DATE - INTERVAL '1 day',
                '1 day'::interval
            )::date as day
        ),
        all_ibans AS (
            SELECT DISTINCT iban
            FROM (
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_balances
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_transactions
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_api_audit_log
            ) i
        ),
        date_iban_grid AS (
            SELECT dr.day, ai.iban
            FROM date_range dr
            CROSS JOIN all_ibans ai
        ),
        api_status AS (
            SELECT 
                l.closingdate::date as day,
                l.iban,
                MAX(CASE WHEN l.endpoint = 'balances' AND l.response_status = 200 THEN 'OK' ELSE 'NOK' END) as balance_status,
                MAX(CASE WHEN l.endpoint = 'transactions' AND l.response_status = 200 THEN 'OK' ELSE 'NOK' END) as transaction_status
            FROM rpa_data.bai_api_audit_log l
            WHERE l.closingdate >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND l.closingdate < CURRENT_DATE
                AND l.endpoint IN ('balances', 'transactions')
            GROUP BY l.closingdate::date, l.iban
        ),
        transaction_count AS (
            SELECT 
                t.booking_date as day,
                t.iban,
                COUNT(*) as tx_count
            FROM rpa_data.bai_rabobank_transactions t
            WHERE t.booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'
                AND t.booking_date < CURRENT_DATE
            GROUP BY t.booking_date, t.iban
        )
        SELECT 
            dig.day as date,
            dig.iban,
            COALESCE(api.balance_status, 'NOK') as balance_status,
            COALESCE(api.transaction_status, 'NOK') as transaction_status,
            COALESCE(tc.tx_count, 0) as tx_count,
            CASE 
                WHEN COALESCE(api.balance_status, 'NOK') = 'OK' 
                 AND COALESCE(api.transaction_status, 'NOK') = 'OK' 
                THEN 'OK' 
                ELSE 'NOK' 
            END as overall_status
        FROM date_iban_grid dig
        LEFT JOIN api_status api ON dig.day = api.day AND dig.iban = api.iban
        LEFT JOIN transaction_count tc ON dig.day = tc.day AND dig.iban = tc.iban
        ORDER BY dig.day DESC, dig.iban
        """
        return self.execute_query(query, (days, days, days))
    
    def get_daily_reconciliation(self, target_date=None, iban_filter=None, days=7):
        """Get daily reconciliation data (opening + transactions = closing)"""
        if target_date is None:
            target_date = datetime.now().date()
        
        if iban_filter:
            query = """
            SELECT 
                t.booking_date as date,
                t.iban,
                MIN(b_open.amount) as opening_balance,
                SUM(CASE WHEN t.transaction_amount > 0 THEN t.transaction_amount ELSE 0 END) as total_credit,
                SUM(CASE WHEN t.transaction_amount < 0 THEN ABS(t.transaction_amount) ELSE 0 END) as total_debit,
                SUM(t.transaction_amount) as transaction_total,
                MIN(b_open.amount) + SUM(t.transaction_amount) as calculated_closing,
                MIN(b_close.amount) as closing_balance,
                ABS(MIN(b_open.amount) + SUM(t.transaction_amount) - COALESCE(MIN(b_close.amount), 0)) as difference,
                CASE WHEN ABS(MIN(b_open.amount) + SUM(t.transaction_amount) - COALESCE(MIN(b_close.amount), 0)) < 0.01 
                    THEN 'OK' ELSE 'MISMATCH' END as status
            FROM rpa_data.bai_rabobank_transactions t
            LEFT JOIN rpa_data.bai_rabobank_balances b_open 
                ON b_open.iban = t.iban 
                AND b_open.reference_date = t.booking_date - INTERVAL '1 day'
            LEFT JOIN rpa_data.bai_rabobank_balances b_close 
                ON b_close.iban = t.iban 
                AND b_close.reference_date = t.booking_date
            WHERE t.booking_date >= CURRENT_DATE - %s
                AND t.iban = %s
            GROUP BY t.booking_date, t.iban
            ORDER BY t.booking_date DESC, t.iban
            """
            return self.execute_query(query, (days, iban_filter))
        else:
            query = """
            SELECT 
                t.booking_date as date,
                t.iban,
                MIN(b_open.amount) as opening_balance,
                SUM(CASE WHEN t.transaction_amount > 0 THEN t.transaction_amount ELSE 0 END) as total_credit,
                SUM(CASE WHEN t.transaction_amount < 0 THEN ABS(t.transaction_amount) ELSE 0 END) as total_debit,
                SUM(t.transaction_amount) as transaction_total,
                MIN(b_open.amount) + SUM(t.transaction_amount) as calculated_closing,
                MIN(b_close.amount) as closing_balance,
                ABS(MIN(b_open.amount) + SUM(t.transaction_amount) - COALESCE(MIN(b_close.amount), 0)) as difference,
                CASE WHEN ABS(MIN(b_open.amount) + SUM(t.transaction_amount) - COALESCE(MIN(b_close.amount), 0)) < 0.01 
                    THEN 'OK' ELSE 'MISMATCH' END as status
            FROM rpa_data.bai_rabobank_transactions t
            LEFT JOIN rpa_data.bai_rabobank_balances b_open 
                ON b_open.iban = t.iban 
                AND b_open.reference_date = t.booking_date - INTERVAL '1 day'
            LEFT JOIN rpa_data.bai_rabobank_balances b_close 
                ON b_close.iban = t.iban 
                AND b_close.reference_date = t.booking_date
            WHERE t.booking_date >= CURRENT_DATE - %s
            GROUP BY t.booking_date, t.iban
            ORDER BY t.booking_date DESC, t.iban
            """
            return self.execute_query(query, (days,))
    
    def get_missing_days(self, days=30):
        """Detect missing transaction days"""
        query = """
        WITH date_series AS (
            SELECT generate_series(
                CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day',
                CURRENT_DATE - INTERVAL '1 day',
                '1 day'::interval
            )::date as date
        ),
        actual_dates AS (
            SELECT DISTINCT booking_date as date
            FROM rpa_data.bai_rabobank_transactions
        )
        SELECT ds.date
        FROM date_series ds
        LEFT JOIN actual_dates ad ON ds.date = ad.date
        WHERE ad.date IS NULL
        ORDER BY ds.date DESC
        """
        return self.execute_query(query, (days,))
    
    def get_detailed_reconciliation(self, days=7, iban_filter=None):
        """Get detailed balance/transaction reconciliation report"""
        # Ensure empty string becomes None
        if iban_filter == '':
            iban_filter = None
            
        query = """
        WITH params AS (
            SELECT
                CURRENT_DATE - INTERVAL '1 day' AS target_date,
                %s AS days_back,
                CASE 
                    WHEN %s IS NOT NULL AND %s != '' THEN ARRAY[%s]::text[] 
                    ELSE ARRAY[]::text[] 
                END AS ibans
        ),
        range AS (
            SELECT
                (SELECT target_date FROM params) - ((SELECT days_back FROM params) - 1) * INTERVAL '1 day' AS start_date,
                (SELECT target_date FROM params) AS end_date
        ),
        rng AS (
            SELECT generate_series(
                (SELECT start_date FROM range),
                (SELECT end_date FROM range),
                '1 day'
            )::date AS day
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
        ),
        audit_pivot AS (
            SELECT
                l.closingdate::date AS day,
                l.iban,
                CASE WHEN bool_or(l.response_status = 200) FILTER (WHERE l.endpoint = 'balances') 
                    THEN 'OK' ELSE 'MISSING' END AS balances_status,
                CASE WHEN bool_or(l.response_status = 200) FILTER (WHERE l.endpoint = 'transactions') 
                    THEN 'OK' ELSE 'MISSING' END AS transactions_status
            FROM rpa_data.bai_api_audit_log l
            CROSS JOIN range r
            CROSS JOIN params p
            WHERE l.closingdate BETWEEN r.start_date AND r.end_date
                AND l.endpoint IN ('balances','transactions')
                AND (
                    (SELECT cardinality(p.ibans)) = 0
                    OR l.iban = ANY(p.ibans)
                )
            GROUP BY l.closingdate::date, l.iban
        ),
        daily_closing AS (
            SELECT
                b.iban,
                b.reference_date::date AS day,
                b.currency,
                b.amount AS closing_balance,
                b.audit_id AS closing_audit_id
            FROM rpa_data.bai_rabobank_balances b
            CROSS JOIN range r
            CROSS JOIN params p
            WHERE LOWER(b.balance_type) IN ('closingbooked', 'closing_booked')
                AND b.reference_date IS NOT NULL
                AND b.reference_date BETWEEN r.start_date AND r.end_date
                AND (
                    (SELECT cardinality(p.ibans)) = 0
                    OR b.iban = ANY(p.ibans)
                )
        ),
        daily_opening AS (
            SELECT
                b.iban,
                (b.reference_date::date + INTERVAL '1 day')::date AS day,
                b.currency,
                b.amount AS opening_balance,
                b.audit_id AS opening_audit_id
            FROM rpa_data.bai_rabobank_balances b
            CROSS JOIN range r
            CROSS JOIN params p
            WHERE LOWER(b.balance_type) IN ('closingbooked', 'closing_booked')
                AND b.reference_date IS NOT NULL
                AND b.reference_date BETWEEN r.start_date - INTERVAL '1 day' 
                    AND r.end_date - INTERVAL '1 day'
                AND (
                    (SELECT cardinality(p.ibans)) = 0
                    OR b.iban = ANY(p.ibans)
                )
        ),
        daily_transactions AS (
            SELECT
                iban,
                booking_date,
                COUNT(*) AS transaction_count,
                SUM(transaction_amount) AS total_transactions,
                COUNT(*) FILTER (WHERE transaction_amount > 0) AS pos_tx_count,
                SUM(CASE WHEN transaction_amount > 0 THEN transaction_amount ELSE 0 END) AS pos_tx_sum,
                COUNT(*) FILTER (WHERE transaction_amount < 0) AS neg_tx_count,
                SUM(CASE WHEN transaction_amount < 0 THEN transaction_amount ELSE 0 END) AS neg_tx_sum,
                MAX(currency) AS currency
            FROM rpa_data.bai_rabobank_transactions t
            CROSS JOIN range r
            CROSS JOIN params p
            WHERE booking_date BETWEEN r.start_date AND r.end_date
                AND (
                    (SELECT cardinality(p.ibans)) = 0
                    OR t.iban = ANY(p.ibans)
                )
            GROUP BY iban, booking_date
        )
        SELECT
            i.iban,
            COALESCE(ai.owner_name, '') AS owner_name,
            r.day,
            CASE
                WHEN o.opening_balance IS NULL THEN 'MISSING_OPENING'
                WHEN c.closing_balance IS NULL THEN 'MISSING_CLOSING'
                WHEN ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(dt.total_transactions, 0))) < 0.01 
                    THEN 'PERFECT_MATCH'
                WHEN ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(dt.total_transactions, 0))) < 1.00 
                    THEN 'MINOR_DIFF'
                ELSE 'MAJOR_DIFF'
            END AS audit_status,
            COALESCE(a.balances_status, 'MISSING') AS balances_status,
            COALESCE(a.transactions_status, 'MISSING') AS transactions_status,
            COALESCE(c.currency, o.currency, dt.currency, '') AS currency,
            COALESCE(o.opening_balance, 0)::numeric(18,2) AS opening_balance,
            COALESCE(dt.total_transactions, 0)::numeric(18,2) AS sum_transactions,
            COALESCE(dt.transaction_count, 0)::int AS transaction_count,
            COALESCE(dt.pos_tx_sum, 0)::numeric(18,2) AS pos_tx_sum,
            COALESCE(dt.pos_tx_count, 0)::int AS pos_tx_count,
            COALESCE(dt.neg_tx_sum, 0)::numeric(18,2) AS neg_tx_sum,
            COALESCE(dt.neg_tx_count, 0)::int AS neg_tx_count,
            COALESCE(c.closing_balance, 0)::numeric(18,2) AS closing_balance,
            ROUND(COALESCE(o.opening_balance, 0) + COALESCE(dt.total_transactions, 0), 2) AS expected_closing,
            ROUND(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(dt.total_transactions, 0)), 2) AS difference
        FROM ib_all i
        CROSS JOIN rng r
        LEFT JOIN audit_pivot a ON a.day = r.day AND a.iban = i.iban
        LEFT JOIN daily_opening o ON o.iban = i.iban AND o.day = r.day
        LEFT JOIN daily_closing c ON c.iban = i.iban AND c.day = r.day
        LEFT JOIN daily_transactions dt ON dt.iban = i.iban AND dt.booking_date = r.day
        LEFT JOIN rpa_data.bai_rabobank_account_info ai ON ai.iban = i.iban
        ORDER BY i.iban, r.day DESC
        """
        return self.execute_query(query, (days, iban_filter, iban_filter, iban_filter))
    
    def get_transaction_details(self, days=7, iban_filter=None, date_from=None, date_to=None, amount_min=None, amount_max=None, counterparty_filter=None):
        """Get individual transaction details with filters"""
        params = []
        where_clauses = []
        
        # Build date filter
        if date_from:
            where_clauses.append(f"booking_date >= %s")
            params.append(date_from)
        elif days:
            where_clauses.append(f"booking_date >= CURRENT_DATE - INTERVAL '1 day' - INTERVAL '%s days' + INTERVAL '1 day'")
            params.append(days)
        
        if date_to:
            where_clauses.append(f"booking_date <= %s")
            params.append(date_to)
        else:
            where_clauses.append(f"booking_date < CURRENT_DATE")
        
        # IBAN filter - support single string or list of IBANs
        if iban_filter:
            if isinstance(iban_filter, list):
                # Multiple IBANs
                placeholders = ','.join(['%s'] * len(iban_filter))
                where_clauses.append(f"iban IN ({placeholders})")
                params.extend(iban_filter)
            else:
                # Single IBAN
                where_clauses.append(f"iban = %s")
                params.append(iban_filter)
        
        # Amount filters
        if amount_min is not None:
            where_clauses.append(f"transaction_amount >= %s")
            params.append(amount_min)
        
        if amount_max is not None:
            where_clauses.append(f"transaction_amount <= %s")
            params.append(amount_max)
        
        # Counterparty filter (search in both creditor and debtor IBAN and names)
        if counterparty_filter:
            where_clauses.append(f"(creditor_iban ILIKE %s OR debtor_iban ILIKE %s OR creditor_name ILIKE %s OR debtor_name ILIKE %s)")
            search_pattern = f"%{counterparty_filter}%"
            params.extend([search_pattern, search_pattern, search_pattern, search_pattern])
        
        where_clause = " AND ".join(where_clauses) if where_clauses else "1=1"
        
        query = f"""
        SELECT 
            booking_date,
            iban,
            transaction_amount,
            creditor_iban,
            creditor_name,
            debtor_iban,
            debtor_name,
            remittance_information_unstructured,
            rabo_detailed_transaction_type,
            rabo_transaction_type_name,
            entry_reference,
            end_to_end_id,
            created_at
        FROM rpa_data.bai_rabobank_transactions
        WHERE {where_clause}
        ORDER BY booking_date DESC, created_at DESC
        LIMIT 10000
        """
        
        return self.execute_query(query, tuple(params))
    
    def get_bank_statement_summary(self, iban, date_from, date_to):
        """Get bank statement summary including opening/closing balance and totals"""
        query = """
        WITH statement_transactions AS (
            SELECT 
                iban,
                transaction_amount,
                booking_date
            FROM rpa_data.bai_rabobank_transactions
            WHERE iban = %s
                AND booking_date >= %s
                AND booking_date <= %s
        ),
        opening_balance AS (
            SELECT 
                amount as balance,
                reference_date
            FROM rpa_data.bai_rabobank_balances
            WHERE iban = %s
                AND balance_type = 'closingBooked'
                AND reference_date = %s::date - INTERVAL '1 day'
            LIMIT 1
        ),
        closing_balance AS (
            SELECT 
                amount as balance,
                reference_date
            FROM rpa_data.bai_rabobank_balances
            WHERE iban = %s
                AND balance_type = 'closingBooked'
                AND reference_date = %s
            LIMIT 1
        ),
        account_info AS (
            SELECT 
                owner_name,
                iban,
                currency
            FROM rpa_data.bai_rabobank_account_info
            WHERE iban = %s
            LIMIT 1
        )
        SELECT 
            ai.owner_name as account_name,
            ai.iban,
            ai.currency,
            COALESCE(ob.balance, 0) as opening_balance,
            COALESCE(cb.balance, 0) as closing_balance,
            COALESCE(SUM(CASE WHEN st.transaction_amount < 0 THEN ABS(st.transaction_amount) ELSE 0 END), 0) as total_debited,
            COALESCE(SUM(CASE WHEN st.transaction_amount > 0 THEN st.transaction_amount ELSE 0 END), 0) as total_credited,
            COUNT(st.transaction_amount) as transaction_count,
            %s as statement_date,
            %s as date_from,
            %s as date_to
        FROM account_info ai
        LEFT JOIN opening_balance ob ON 1=1
        LEFT JOIN closing_balance cb ON 1=1
        LEFT JOIN statement_transactions st ON st.iban = ai.iban
        GROUP BY ai.owner_name, ai.iban, ai.currency, ob.balance, cb.balance
        """
        
        from datetime import date
        statement_date = date.today()
        
        return self.execute_query(query, (
            iban, date_from, date_to,  # statement_transactions
            iban, date_from,  # opening_balance
            iban, date_to,  # closing_balance
            iban,  # account_info
            statement_date, date_from, date_to  # SELECT clause
        ))
    
    def get_bank_statement_transactions(self, iban, date_from, date_to):
        """Get bank statement transaction details"""
        query = """
        SELECT 
            value_date as valuedate,
            rabo_detailed_transaction_type,
            rabo_transaction_type_name,
            debtor_iban,
            debtor_name,
            creditor_iban,
            creditor_name,
            remittance_information_unstructured as description,
            booking_date as processdate,
            end_to_end_id,
            transaction_amount
        FROM rpa_data.bai_rabobank_transactions
        WHERE iban = %s
            AND booking_date >= %s
            AND booking_date <= %s
        ORDER BY entry_reference ASC
        """
        
        return self.execute_query(query, (iban, date_from, date_to))

# Module-level database instances for each environment
# Shared database for users, authentication, audit logs (used by all modules)
shared_db = Database(db_type='shared')

# BAI module uses production database
bai_db = Database(db_type='bai')

# Recon module uses accept database
recon_db = Database(db_type='recon')

# Legacy singleton for backward compatibility (defaults to shared for auth)
db = shared_db
