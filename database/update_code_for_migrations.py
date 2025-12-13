"""
Automated Code Update Script
Updates Python code after database migrations
"""
import sys
import os
import re

def update_fase1():
    """Update code voor Fase 1: bai_monitor_users -> cashapp_users"""
    print("\n" + "="*80)
    print("Updating code for Fase 1: Rename users table")
    print("="*80 + "\n")
    
    auth_file = os.path.join(os.path.dirname(__file__), '..', 'app', 'shared', 'auth.py')
    
    # Read file
    with open(auth_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Count replacements
    count = content.count('bai_monitor_users')
    
    if count == 0:
        print("✓ No replacements needed - already using 'cashapp_users'")
        return True
    
    print(f"Found {count} occurrences of 'bai_monitor_users' in auth.py")
    
    # Replace all occurrences
    updated_content = content.replace('bai_monitor_users', 'cashapp_users')
    
    # Backup original
    backup_file = auth_file + '.backup_fase1'
    with open(backup_file, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"✓ Created backup: {os.path.basename(backup_file)}")
    
    # Write updated file
    with open(auth_file, 'w', encoding='utf-8') as f:
        f.write(updated_content)
    
    print(f"✓ Updated {count} references in auth.py")
    print("\n✅ Fase 1 code update completed!\n")
    return True

def update_fase2():
    """Update code voor Fase 2: Add module permissions"""
    print("\n" + "="*80)
    print("Updating code for Fase 2: Module Permissions")
    print("="*80 + "\n")
    
    changes_made = []
    
    # 1. Update User class in auth.py
    auth_file = os.path.join(os.path.dirname(__file__), '..', 'app', 'shared', 'auth.py')
    
    with open(auth_file, 'r', encoding='utf-8') as f:
        auth_content = f.read()
    
    # Add permission properties to User.__init__
    if 'self.has_bai_access' not in auth_content:
        # Find __init__ method and add properties
        init_pattern = r'(def __init__\(self, username, user_id=None, email=None, full_name=None, is_admin=False\):)'
        new_init = r'\1\n        # Module permissions\n        self.has_bai_access = False\n        self.has_recon_access = False'
        
        # This is a complex change - we'll create a template
        print("⚠️  Manual step required:")
        print("    Add to User.__init__:")
        print("        self.has_bai_access = has_bai_access")
        print("        self.has_recon_access = has_recon_access")
        changes_made.append("User class needs permission properties")
    
    # 2. Update User.get() to fetch permissions
    if 'has_bai_access' not in auth_content or 'SELECT id, username' in auth_content:
        print("⚠️  Manual step required:")
        print("    Update User.get() SELECT query to include:")
        print("        has_bai_access, has_recon_access")
        changes_made.append("User.get() needs to fetch permissions")
    
    # 3. Update User.get_all_users() to include permissions
    if 'get_all_users' in auth_content:
        print("⚠️  Manual step required:")
        print("    Update User.get_all_users() SELECT query to include:")
        print("        has_bai_access, has_recon_access")
        changes_made.append("User.get_all_users() needs permission columns")
    
    # 4. Create decorator file
    decorator_file = os.path.join(os.path.dirname(__file__), '..', 'app', 'shared', 'decorators.py')
    if not os.path.exists(decorator_file):
        decorator_content = '''"""
Access control decorators for CashApp modules
"""
from functools import wraps
from flask import flash, redirect, url_for
from flask_login import current_user

def require_bai_access(f):
    """Decorator to require BAI module access"""
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated:
            flash('Please log in to access this page.', 'warning')
            return redirect(url_for('shared.login'))
        
        if not (current_user.is_admin or getattr(current_user, 'has_bai_access', False)):
            flash('Access denied. BAI module access required.', 'danger')
            return redirect(url_for('shared.dashboard'))
        
        return f(*args, **kwargs)
    return decorated_function

def require_recon_access(f):
    """Decorator to require Recon module access"""
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated:
            flash('Please log in to access this page.', 'warning')
            return redirect(url_for('shared.login'))
        
        if not (current_user.is_admin or getattr(current_user, 'has_recon_access', False)):
            flash('Access denied. Recon module access required.', 'danger')
            return redirect(url_for('shared.dashboard'))
        
        return f(*args, **kwargs)
    return decorated_function
'''
        with open(decorator_file, 'w', encoding='utf-8') as f:
            f.write(decorator_content)
        print(f"✓ Created {os.path.basename(decorator_file)}")
        changes_made.append("Created decorators.py")
    
    if changes_made:
        print(f"\n✅ Fase 2 automated changes completed!")
        print(f"\n⚠️  {len(changes_made)} manual steps required - see above")
    else:
        print("\n✅ Fase 2 code already up to date!")
    
    return True

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("\nUsage: python update_code_for_migrations.py <fase1|fase2>")
        print("\n  fase1 - Update code for renamed users table")
        print("  fase2 - Update code for module permissions")
        sys.exit(1)
    
    fase = sys.argv[1].lower()
    
    if fase == 'fase1':
        success = update_fase1()
    elif fase == 'fase2':
        success = update_fase2()
    else:
        print(f"Unknown fase: {fase}")
        print("Use 'fase1' or 'fase2'")
        sys.exit(1)
    
    sys.exit(0 if success else 1)
