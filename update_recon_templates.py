"""
Update Recon templates to use blueprint url_for() references
"""
import os
import re

template_dir = r'C:\Users\bpeijmen\Documents\Code\CashApp\app\recon\templates'

# Mapping of old routes to new blueprint routes
route_mappings = {
    # Dashboard and main
    "url_for('dashboard')": "url_for('recon.dashboard')",
    "url_for('login')": "url_for('shared.login')",
    "url_for('logout')": "url_for('shared.logout')",
    
    # Payments
    "url_for('payments')": "url_for('recon.payments')",
    "url_for('api_payment_details'": "url_for('recon.api_payment_details'",
    
    # Reconciliation
    "url_for('reconciliation')": "url_for('recon.reconciliation')",
    
    # Reports
    "url_for('reports')": "url_for('recon.reports')",
    
    # Import
    "url_for('import_data')": "url_for('recon.import_data')",
    "url_for('import_history')": "url_for('recon.import_history')",
    
    # Settings
    "url_for('settings')": "url_for('recon.settings')",
    
    # API endpoints
    "url_for('api_daily_stats'": "url_for('recon.api_daily_stats'",
    "url_for('api_brand_stats'": "url_for('recon.api_brand_stats'",
}

# Templates that should extend from shared base (base.html, login.html are kept in shared)
# All other recon templates should NOT change their extends

files_updated = 0
replacements_made = 0

for filename in os.listdir(template_dir):
    if filename.endswith('.html'):
        filepath = os.path.join(template_dir, filename)
        
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Apply route mappings
        for old_route, new_route in route_mappings.items():
            if old_route in content:
                content = content.replace(old_route, new_route)
                replacements_made += 1
                print(f"  {filename}: {old_route} → {new_route}")
        
        # Write back if changed
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            files_updated += 1
            print(f"✅ Updated: {filename}")

print(f"\n✅ Complete! Updated {files_updated} files with {replacements_made} replacements")
