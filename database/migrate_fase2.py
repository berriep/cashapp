"""
Database Migration - Fase 2
Voeg module permission kolommen toe aan cashapp_users
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

import psycopg2
from psycopg2.extras import RealDictCursor
from config.config import Config

print("\n" + "="*80)
print("CashApp Database Migration - Fase 2")
print("Add Module Permissions: has_bai_access, has_recon_access")
print("="*80 + "\n")

conn_string = Config.get_db_connection_string('shared')
print(f"Connecting to: {Config.SHARED_DB_HOST}/{Config.SHARED_DB_NAME}")

try:
    conn = psycopg2.connect(conn_string, cursor_factory=RealDictCursor)
    conn.autocommit = True
    
    with conn.cursor() as cur:
        # Step 1: Add columns
        print("\nStep 1: Adding permission columns...")
        cur.execute("""
            ALTER TABLE rpa_data.cashapp_users 
            ADD COLUMN IF NOT EXISTS has_bai_access BOOLEAN DEFAULT TRUE,
            ADD COLUMN IF NOT EXISTS has_recon_access BOOLEAN DEFAULT FALSE
        """)
        print("✓ Columns added successfully")
        
        # Step 2: Set defaults for existing users
        print("\nStep 2: Setting default permissions for existing users...")
        cur.execute("""
            UPDATE rpa_data.cashapp_users 
            SET has_bai_access = TRUE
            WHERE has_bai_access IS NULL
        """)
        print("✓ All existing users now have BAI access (backward compatible)")
        
        # Step 3: Give admins full access
        print("\nStep 3: Giving admins full access to all modules...")
        cur.execute("""
            UPDATE rpa_data.cashapp_users 
            SET has_bai_access = TRUE,
                has_recon_access = TRUE
            WHERE is_admin = TRUE
        """)
        admin_count = cur.rowcount
        print(f"✓ Updated {admin_count} admin user(s) with full access")
        
        # Step 4: Verify new columns
        print("\nStep 4: Verifying new columns...")
        cur.execute("""
            SELECT column_name, data_type, column_default
            FROM information_schema.columns
            WHERE table_schema = 'rpa_data' 
            AND table_name = 'cashapp_users'
            AND column_name IN ('has_bai_access', 'has_recon_access')
            ORDER BY ordinal_position
        """)
        columns = cur.fetchall()
        print(f"✓ Verified {len(columns)} new columns:")
        for col in columns:
            print(f"  - {col['column_name']:<30} {col['data_type']:<20} DEFAULT {col['column_default']}")
        
        # Step 5: Show user permissions overview
        print("\nStep 5: Current user permissions overview...")
        cur.execute("""
            SELECT 
                id,
                username,
                is_admin,
                has_bai_access,
                has_recon_access,
                is_active
            FROM rpa_data.cashapp_users
            ORDER BY username
        """)
        users = cur.fetchall()
        print(f"✓ Found {len(users)} user(s):")
        print(f"\n{'ID':<5} {'Username':<20} {'Admin':<8} {'BAI':<8} {'Recon':<8} {'Active':<8}")
        print("-" * 60)
        for user in users:
            admin_str = "✓ YES" if user['is_admin'] else "  no"
            bai_str = "✓ YES" if user['has_bai_access'] else "  no"
            recon_str = "✓ YES" if user['has_recon_access'] else "  no"
            active_str = "✓ YES" if user['is_active'] else "  no"
            print(f"{user['id']:<5} {user['username']:<20} {admin_str:<8} {bai_str:<8} {recon_str:<8} {active_str:<8}")
    
    conn.close()
    
    print("\n" + "="*80)
    print("✅ Fase 2 Migration completed successfully!")
    print("="*80 + "\n")
    print("Module permissions are now active!")
    print("- Admin users have access to all modules")
    print("- Existing users have BAI access (backward compatible)")
    print("- Recon access is disabled by default (grant per user)\n")
    print("Next: Restart Flask app to use new permissions\n")
    
except Exception as e:
    print(f"\n✗ Error: {e}\n")
    import traceback
    traceback.print_exc()
    sys.exit(1)
