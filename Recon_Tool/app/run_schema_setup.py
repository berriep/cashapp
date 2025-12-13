"""
Execute database schema setup scripts
"""
import psycopg2
from config.config import Config

def run_sql_file(filepath, description):
    """Execute SQL file"""
    print(f"\n{'='*60}")
    print(f"Executing: {description}")
    print(f"File: {filepath}")
    print('='*60)
    
    try:
        # Read SQL file
        with open(filepath, 'r', encoding='utf-8') as f:
            sql = f.read()
        
        # Connect and execute
        conn = psycopg2.connect(Config.get_db_connection_string())
        conn.autocommit = True  # Changed to autocommit for DDL statements
        
        try:
            with conn.cursor() as cur:
                cur.execute(sql)
            print(f"✓ {description} completed successfully")
            return True
            
        except Exception as e:
            conn.rollback()
            print(f"✗ Error executing {description}:")
            print(f"  {str(e)}")
            return False
            
        finally:
            conn.close()
            
    except FileNotFoundError:
        print(f"✗ File not found: {filepath}")
        return False
    except Exception as e:
        print(f"✗ Error: {str(e)}")
        return False

def main():
    """Run all schema setup scripts"""
    print("Database Schema Setup")
    print(f"Target: {Config.DB_HOST}/{Config.DB_NAME}")
    print(f"Schema: {Config.DB_SCHEMA}")
    
    scripts = [
        ("../database/01_create_schema.sql", "Main schema (tables, functions, partitions)"),
        ("../database/02_create_users_table.sql", "Users table for authentication"),
        ("../database/03_create_partitions.sql", "Create initial partitions (30 months)")
    ]
    
    success_count = 0
    for filepath, description in scripts:
        if run_sql_file(filepath, description):
            success_count += 1
    
    print(f"\n{'='*60}")
    print(f"Setup complete: {success_count}/{len(scripts)} scripts executed successfully")
    print('='*60)
    
    if success_count == len(scripts):
        print("\n✓ Database schema is ready!")
        print("\nNext steps:")
        print("1. Run: py create_admin_user.py")
        print("2. Run: py test_db.py (to verify setup)")
        print("3. Run: py main.py (to start the application)")
    else:
        print("\n✗ Some scripts failed. Please check the errors above.")

if __name__ == '__main__':
    main()
