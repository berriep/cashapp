"""Update template references for blueprint structure"""
import os
import re

def update_template(filepath):
    """Update a single template file"""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original = content
    
    # Update base template extends
    content = content.replace('{% extends "base.html" %}', '{% extends "shared/base.html" %}')
    
    # Update url_for for BAI routes
    bai_routes = [
        'dashboard', 'transaction_details', 'balances', 'bank_statements', 
        'bank_statement_pdf', 'reports', 'reconciliation_report',
        'admin', 'admin_users', 'admin_create_user', 'admin_edit_user',
        'admin_delete_user', 'admin_change_password', 'api_transaction_chart'
    ]
    
    for route in bai_routes:
        # Match url_for('route_name' with optional parameters
        content = re.sub(
            rf"url_for\('{route}'",
            f"url_for('bai.{route}'",
            content
        )
    
    # Update shared routes (login, logout)
    shared_routes = ['login', 'logout']
    for route in shared_routes:
        content = re.sub(
            rf"url_for\('{route}'",
            f"url_for('shared.{route}'",
            content
        )
    
    # Only write if changed
    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

# Process all templates in app/bai/templates
template_dir = 'app/bai/templates'
updated_count = 0

for filename in os.listdir(template_dir):
    if filename.endswith('.html'):
        filepath = os.path.join(template_dir, filename)
        if update_template(filepath):
            print(f'✅ Updated: {filename}')
            updated_count += 1
        else:
            print(f'⏭️  Skipped: {filename} (no changes needed)')

print(f'\n✅ Updated {updated_count} template files!')
