"""Convert all Recon templates from extends to standalone"""
import os
import re

templates_dir = r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates'

# Read base template
with open(os.path.join(templates_dir, 'recon_base.html'), 'r', encoding='utf-8') as f:
    base_template = f.read()

# Templates to convert (skip base templates and already converted)
templates_to_convert = [
    'payments.html',
    'reconciliation.html', 
    'reports.html',
    'import.html',
    'import_history.html',
    'settings.html'
]

for template_name in templates_to_convert:
    template_path = os.path.join(templates_dir, template_name)
    
    if not os.path.exists(template_path):
        print(f"⚠ Skipped {template_name} (not found)")
        continue
    
    # Read the template
    with open(template_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Check if it uses extends
    if '{% extends' not in content:
        print(f"⚠ Skipped {template_name} (already standalone)")
        continue
    
    # Extract title
    title_match = re.search(r'{% block title %}(.*?){% endblock %}', content)
    title = title_match.group(1) if title_match else 'Reconciliation Tool'
    
    # Extract content block
    content_match = re.search(r'{% block content %}(.*?){% endblock %}', content, re.DOTALL)
    content_block = content_match.group(1).strip() if content_match else ''
    
    # Extract scripts block if exists
    scripts_match = re.search(r'{% block scripts %}(.*?){% endblock %}', content, re.DOTALL)
    scripts_block = scripts_match.group(1).strip() if scripts_match else ''
    
    # Build standalone template
    result = base_template
    
    # Replace title
    result = result.replace('{% block title %}Reconciliation Tool{% endblock %}', title)
    
    # Replace content
    result = result.replace('{% block content %}{% endblock %}', content_block)
    
    # Replace scripts
    result = result.replace('{% block scripts %}{% endblock %}', scripts_block)
    
    # Backup original
    backup_path = template_path + '.backup_extends'
    if not os.path.exists(backup_path):
        with open(backup_path, 'w', encoding='utf-8') as f:
            f.write(content)
    
    # Write standalone version
    with open(template_path, 'w', encoding='utf-8') as f:
        f.write(result)
    
    print(f"✓ Converted {template_name}")
    print(f"  Content: {len(content_block)} chars, Scripts: {len(scripts_block)} chars")

print("\nDone! All templates converted to standalone.")
