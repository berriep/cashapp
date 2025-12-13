"""
Test database connection
"""
from config.config import Config
from database import db

def test_connection():
    """Test database connection and schema"""
    print("Testing database connection...")
    print(f"Host: {Config.DB_HOST}")
    print(f"Database: {Config.DB_NAME}")
    print(f"Schema: {Config.DB_SCHEMA}")
    print("-" * 50)
    
    try:
        # Test connection
        conn = db.connect()
        print("✓ Connection successful")
        
        # Test schema exists
        result = db.execute_query(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name = %s",
            (Config.DB_SCHEMA,)
        )
        if result:
            print(f"✓ Schema '{Config.DB_SCHEMA}' exists")
        else:
            print(f"✗ Schema '{Config.DB_SCHEMA}' does not exist")
            return False
        
        # Test tables exist
        result = db.execute_query(
            f"""
            SELECT table_name FROM information_schema.tables 
            WHERE table_schema = %s 
            ORDER BY table_name
            """,
            (Config.DB_SCHEMA,)
        )
        
        if result:
            print(f"\n✓ Found {len(result)} tables:")
            for row in result:
                print(f"  - {row['table_name']}")
        else:
            print("✗ No tables found in schema")
            return False
        
        # Test partitions
        result = db.execute_query(
            """
            SELECT tablename FROM pg_tables 
            WHERE schemaname = %s 
            AND tablename LIKE 'worldline_payments_%%'
            ORDER BY tablename DESC
            LIMIT 5
            """,
            (Config.DB_SCHEMA,)
        )
        
        if result:
            print(f"\n✓ Found {len(result)} partitions (showing first 5):")
            for row in result:
                print(f"  - {row['tablename']}")
        
        print("\n✓ All tests passed!")
        return True
        
    except Exception as e:
        print(f"\n✗ Error: {e}")
        return False
    finally:
        db.close()

if __name__ == '__main__':
    test_connection()
