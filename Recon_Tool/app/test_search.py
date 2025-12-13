import sys
sys.path.insert(0, '.')

from database import db

# Test search
search_term = "7870217.1764629862994"
print(f"Searching for: {search_term}")

try:
    results = db.search_payments(search_term, limit=10)
    print(f"\nFound {len(results)} results:")
    for r in results:
        print(f"  ID: {r['id']}, REF: {r['ref']}, Date: {r['paydate']}")
    
    # Also test with wildcards
    print(f"\nSearching for partial: 7870217")
    results2 = db.search_payments("7870217", limit=10)
    print(f"Found {len(results2)} results")
    for r in results2[:3]:
        print(f"  ID: {r['id']}, REF: {r['ref']}, Date: {r['paydate']}")
        
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
finally:
    db.close()
