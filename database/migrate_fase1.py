"""
Direct SQL execution for migrations
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from app.shared.database import shared_db

print("\n" + "="*80)
print("Fase 1: Hernoem bai_monitor_users naar cashapp_users")
print("="*80 + "\n")

try:
    # Step 1: Rename table
    print("Step 1: Renaming table...")
    shared_db.execute_query("ALTER TABLE rpa_data.bai_monitor_users RENAME TO cashapp_users")
    print("✓ Table renamed successfully\n")
    
    # Step 2: Verify
    print("Step 2: Verifying table exists...")
    result = shared_db.execute_query(
        "SELECT tablename, schemaname FROM pg_tables WHERE schemaname = 'rpa_data' AND tablename = 'cashapp_users'"
    )
    if result:
        print(f"✓ Table found: {result[0]}\n")
    else:
        print("✗ Table not found!\n")
        sys.exit(1)
    
    # Step 3: Show columns
    print("Step 3: Showing table structure...")
    result = shared_db.execute_query(
        """SELECT column_name, data_type, is_nullable, column_default 
        FROM information_schema.columns 
        WHERE table_schema = 'rpa_data' AND table_name = 'cashapp_users' 
        ORDER BY ordinal_position"""
    )
    if result:
        print(f"✓ Table has {len(result)} columns:")
        for row in result:
            print(f"  - {row['column_name']}: {row['data_type']}")
    
    print("\n" + "="*80)
    print("✅ Fase 1 Migration completed successfully!")
    print("="*80 + "\n")
    
except Exception as e:
    print(f"\n✗ Error: {e}\n")
    sys.exit(1)
