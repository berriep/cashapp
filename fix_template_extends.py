import os
import re

# BAI templates directory
bai_templates_dir = r"C:\Users\bpeijmen\Documents\Code\CashApp\app\bai\templates"

# Templates to update (change extends from "shared/base.html" to "base.html")
template_files = [
    "404.html",
    "500.html", 
    "balances.html",
    "bank_statements.html",
    "dashboard.html",
    "reconciliation_report.html",
    "reports.html",
    "transactions.html",
    "transaction_details.html",
    "admin/create_user.html",
    "admin/edit_user.html",
    "admin/users.html"
]

for template_file in template_files:
    file_path = os.path.join(bai_templates_dir, template_file)
    
    if os.path.exists(file_path):
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Change {% extends "shared/base.html" %} to {% extends "base.html" %}
        updated = content.replace('{% extends "shared/base.html" %}', '{% extends "base.html" %}')
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(updated)
        
        print(f"✅ Updated: {template_file}")
    else:
        print(f"❌ Not found: {template_file}")

print("\n✅ All BAI templates updated to use base.html (without shared/ prefix)")
