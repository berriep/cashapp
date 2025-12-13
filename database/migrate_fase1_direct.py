"""
Direct database migration script bypassing execute_query
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

import psycopg2
from psycopg2.extras import RealDictCursor
from config.config import Config

print("\n" + "="*80)
print("CashApp Database Migration - Fase 1")
print("Hernoem: bai_monitor_users → cashapp_users")
print("="*80 + "\n")

# Connect directly
conn_string = Config.get_db_connection_string('shared')
print(f"Connecting to: {Config.SHARED_DB_HOST}/{Config.SHARED_DB_NAME}")

try:
    conn = psycopg2.connect(conn_string, cursor_factory=RealDictCursor)
    conn.autocommit = True  # For DDL statements
    
    with conn.cursor() as cur:
        # Step 1: Rename table
        print("\nStep 1: Renaming table...")
        cur.execute("ALTER TABLE rpa_data.bai_monitor_users RENAME TO cashapp_users")
        print("✓ Table renamed successfully")
        
        # Step 2: Verify
        print("\nStep 2: Verifying table exists...")
        cur.execute("SELECT tablename, schemaname FROM pg_tables WHERE schemaname = 'rpa_data' AND tablename = 'cashapp_users'")
        result = cur.fetchall()
        if result:
            print(f"✓ Table found: {result[0]['tablename']} in schema {result[0]['schemaname']}")
        else:
            print("✗ Table not found!")
            sys.exit(1)
        
        # Step 3: Show columns
        print("\nStep 3: Showing table structure...")
        cur.execute("""
            SELECT column_name, data_type, is_nullable, column_default 
            FROM information_schema.columns 
            WHERE table_schema = 'rpa_data' AND table_name = 'cashapp_users' 
            ORDER BY ordinal_position
        """)
        columns = cur.fetchall()
        print(f"✓ Table has {len(columns)} columns:")
        for col in columns:
            nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
            default = f" DEFAULT {col['column_default']}" if col['column_default'] else ""
            print(f"  - {col['column_name']:<30} {col['data_type']:<20} {nullable}{default}")
    
    conn.close()
    
    print("\n" + "="*80)
    print("✅ Fase 1 Migration completed successfully!")
    print("="*80 + "\n")
    print("Next step: Run Fase 2 migration to add module permissions")
    print("Command: py migrate_fase2.py\n")
    
except psycopg2.errors.UndefinedTable as e:
    print(f"\n✗ Table 'bai_monitor_users' not found. It may have already been renamed.")
    print(f"   Error: {e}\n")
    sys.exit(1)
except Exception as e:
    print(f"\n✗ Error: {e}\n")
    sys.exit(1)
