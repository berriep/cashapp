"""Update admin template references"""
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
        'dashboard', 'admin', 'admin_users', 'admin_create_user', 'admin_edit_user',
        'admin_delete_user', 'admin_change_password'
    ]
    
    for route in bai_routes:
        content = re.sub(
            rf"url_for\('{route}'",
            f"url_for('bai.{route}'",
            content
        )
    
    # Update shared routes
    shared_routes = ['login', 'logout']
    for route in shared_routes:
        content = re.sub(
            rf"url_for\('{route}'",
            f"url_for('shared.{route}'",
            content
        )
    
    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

# Process admin templates
admin_dir = 'app/bai/templates/admin'
updated_count = 0

for filename in os.listdir(admin_dir):
    if filename.endswith('.html'):
        filepath = os.path.join(admin_dir, filename)
        if update_template(filepath):
            print(f'✅ Updated: admin/{filename}')
            updated_count += 1

print(f'\n✅ Updated {updated_count} admin template files!')
