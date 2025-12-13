"""
Shared authentication module for CashApp
Handles login, logout, and user management
"""
from flask import Blueprint, render_template, request, redirect, url_for, flash
from flask_login import UserMixin, login_user, logout_user, login_required, current_user
import bcrypt
from config.config import Config
from app.shared.decorators import require_admin

# Create shared blueprint
shared_bp = Blueprint('shared', __name__, template_folder='templates')

class User(UserMixin):
    """User model for authentication"""

    def __init__(self, username, user_id=None, email=None, full_name=None, is_admin=False, has_bai_access=False, has_recon_access=False):
        self.id = username
        self.user_id = user_id
        self.username = username
        self.email = email
        self.full_name = full_name
        self.is_admin = is_admin
        self.has_bai_access = has_bai_access
        self.has_recon_access = has_recon_access

    @staticmethod
    def get(username):
        """Get user by username from database"""
        from app.shared.database import shared_db
        try:
            result = shared_db.execute_query(
                "SELECT id, username, email, full_name, is_admin, has_bai_access, has_recon_access FROM rpa_data.cashapp_users WHERE username = %s AND is_active = TRUE",
                (username,)
            )
            if result:
                user_data = result[0]
                return User(
                    username=user_data['username'],
                    user_id=user_data['id'],
                    email=user_data['email'],
                    full_name=user_data['full_name'],
                    is_admin=user_data['is_admin'],
                    has_bai_access=user_data.get('has_bai_access', False),
                    has_recon_access=user_data.get('has_recon_access', False)
                )
        except:
            pass

        if username == getattr(Config, 'ADMIN_USERNAME', 'admin'):
            return User(username, is_admin=True)
        return None

    @staticmethod
    def verify_password(username, password):
        """Verify username and password against database"""
        from app.shared.database import shared_db
        try:
            result = shared_db.execute_query(
                "SELECT password_hash, is_active FROM rpa_data.cashapp_users WHERE username = %s",
                (username,)
            )
            if result and result[0]['is_active']:
                password_hash = result[0]['password_hash']
                return bcrypt.checkpw(password.encode('utf-8'), password_hash.encode('utf-8'))
        except:
            pass

        env_password = getattr(Config, 'ADMIN_PASSWORD', 'admin')
        return username == getattr(Config, 'ADMIN_USERNAME', 'admin') and password == env_password

    @staticmethod
    def get_all_users():
        """Get all users from database"""
        from app.shared.database import shared_db
        try:
            results = shared_db.execute_query(
                "SELECT id, username, email, full_name, is_admin, has_bai_access, has_recon_access, is_active, created_at, last_login FROM rpa_data.cashapp_users ORDER BY username"
            )
            return results if results else []
        except Exception as e:
            print(f"Error getting all users: {e}")
            return []

    @staticmethod
    def create_user(username, password, email=None, full_name=None, is_admin=False, has_bai_access=True, has_recon_access=False):
        '''Create new user in database'''
        from app.shared.database import shared_db
        password_hash = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        shared_db.execute_update(
            '''INSERT INTO rpa_data.cashapp_users (username, password_hash, email, full_name, is_admin, has_bai_access, has_recon_access, is_active) VALUES (%s, %s, %s, %s, %s, %s, %s, TRUE)''',
            (username, password_hash, email, full_name, is_admin, has_bai_access, has_recon_access),
            
        )

    @staticmethod
    def update_user(user_id, email=None, full_name=None, is_admin=False, has_bai_access=True, has_recon_access=False, is_active=True):
        '''Update user in database'''
        from app.shared.database import shared_db
        shared_db.execute_update(
            '''UPDATE rpa_data.cashapp_users SET email = %s, full_name = %s, is_admin = %s, has_bai_access = %s, has_recon_access = %s, is_active = %s WHERE id = %s''',
            (email, full_name, is_admin, has_bai_access, has_recon_access, is_active, user_id),
            
        )

    @staticmethod
    def delete_user(user_id):
        '''Delete user from database (soft delete - set is_active to FALSE)'''
        from app.shared.database import shared_db
        shared_db.execute_update(
            'UPDATE rpa_data.cashapp_users SET is_active = FALSE WHERE id = %s',
            (user_id,),
            
        )

    @staticmethod
    def change_password(user_id, new_password):
        '''Change user password'''
        from app.shared.database import shared_db
        password_hash = bcrypt.hashpw(new_password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        shared_db.execute_update(
            'UPDATE rpa_data.cashapp_users SET password_hash = %s WHERE id = %s',
            (password_hash, user_id),
            
        )


@shared_bp.route('/login', methods=['GET', 'POST'])
def login():
    """Login page"""
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')

        if User.verify_password(username, password):
            user = User.get(username)
            if user:
                login_user(user, remember=True)
                flash('Successfully logged in!', 'success')
                next_page = request.args.get('next')
                return redirect(next_page if next_page else url_for('shared.dashboard'))

        flash('Invalid username or password', 'danger')

    return render_template('login.html')

@shared_bp.route('/logout')
@login_required
def logout():
    """Logout current user"""
    logout_user()
    flash('You have been logged out.', 'info')
    return redirect(url_for('shared.login'))

@shared_bp.route('/dashboard')
@login_required
def dashboard():
    """Main dashboard - shows links to both modules"""
    return render_template('dashboard.html')


# Admin routes
@shared_bp.route('/admin')
@login_required
@require_admin
def admin():
    '''Admin dashboard - redirect to users'''
    return redirect(url_for('shared.admin_users'))

@shared_bp.route('/admin/users')
@login_required
@require_admin
def admin_users():
    '''User management page'''
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('shared.dashboard'))
    
    users = User.get_all_users()
    return render_template('admin/users.html', users=users)

@shared_bp.route('/admin/users/create', methods=['GET', 'POST'])
@login_required
@require_admin
def admin_create_user():
    '''Create new user'''
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('shared.dashboard'))
    
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')
        email = request.form.get('email')
        full_name = request.form.get('full_name')
        is_admin = request.form.get('is_admin') == 'on'
        has_bai_access = request.form.get('has_bai_access') == 'on'
        has_recon_access = request.form.get('has_recon_access') == 'on'
        
        if not username or not password:
            flash('Username and password are required', 'danger')
        else:
            try:
                User.create_user(username, password, email, full_name, is_admin, has_bai_access, has_recon_access)
                flash(f'User {username} created successfully', 'success')
                return redirect(url_for('shared.admin_users'))
            except Exception as e:
                flash(f'Error creating user: {str(e)}', 'danger')
    
    return render_template('admin/create_user.html')

@shared_bp.route('/admin/users/<int:user_id>/edit', methods=['GET', 'POST'])
@login_required
@require_admin
def admin_edit_user(user_id):
    '''Edit user'''
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('shared.dashboard'))
    
    if request.method == 'POST':
        email = request.form.get('email')
        full_name = request.form.get('full_name')
        is_admin = request.form.get('is_admin') == 'on'
        has_bai_access = request.form.get('has_bai_access') == 'on'
        has_recon_access = request.form.get('has_recon_access') == 'on'
        is_active = request.form.get('is_active') == 'on'
        
        try:
            User.update_user(user_id, email=email, full_name=full_name, is_admin=is_admin, has_bai_access=has_bai_access, has_recon_access=has_recon_access, is_active=is_active)
            flash('User updated successfully', 'success')
            return redirect(url_for('shared.admin_users'))
        except Exception as e:
            flash(f'Error updating user: {str(e)}', 'danger')
    
    users = User.get_all_users()
    user = next((u for u in users if u['id'] == user_id), None)
    if not user:
        flash('User not found', 'danger')
        return redirect(url_for('shared.admin_users'))
    
    return render_template('admin/edit_user.html', user=user)

@shared_bp.route('/admin/users/<int:user_id>/delete', methods=['POST'])
@login_required
@require_admin
def admin_delete_user(user_id):
    '''Delete user'''
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('shared.dashboard'))
    
    try:
        User.delete_user(user_id)
        flash('User deleted successfully', 'success')
    except Exception as e:
        flash(f'Error deleting user: {str(e)}', 'danger')
    
    return redirect(url_for('shared.admin_users'))

@shared_bp.route('/admin/users/<int:user_id>/change-password', methods=['POST'])
@login_required
@require_admin
def admin_change_password(user_id):
    '''Change user password'''
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('shared.dashboard'))
    
    new_password = request.form.get('new_password')
    if not new_password:
        flash('Password is required', 'danger')
    else:
        try:
            User.change_password(user_id, new_password)
            flash('Password changed successfully', 'success')
        except Exception as e:
            flash(f'Error resetting password: {str(e)}', 'danger')
    
    return redirect(url_for('shared.admin_users'))










