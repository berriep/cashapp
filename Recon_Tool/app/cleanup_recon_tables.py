"""
Clean up any existing recon_ tables before fresh install
"""
import psycopg2
from config.config import Config

def cleanup():
    """Drop all recon_ tables and functions"""
    print("Cleaning up existing recon_ objects...")
    
    conn = psycopg2.connect(Config.get_db_connection_string())
    conn.autocommit = True
    
    try:
        with conn.cursor() as cur:
            # Drop functions
            print("Dropping functions...")
            cur.execute("DROP FUNCTION IF EXISTS rpa_data.recon_create_worldline_partition(TIMESTAMP) CASCADE")
            cur.execute("DROP FUNCTION IF EXISTS rpa_data.recon_archive_old_partitions(INTEGER) CASCADE")
            
            # Get all recon tables
            cur.execute("""
                SELECT tablename FROM pg_tables 
                WHERE schemaname = 'rpa_data' 
                AND tablename LIKE 'recon_%'
                ORDER BY tablename
            """)
            tables = cur.fetchall()
            
            # Drop tables
            print(f"Dropping {len(tables)} tables...")
            for table in tables:
                table_name = table[0]
                print(f"  Dropping {table_name}...")
                cur.execute(f"DROP TABLE IF EXISTS rpa_data.{table_name} CASCADE")
            
        print("✓ Cleanup complete")
        return True
        
    except Exception as e:
        print(f"✗ Error: {e}")
        return False
    finally:
        conn.close()

if __name__ == '__main__':
    cleanup()
