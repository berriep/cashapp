"""
Access control decorators for CashApp modules
Provides permission-based access control for different modules
"""
from functools import wraps
from flask import flash, redirect, url_for
from flask_login import current_user

def require_bai_access(f):
    """
    Decorator to require BAI module access
    
    Usage:
        @bai_bp.route('/dashboard')
        @login_required
        @require_bai_access
        def bai_dashboard():
            ...
    """
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated:
            flash('Please log in to access this page.', 'warning')
            return redirect(url_for('shared.login'))
        
        # Admin has access to everything
        if current_user.is_admin:
            return f(*args, **kwargs)
        
        # Check BAI permission
        if not getattr(current_user, 'has_bai_access', False):
            flash('Access denied. BAI module access required.', 'danger')
            return redirect(url_for('shared.dashboard'))
        
        return f(*args, **kwargs)
    return decorated_function

def require_recon_access(f):
    """
    Decorator to require Recon module access
    
    Usage:
        @recon_bp.route('/dashboard')
        @login_required
        @require_recon_access
        def recon_dashboard():
            ...
    """
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated:
            flash('Please log in to access this page.', 'warning')
            return redirect(url_for('shared.login'))
        
        # Admin has access to everything
        if current_user.is_admin:
            return f(*args, **kwargs)
        
        # Check Recon permission
        if not getattr(current_user, 'has_recon_access', False):
            flash('Access denied. Recon module access required.', 'danger')
            return redirect(url_for('shared.dashboard'))
        
        return f(*args, **kwargs)
    return decorated_function

def require_admin(f):
    """
    Decorator to require admin privileges
    
    Usage:
        @shared_bp.route('/admin/users')
        @login_required
        @require_admin
        def admin_users():
            ...
    """
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated:
            flash('Please log in to access this page.', 'warning')
            return redirect(url_for('shared.login'))
        
        if not current_user.is_admin:
            flash('Access denied. Admin privileges required.', 'danger')
            return redirect(url_for('shared.dashboard'))
        
        return f(*args, **kwargs)
    return decorated_function
