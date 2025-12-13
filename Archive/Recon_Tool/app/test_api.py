import sys
sys.path.insert(0, '.')

from database import db
from datetime import date

# Test get_payment_details
payment_id = "9118602260/0"
paydate = "2025-12-01"  # ISO format

print(f"Testing get_payment_details('{payment_id}', '{paydate}')")

try:
    result = db.get_payment_details(payment_id, paydate)
    if result:
        print(f"✓ Found payment!")
        print(f"  ID: {result['id']}")
        print(f"  REF: {result['ref']}")
        print(f"  Brand: {result.get('brand')}")
        print(f"  Total fields: {len(result)}")
    else:
        print("✗ Payment not found")
        
    # Also try with date object
    print(f"\nTrying with date object...")
    result2 = db.get_payment_details(payment_id, date(2025, 12, 1))
    if result2:
        print(f"✓ Found with date object!")
    else:
        print("✗ Not found with date object")
        
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
finally:
    db.close()
