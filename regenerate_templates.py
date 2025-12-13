"""Re-convert all templates from backup to pick up base template changes"""
import os
import re

templates_dir = r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates'

# Read base template
with open(os.path.join(templates_dir, 'recon_base.html'), 'r', encoding='utf-8') as f:
    base_template = f.read()

# Templates to reconvert
templates_to_convert = [
    'payments.html',
    'reconciliation.html', 
    'reports.html',
    'import.html',
    'import_history.html',
    'settings.html',
    'recon_dashboard.html'
]

for template_name in templates_to_convert:
    backup_path = os.path.join(templates_dir, template_name + '.backup_extends')
    template_path = os.path.join(templates_dir, template_name)
    
    # Use backup if exists, otherwise use current
    if os.path.exists(backup_path):
        with open(backup_path, 'r', encoding='utf-8') as f:
            content = f.read()
    elif os.path.exists(template_path):
        with open(template_path, 'r', encoding='utf-8') as f:
            content = f.read()
    else:
        print(f"⚠ Skipped {template_name} (not found)")
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
    
    # Write standalone version
    with open(template_path, 'w', encoding='utf-8') as f:
        f.write(result)
    
    print(f"✓ Regenerated {template_name}")

print("\nDone! All templates regenerated with updated base.")
