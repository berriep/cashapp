"""Create standalone dashboard.html without template inheritance"""

# Read the base template
with open(r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates\recon_base.html', 'r', encoding='utf-8') as f:
    base_content = f.read()

# Read the dashboard content (everything between {% block content %} and {% endblock %})
with open(r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates\dashboard_with_extends.html', 'r', encoding='utf-8') as f:
    dashboard_lines = f.readlines()

# Extract content block (skip first 2 lines: {% extends %} and {% block title %})
content_start = False
content_lines = []
scripts_lines = []
in_scripts = False

for line in dashboard_lines:
    if '{% block content %}' in line:
        content_start = True
        continue
    if '{% block scripts %}' in line:
        in_scripts = True
        continue
    if '{% endblock %}' in line:
        if in_scripts:
            in_scripts = False
        else:
            content_start = False
        continue
    
    if content_start:
        content_lines.append(line)
    elif in_scripts:
        scripts_lines.append(line)

content_block = ''.join(content_lines)
scripts_block = ''.join(scripts_lines)

# Replace the {% block content %} in base with actual content
result = base_content.replace('{% block content %}{% endblock %}', content_block.strip())

# Replace the {% block scripts %} in base with actual scripts
result = result.replace('{% block scripts %}{% endblock %}', scripts_block.strip())

# Write to dashboard.html
with open(r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates\dashboard.html', 'w', encoding='utf-8') as f:
    f.write(result)

print("✓ Created standalone dashboard.html")
print(f"  Content lines: {len(content_lines)}")
print(f"  Scripts lines: {len(scripts_lines)}")
