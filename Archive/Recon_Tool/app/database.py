import psycopg2
from psycopg2.extras import RealDictCursor
from config.config import Config
from datetime import datetime, timedelta
from typing import List, Dict, Optional

class Database:
    """Database connection and query management"""
    
    def __init__(self):
        self.conn = None
        self.schema = Config.DB_SCHEMA
        
    def connect(self):
        """Establish database connection"""
        if self.conn is None or self.conn.closed:
            self.conn = psycopg2.connect(
                Config.get_db_connection_string(),
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
    
    # =====================================================
    # WORLDLINE PAYMENTS QUERIES
    # =====================================================
    
    def get_worldline_payments(self, start_date=None, end_date=None, brand=None, 
                               merchref=None, ref=None, status=None, limit=100, offset=0):
        """Get Worldline payments with filters and pagination"""
        conditions = []
        params = []
        
        if start_date:
            conditions.append("paydate >= %s")
            params.append(start_date)
        
        if end_date:
            conditions.append("paydate <= %s")
            params.append(end_date)
        
        if brand:
            conditions.append("brand = %s")
            params.append(brand)
        
        if merchref:
            conditions.append("merchref = %s")
            params.append(merchref)
        
        if ref:
            conditions.append("ref ILIKE %s")
            params.append(f"%{ref}%")
        
        if status:
            conditions.append("status = %s")
            params.append(status)
        
        where_clause = "WHERE " + " AND ".join(conditions) if conditions else ""
        
        query = f"""
            SELECT 
                id, ref, "order", status, lib, accept, paydate, facname1, country,
                total, cur, method, brand, card, merchref, batchref, owner,
                paydatetime, orderdatetime, source_file, created_at
            FROM {self.schema}.recon_worldline_payments
            {where_clause}
            ORDER BY paydate DESC, id DESC
            LIMIT %s OFFSET %s
        """
        params.extend([limit, offset])
        
        return self.execute_query(query, tuple(params))
    
    def get_worldline_payment_count(self, start_date=None, end_date=None, brand=None, 
                                    merchref=None, ref=None, status=None):
        """Get total count of Worldline payments matching filters"""
        conditions = []
        params = []
        
        if start_date:
            conditions.append("paydate >= %s")
            params.append(start_date)
        
        if end_date:
            conditions.append("paydate <= %s")
            params.append(end_date)
        
        if brand:
            conditions.append("brand = %s")
            params.append(brand)
        
        if merchref:
            conditions.append("merchref = %s")
            params.append(merchref)
        
        if ref:
            conditions.append("ref ILIKE %s")
            params.append(f"%{ref}%")
        
        if status:
            conditions.append("status = %s")
            params.append(status)
        
        where_clause = "WHERE " + " AND ".join(conditions) if conditions else ""
        
        query = f"""
            SELECT COUNT(*) as count
            FROM {self.schema}.recon_worldline_payments
            {where_clause}
        """
        
        result = self.execute_query(query, tuple(params))
        return result[0]['count'] if result else 0
    
    def get_worldline_summary_stats(self, days=30):
        """Get summary statistics for dashboard"""
        query = f"""
            SELECT
                COUNT(*) as total_transactions,
                COUNT(DISTINCT paydate) as days_with_data,
                COUNT(DISTINCT brand) as unique_brands,
                COUNT(DISTINCT merchref) as unique_merchants,
                SUM(total) as total_amount,
                AVG(total) as avg_amount,
                MIN(paydate) as earliest_date,
                MAX(paydate) as latest_date
            FROM {self.schema}.recon_worldline_payments
            WHERE paydate >= CURRENT_DATE - INTERVAL '%s days'
        """
        return self.execute_query(query, (days,))[0]
    
    def get_daily_volume(self, days=30):
        """Get daily transaction volume"""
        query = f"""
            SELECT
                paydate as date,
                COUNT(*) as transaction_count,
                SUM(total) as total_amount,
                AVG(total) as avg_amount,
                COUNT(DISTINCT brand) as unique_brands,
                COUNT(DISTINCT merchref) as unique_merchants
            FROM {self.schema}.recon_worldline_payments
            WHERE paydate >= CURRENT_DATE - INTERVAL '%s days'
            GROUP BY paydate
            ORDER BY paydate DESC
        """
        return self.execute_query(query, (days,))
    
    def get_brand_breakdown(self, days=30):
        """Get transaction breakdown by brand"""
        query = f"""
            SELECT
                brand,
                COUNT(*) as transaction_count,
                SUM(total) as total_amount,
                AVG(total) as avg_amount,
                COUNT(DISTINCT paydate) as days_active
            FROM {self.schema}.recon_worldline_payments
            WHERE paydate >= CURRENT_DATE - INTERVAL '%s days'
              AND brand IS NOT NULL AND brand != ''
            GROUP BY brand
            ORDER BY transaction_count DESC
        """
        return self.execute_query(query, (days,))
    
    def get_merchant_breakdown(self, days=30, limit=20):
        """Get top merchants by transaction volume"""
        query = f"""
            SELECT
                merchref,
                COUNT(*) as transaction_count,
                SUM(total) as total_amount,
                AVG(total) as avg_amount,
                MIN(paydate) as first_transaction,
                MAX(paydate) as last_transaction
            FROM {self.schema}.recon_worldline_payments
            WHERE paydate >= CURRENT_DATE - INTERVAL '%s days'
              AND merchref IS NOT NULL AND merchref != ''
            GROUP BY merchref
            ORDER BY transaction_count DESC
            LIMIT %s
        """
        return self.execute_query(query, (days, limit))
    
    def get_country_breakdown(self, days=30):
        """Get transaction breakdown by country"""
        query = f"""
            SELECT
                country,
                COUNT(*) as transaction_count,
                SUM(total) as total_amount,
                AVG(total) as avg_amount
            FROM {self.schema}.recon_worldline_payments
            WHERE paydate >= CURRENT_DATE - INTERVAL '%s days'
              AND country IS NOT NULL AND country != ''
            GROUP BY country
            ORDER BY transaction_count DESC
        """
        return self.execute_query(query, (days,))
    
    def get_payment_details(self, payment_id: str, paydate: str):
        """Get full details of a single payment by ID and paydate"""
        query = f"""
            SELECT *
            FROM {self.schema}.recon_worldline_payments
            WHERE id = %s AND paydate = %s
            LIMIT 1
        """
        result = self.execute_query(query, (payment_id, paydate))
        return result[0] if result else None
    
    def search_payments(self, search_term: str, limit=50):
        """Search payments by multiple fields"""
        query = f"""
            SELECT 
                id, ref, "order", status, paydate, facname1, country,
                total, cur, brand, merchref, owner
            FROM {self.schema}.recon_worldline_payments
            WHERE 
                id::text ILIKE %s OR
                ref::text ILIKE %s OR
                "order"::text ILIKE %s OR
                facname1::text ILIKE %s OR
                owner::text ILIKE %s OR
                merchref::text ILIKE %s OR
                batchref::text ILIKE %s
            ORDER BY paydate DESC
            LIMIT %s
        """
        search_pattern = f"%{search_term}%"
        params = (search_pattern, search_pattern, search_pattern, 
                 search_pattern, search_pattern, search_pattern, 
                 search_pattern, limit)
        
        return self.execute_query(query, params)
    
    # =====================================================
    # RECONCILIATION QUERIES
    # =====================================================
    
    def get_unmatched_worldline_payments(self, start_date, end_date):
        """Get Worldline payments without matches"""
        query = f"""
            SELECT 
                wp.id, wp.ref, wp.paydate, wp.total, wp.brand, wp.merchref, wp.owner
            FROM {self.schema}.recon_worldline_payments wp
            LEFT JOIN {self.schema}.recon_reconciliation_matches rm 
                ON rm.source_a_record_id = wp.id 
                AND rm.source_a_id = (SELECT source_id FROM {self.schema}.recon_data_sources WHERE source_name = 'Worldline')
            WHERE wp.paydate BETWEEN %s AND %s
              AND rm.match_id IS NULL
            ORDER BY wp.paydate DESC
        """
        return self.execute_query(query, (start_date, end_date))
    
    def get_reconciliation_exceptions(self, status=None, exception_type=None, limit=100):
        """Get reconciliation exceptions"""
        conditions = []
        params = []
        
        if status:
            conditions.append("status = %s")
            params.append(status)
        
        if exception_type:
            conditions.append("exception_type = %s")
            params.append(exception_type)
        
        where_clause = "WHERE " + " AND ".join(conditions) if conditions else ""
        
        query = f"""
            SELECT
                re.*,
                ds.source_name,
                ds.source_type
            FROM {self.schema}.recon_reconciliation_exceptions re
            JOIN {self.schema}.recon_data_sources ds ON ds.source_id = re.source_id
            {where_clause}
            ORDER BY re.created_at DESC
            LIMIT %s
        """
        params.append(limit)
        
        return self.execute_query(query, tuple(params))
    
    def get_reconciliation_summary(self, start_date, end_date):
        """Get reconciliation summary statistics"""
        query = f"""
            SELECT
                (SELECT COUNT(*) FROM {self.schema}.recon_worldline_payments 
                 WHERE paydate BETWEEN %s AND %s) as total_worldline,
                (SELECT COUNT(*) FROM {self.schema}.recon_reconciliation_matches 
                 WHERE matched_at BETWEEN %s AND %s) as total_matched,
                (SELECT COUNT(*) FROM {self.schema}.recon_reconciliation_exceptions 
                 WHERE exception_date BETWEEN %s AND %s AND status = 'OPEN') as open_exceptions,
                (SELECT COUNT(*) FROM {self.schema}.recon_reconciliation_exceptions 
                 WHERE exception_date BETWEEN %s AND %s AND status = 'RESOLVED') as resolved_exceptions
        """
        params = (start_date, end_date, start_date, end_date, 
                 start_date, end_date, start_date, end_date)
        return self.execute_query(query, params)[0]
    
    # =====================================================
    # IMPORT LOG QUERIES
    # =====================================================
    
    def get_import_history(self, limit=50):
        """Get file import history"""
        query = f"""
            SELECT
                fil.*,
                ds.source_name
            FROM {self.schema}.recon_file_import_log fil
            JOIN {self.schema}.recon_data_sources ds ON ds.source_id = fil.source_id
            ORDER BY fil.started_at DESC
            LIMIT %s
        """
        return self.execute_query(query, (limit,))
    
    def get_import_stats(self):
        """Get import statistics"""
        query = f"""
            SELECT
                COUNT(*) as total_imports,
                SUM(records_imported) as total_records_imported,
                SUM(records_failed) as total_records_failed,
                SUM(records_duplicate) as total_duplicates,
                COUNT(*) FILTER (WHERE import_status = 'SUCCESS') as successful_imports,
                COUNT(*) FILTER (WHERE import_status = 'FAILED') as failed_imports,
                MAX(completed_at) as last_import
            FROM {self.schema}.recon_file_import_log
        """
        return self.execute_query(query)[0]
    
    # =====================================================
    # DATA SOURCES & RULES
    # =====================================================
    
    def get_data_sources(self):
        """Get all data sources"""
        query = f"""
            SELECT * FROM {self.schema}.recon_data_sources
            WHERE is_active = TRUE
            ORDER BY source_name
        """
        return self.execute_query(query)
    
    def get_reconciliation_rules(self):
        """Get all reconciliation rules"""
        query = f"""
            SELECT
                rr.*,
                ds_a.source_name as source_a_name,
                ds_b.source_name as source_b_name
            FROM {self.schema}.recon_reconciliation_rules rr
            LEFT JOIN {self.schema}.recon_data_sources ds_a ON ds_a.source_id = rr.source_a_id
            LEFT JOIN {self.schema}.recon_data_sources ds_b ON ds_b.source_id = rr.source_b_id
            WHERE rr.is_active = TRUE
            ORDER BY rr.rule_name
        """
        return self.execute_query(query)
    
    # =====================================================
    # PARTITION MANAGEMENT
    # =====================================================
    
    def get_partition_info(self):
        """Get information about partitions"""
        query = f"""
            SELECT 
                tablename,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
            FROM pg_tables
            WHERE schemaname = %s
              AND tablename LIKE 'recon_worldline_payments_%%'
              AND tablename != 'recon_worldline_payments_archive'
            ORDER BY tablename DESC
        """
        return self.execute_query(query, (self.schema,))
    
    def get_data_date_range(self):
        """Get the date range of data in the database"""
        query = f"""
            SELECT
                MIN(paydate) as earliest_date,
                MAX(paydate) as latest_date,
                COUNT(DISTINCT paydate) as unique_dates
            FROM {self.schema}.recon_worldline_payments
        """
        return self.execute_query(query)[0]


# Singleton instance
db = Database()
