"""
Database Migration Runner for CashApp
Executes SQL migration scripts against the database
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from app.shared.database import shared_db
from config.config import Config

def run_migration(migration_file):
    """Execute a SQL migration file"""
    print(f"\n{'='*80}")
    print(f"Running migration: {migration_file}")
    print(f"{'='*80}\n")
    
    # Read SQL file
    sql_path = os.path.join(os.path.dirname(__file__), migration_file)
    with open(sql_path, 'r', encoding='utf-8') as f:
        sql_content = f.read()
    
    # Split into individual statements (skip comments and empty lines)
    statements = []
    current_statement = []
    
    for line in sql_content.split('\n'):
        line = line.strip()
        
        # Skip comments and empty lines
        if line.startswith('--') or not line:
            continue
        
        current_statement.append(line)
        
        # Execute when we hit a semicolon
        if line.endswith(';'):
            statement = ' '.join(current_statement)
            statements.append(statement)
            current_statement = []
    
    # Execute each statement
    for i, statement in enumerate(statements, 1):
        try:
            print(f"Executing statement {i}/{len(statements)}...")
            
            # Check if it's a SELECT query
            is_select = statement.strip().upper().startswith('SELECT')
            
            if is_select:
                result = shared_db.execute_query(statement)
                if result:
                    print(f"✓ Query returned {len(result)} rows")
                    for row in result:
                        print(f"  {row}")
                else:
                    print("✓ Query executed (no results)")
            else:
                shared_db.execute_query(statement)
                print("✓ Statement executed successfully")
                
        except Exception as e:
            print(f"✗ Error executing statement: {e}")
            print(f"  Statement: {statement[:100]}...")
            return False
    
    print(f"\n{'='*80}")
    print(f"Migration completed successfully!")
    print(f"{'='*80}\n")
    return True

if __name__ == "__main__":
    print("\n" + "="*80)
    print("CashApp Database Migration Tool")
    print("="*80)
    print(f"Database: {Config.SHARED_DB_HOST}/{Config.SHARED_DB_NAME}")
    print("="*80 + "\n")
    
    if len(sys.argv) < 2:
        print("Available migrations:")
        print("  1. migration_001_rename_users_table.sql")
        print("  2. migration_002_add_module_permissions.sql")
        print("\nUsage: python run_migration.py <migration_file>")
        print("Example: python run_migration.py migration_001_rename_users_table.sql")
        sys.exit(1)
    
    migration_file = sys.argv[1]
    
    # Confirm before running
    response = input(f"\nRun migration '{migration_file}' on database '{Config.SHARED_DB_NAME}'? (yes/no): ")
    if response.lower() != 'yes':
        print("Migration cancelled.")
        sys.exit(0)
    
    success = run_migration(migration_file)
    sys.exit(0 if success else 1)



