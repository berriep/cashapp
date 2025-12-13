import re

file_path = r"C:\Users\bpeijmen\Documents\Code\CashApp\app\shared\templates\base.html"

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Fix all url_for references
replacements = [
    (r"url_for\('dashboard'\)", "url_for('shared.dashboard')"),
    (r"url_for\('transaction_details'\)", "url_for('bai.transaction_details')"),
    (r"url_for\('balances'\)", "url_for('bai.balances')"),
    (r"url_for\('bank_statements'\)", "url_for('bai.bank_statements')"),
    (r"url_for\('reports'\)", "url_for('bai.reports')"),
    (r"url_for\('admin_users'\)", "url_for('bai.admin_users')"),
    (r"url_for\('logout'\)", "url_for('shared.logout')"),
    (r"request\.endpoint == 'dashboard'", "request.endpoint == 'shared.dashboard'"),
    (r"request\.endpoint == 'transaction_details'", "request.endpoint == 'bai.transaction_details'"),
    (r"request\.endpoint == 'balances'", "request.endpoint == 'bai.balances'"),
    (r"request\.endpoint == 'bank_statements'", "request.endpoint == 'bai.bank_statements'"),
]

for old, new in replacements:
    content = re.sub(old, new, content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("✅ base.html updated with blueprint url_for() references!")
