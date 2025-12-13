"""
Direct CSV import script - bypasses web upload file size limits
Usage: py import_csv_direct.py <path_to_csv_file>
"""
import sys
import os
from datetime import datetime
from config.config import Config
from data_import import WorldlineCSVImporter
from database import db

def import_csv_file(filepath):
    """Import CSV file directly into database"""
    
    if not os.path.exists(filepath):
        print(f"❌ File not found: {filepath}")
        return False
    
    print("=" * 60)
    print("Worldline CSV Direct Import")
    print("=" * 60)
    print(f"File: {filepath}")
    print(f"Size: {os.path.getsize(filepath) / (1024*1024):.2f} MB")
    print(f"Database: {Config.DB_HOST}/{Config.DB_NAME}")
    print(f"Schema: {Config.DB_SCHEMA}")
    print("=" * 60)
    
    # Get or create Worldline data source
    try:
        sources = db.execute_query(
            f"SELECT source_id FROM {Config.DB_SCHEMA}.recon_data_sources WHERE source_name = 'Worldline'"
        )
        if sources:
            source_id = sources[0]['source_id']
        else:
            # Create Worldline data source
            result = db.execute_query(
                f"""
                INSERT INTO {Config.DB_SCHEMA}.recon_data_sources (source_name, source_type, description)
                VALUES ('Worldline', 'CSV', 'Worldline payment provider data')
                RETURNING source_id
                """
            )
            source_id = result[0]['source_id']
            print(f"✓ Created data source 'Worldline' (ID: {source_id})")
    except Exception as e:
        print(f"❌ Error getting/creating data source: {e}")
        return False
    
    # Initialize importer
    importer = WorldlineCSVImporter()
    
    try:
        # Read and parse CSV
        print("\n📖 Reading CSV file...")
        records, errors = importer.read_csv(filepath)
        
        if errors:
            print(f"⚠ Found {len(errors)} parsing errors:")
            for error in errors[:10]:  # Show first 10 errors
                print(f"  - {error}")
        
        print(f"✓ Found {len(records)} records in CSV")
        
        # Import records
        print("\n💾 Importing records to database...")
        stats = importer.import_records(records, source_id, os.path.basename(filepath))
        
        print("\n" + "=" * 60)
        print("Import Complete!")
        print("=" * 60)
        print(f"✓ Successfully imported: {stats['imported']}")
        print(f"⚠ Duplicates skipped:   {stats['duplicates']}")
        print(f"❌ Failed:              {stats['failed']}")
        print(f"⏱ Duration:             {stats['duration']:.2f} seconds")
        print("=" * 60)
        
        return True
        
    except Exception as e:
        print(f"\n❌ Import failed: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: py import_csv_direct.py <path_to_csv_file>")
        print("\nExample:")
        print("  py import_csv_direct.py ..\\Documentation\\Worldline_Payments_2025.csv")
        sys.exit(1)
    
    csv_file = sys.argv[1]
    success = import_csv_file(csv_file)
    sys.exit(0 if success else 1)
