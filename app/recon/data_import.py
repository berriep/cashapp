import csv
import os
import psycopg2
from psycopg2.extras import execute_batch
from datetime import datetime
from config.config import Config
from typing import Dict, List, Tuple
import re

class WorldlineCSVImporter:
    """Import Worldline CSV files into PostgreSQL database"""
    
    def __init__(self):
        self.conn = None
        self.schema = 'rpa_data'  # Recon tables are in rpa_data schema
        
    def connect(self):
        """Establish database connection to Recon database"""
        if self.conn is None or self.conn.closed:
            self.conn = psycopg2.connect(Config.get_db_connection_string('recon'))
        return self.conn
    
    def close(self):
        """Close database connection"""
        if self.conn and not self.conn.closed:
            self.conn.close()
    
    @staticmethod
    def parse_european_decimal(value: str) -> float:
        """Convert European decimal format (comma) to float"""
        if not value or value.strip() == '':
            return None
        try:
            # Replace comma with dot for decimal
            cleaned = value.replace(',', '.')
            return float(cleaned)
        except ValueError:
            return None
    
    @staticmethod
    def parse_date(date_str: str) -> str:
        """Parse various date formats to YYYY-MM-DD"""
        if not date_str or date_str.strip() == '':
            return None
        
        try:
            # Try DD/MM/YYYY format first
            if '/' in date_str:
                parts = date_str.split('/')
                if len(parts) == 3:
                    day, month, year = parts
                    # Handle 2-digit year
                    if len(year) == 2:
                        year = '20' + year if int(year) < 50 else '19' + year
                    return f"{year}-{month.zfill(2)}-{day.zfill(2)}"
            
            # Try other formats
            for fmt in ['%d/%m/%Y', '%Y-%m-%d', '%d-%m-%Y']:
                try:
                    dt = datetime.strptime(date_str.strip(), fmt)
                    return dt.strftime('%Y-%m-%d')
                except ValueError:
                    continue
            
            return None
        except Exception:
            return None
    
    @staticmethod
    def parse_datetime(datetime_str: str) -> str:
        """Parse datetime formats to YYYY-MM-DD HH:MM:SS"""
        if not datetime_str or datetime_str.strip() == '':
            return None
        
        try:
            # Try DD/MM/YYYY HH:MM:SS format
            for fmt in ['%d/%m/%Y %H:%M:%S', '%d/%m/%Y %H:%M', '%Y-%m-%d %H:%M:%S']:
                try:
                    dt = datetime.strptime(datetime_str.strip(), fmt)
                    return dt.strftime('%Y-%m-%d %H:%M:%S')
                except ValueError:
                    continue
            
            # If no time component, treat as date
            return WorldlineCSVImporter.parse_date(datetime_str)
        except Exception:
            return None
    
    def read_csv(self, filepath: str, encoding: str = 'utf-8') -> Tuple[List[Dict], List[str]]:
        """Read Worldline CSV file and return records"""
        records = []
        errors = []
        
        try:
            with open(filepath, 'r', encoding=encoding) as csvfile:
                # Worldline uses semicolon as delimiter
                reader = csv.DictReader(csvfile, delimiter=';')
                
                for row_num, row in enumerate(reader, start=2):  # Start at 2 (header is row 1)
                    try:
                        # Parse and clean data
                        record = {
                            'id': row.get('Id', '').strip(),
                            'ref': row.get('REF', '').strip(),
                            'order': row.get('ORDER', '').strip(),
                            'status': row.get('STATUS', '').strip(),
                            'lib': row.get('LIB', '').strip(),
                            'accept': row.get('ACCEPT', '').strip(),
                            'ncid': row.get('NCID', '').strip(),
                            'ncster': row.get('NCSTER', '').strip(),
                            'paydate': self.parse_date(row.get('PAYDATE', '')),
                            'cie': row.get('CIE', '').strip(),
                            'facname1': row.get('FACNAME1', '').strip(),
                            'country': row.get('COUNTRY', '').strip(),
                            'total': self.parse_european_decimal(row.get('TOTAL', '')),
                            'cur': row.get('CUR', '').strip(),
                            'method': row.get('METHOD', '').strip(),
                            'brand': row.get('BRAND', '').strip(),
                            'card': row.get('CARD', '').strip(),
                            'expdate': row.get('EXPDATE', '').strip(),
                            'uid': row.get('UID', '').strip(),
                            'struct': row.get('STRUCT', '').strip(),
                            'fileid': row.get('FILEID', '').strip(),
                            'action': row.get('ACTION', '').strip(),
                            'ticket': row.get('TICKET', '').strip(),
                            'desc': row.get('DESC', '').strip(),
                            'ship': self.parse_european_decimal(row.get('SHIP', '')),
                            'tax': self.parse_european_decimal(row.get('TAX', '')),
                            'userid': row.get('USERID', '').strip(),
                            'merchref': row.get('MERCHREF', '').strip(),
                            'refid': row.get('REFID', '').strip(),
                            'refkind': row.get('REFKIND', '').strip(),
                            'eci': row.get('ECI', '').strip(),
                            'cccty': row.get('CCCTY', '').strip(),
                            'ipcty': row.get('IPCTY', '').strip(),
                            'cvccheck': row.get('CVCCHECK', '').strip(),
                            'aavcheck': row.get('AAVCHECK', '').strip(),
                            'vc': row.get('VC', '').strip(),
                            'batchref': row.get('BATCHREF', '').strip(),
                            'owner': row.get('OWNER', '').strip(),
                            'alias': row.get('ALIAS', '').strip(),
                            'fraud_type': row.get('FRAUD_TYPE', '').strip(),
                            'bincard': row.get('BINCARD', '').strip(),
                            'rec_ipaddr': row.get('REC_IPADDR', '').strip(),
                            'paydatetime': self.parse_datetime(row.get('PAYDATETIME', '')),
                            'orderdatetime': self.parse_datetime(row.get('ORDERDATETIME', '')),
                            'subbrand': row.get('SUBBRAND', '').strip(),
                            'source_file': os.path.basename(filepath)
                        }
                        
                        # Validate required fields
                        if not record['id']:
                            errors.append(f"Row {row_num}: Missing required field 'Id'")
                            continue
                        
                        if not record['paydate']:
                            errors.append(f"Row {row_num}: Invalid or missing PAYDATE for Id={record['id']}")
                            continue
                        
                        records.append(record)
                        
                    except Exception as e:
                        errors.append(f"Row {row_num}: Error parsing row - {str(e)}")
                        continue
        
        except Exception as e:
            errors.append(f"Error reading CSV file: {str(e)}")
        
        return records, errors
    
    def ensure_partition_exists(self, paydate: str):
        """Ensure partition exists for the given payment date"""
        conn = self.connect()
        try:
            with conn.cursor() as cur:
                cur.execute(
                    f"SELECT {self.schema}.recon_create_worldline_partition(%s)",
                    (paydate,)
                )
                conn.commit()
        except Exception as e:
            conn.rollback()
            print(f"Warning: Could not create partition for {paydate}: {e}")
    
    def import_records(self, records: List[Dict], source_id: int = None, source_file: str = None, batch_size: int = 1000) -> Dict:
        """Import records into database with duplicate detection"""
        import time
        start_time = time.time()
        
        conn = self.connect()
        imported = 0
        duplicates = 0
        failed = 0
        
        # Add source_file to all records
        for record in records:
            record['source_file'] = source_file
        
        insert_query = f"""
            INSERT INTO {self.schema}.recon_worldline_payments (
                id, ref, "order", status, lib, accept, ncid, ncster, paydate,
                cie, facname1, country, total, cur, method, brand, card, expdate,
                uid, struct, fileid, action, ticket, "desc", ship, tax, userid,
                merchref, refid, refkind, eci, cccty, ipcty, cvccheck, aavcheck,
                vc, batchref, owner, alias, fraud_type, bincard, rec_ipaddr,
                paydatetime, orderdatetime, subbrand, source_file
            ) VALUES (
                %(id)s, %(ref)s, %(order)s, %(status)s, %(lib)s, %(accept)s, %(ncid)s, 
                %(ncster)s, %(paydate)s, %(cie)s, %(facname1)s, %(country)s, %(total)s, 
                %(cur)s, %(method)s, %(brand)s, %(card)s, %(expdate)s, %(uid)s, %(struct)s,
                %(fileid)s, %(action)s, %(ticket)s, %(desc)s, %(ship)s, %(tax)s, %(userid)s,
                %(merchref)s, %(refid)s, %(refkind)s, %(eci)s, %(cccty)s, %(ipcty)s, 
                %(cvccheck)s, %(aavcheck)s, %(vc)s, %(batchref)s, %(owner)s, %(alias)s,
                %(fraud_type)s, %(bincard)s, %(rec_ipaddr)s, %(paydatetime)s, 
                %(orderdatetime)s, %(subbrand)s, %(source_file)s
            )
            ON CONFLICT (id, paydate) DO NOTHING
        """
        
        try:
            # Ensure partitions exist for all unique dates
            unique_dates = set(r['paydate'] for r in records if r.get('paydate'))
            for paydate in unique_dates:
                self.ensure_partition_exists(paydate)
            
            # Batch insert
            with conn.cursor() as cur:
                for i in range(0, len(records), batch_size):
                    batch = records[i:i + batch_size]
                    
                    try:
                        execute_batch(cur, insert_query, batch)
                        conn.commit()
                        
                        # Count how many were actually inserted (not duplicates)
                        batch_imported = cur.rowcount
                        imported += batch_imported
                        duplicates += len(batch) - batch_imported
                        
                        print(f"Batch {i//batch_size + 1}: {batch_imported} imported, {len(batch) - batch_imported} duplicates")
                        
                    except Exception as e:
                        conn.rollback()
                        error_msg = f"Batch {i//batch_size + 1} failed: {str(e)}"
                        print(error_msg)
                        failed += len(batch)
                        continue
        
        except Exception as e:
            conn.rollback()
            print(f"Import failed: {str(e)}")
        
        duration = time.time() - start_time
        return {
            'imported': imported,
            'duplicates': duplicates,
            'failed': failed,
            'duration': duration
        }
    
    def log_import(self, filename: str, filesize: int, total_records: int, 
                   imported: int, failed: int, duplicates: int, status: str, 
                   error_msg: str = None, username: str = None):
        """Log import details to file_import_log table"""
        conn = self.connect()
        
        query = f"""
            INSERT INTO {self.schema}.recon_file_import_log (
                source_id, filename, file_size_bytes, records_total, records_imported,
                records_failed, records_duplicate, import_status, error_message,
                completed_at, imported_by
            ) VALUES (
                (SELECT source_id FROM {self.schema}.recon_data_sources WHERE source_name = 'Worldline'),
                %s, %s, %s, %s, %s, %s, %s, %s, CURRENT_TIMESTAMP, %s
            )
        """
        
        try:
            with conn.cursor() as cur:
                cur.execute(query, (
                    filename, filesize, total_records, imported, failed, 
                    duplicates, status, error_msg, username
                ))
                conn.commit()
        except Exception as e:
            conn.rollback()
            print(f"Warning: Could not log import: {e}")
    
    def import_file(self, filepath: str, username: str = None) -> Dict:
        """Main import function"""
        print(f"Starting import of {filepath}...")
        
        # Get file size
        filesize = os.path.getsize(filepath)
        filename = os.path.basename(filepath)
        
        # Read CSV
        print("Reading CSV file...")
        records, read_errors = self.read_csv(filepath)
        
        if not records:
            error_msg = "No valid records found in file"
            self.log_import(filename, filesize, 0, 0, len(read_errors), 0, 'FAILED', error_msg, username)
            return {
                'status': 'FAILED',
                'total_records': 0,
                'imported': 0,
                'failed': len(read_errors),
                'duplicates': 0,
                'errors': read_errors
            }
        
        print(f"Found {len(records)} valid records")
        
        # Import records
        print("Importing records to database...")
        imported, duplicates, import_errors = self.import_records(records)
        
        failed = len(read_errors) + len(import_errors)
        all_errors = read_errors + import_errors
        
        # Determine status
        if imported == 0 and failed > 0:
            status = 'FAILED'
        elif failed > 0:
            status = 'PARTIAL'
        else:
            status = 'SUCCESS'
        
        # Log import
        error_summary = '; '.join(all_errors[:5]) if all_errors else None
        self.log_import(filename, filesize, len(records), imported, failed, 
                       duplicates, status, error_summary, username)
        
        print(f"Import complete: {imported} imported, {duplicates} duplicates, {failed} failed")
        
        return {
            'status': status,
            'total_records': len(records),
            'imported': imported,
            'failed': failed,
            'duplicates': duplicates,
            'errors': all_errors
        }


# Example usage
if __name__ == '__main__':
    importer = WorldlineCSVImporter()
    
    # Import a file
    result = importer.import_file('path/to/worldline_payments.csv', username='admin')
    
    print(f"\nImport Summary:")
    print(f"Status: {result['status']}")
    print(f"Total Records: {result['total_records']}")
    print(f"Imported: {result['imported']}")
    print(f"Duplicates: {result['duplicates']}")
    print(f"Failed: {result['failed']}")
    
    if result['errors']:
        print(f"\nErrors ({len(result['errors'])}):")
        for error in result['errors'][:10]:  # Show first 10 errors
            print(f"  - {error}")
    
    importer.close()
