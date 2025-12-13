"""Fix Recon templates to use recon/base.html instead of base.html"""
import os

# Templates folder
templates_dir = r"C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates"

# Files to update
files_to_update = [
    "404.html",
    "500.html", 
    "dashboard.html",
    "import.html",
    "import_history.html",
    "login.html",
    "payments.html",
    "reconciliation.html",
    "reports.html",
    "settings.html"
]

for filename in files_to_update:
    filepath = os.path.join(templates_dir, filename)
    
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace extends base.html with recon/base.html
    updated = content.replace('{% extends "base.html" %}', '{% extends "recon/base.html" %}')
    
    if updated != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(updated)
        print(f"✓ Updated {filename}")
    else:
        print(f"- Skipped {filename} (no changes needed)")

print("\nDone!")
