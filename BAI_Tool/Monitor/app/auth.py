from flask_login import UserMixin
import bcrypt
from config.config import Config

class User(UserMixin):
    """User model for authentication"""
    
    def __init__(self, username, user_id=None, email=None, full_name=None, is_admin=False):
        self.id = username
        self.user_id = user_id
        self.username = username
        self.email = email
        self.full_name = full_name
        self.is_admin = is_admin
    
    @staticmethod
    def get(username):
        """Get user by username from database"""
        from app.database import db
        try:
            result = db.execute_query(
                "SELECT id, username, email, full_name, is_admin FROM rpa_data.bai_monitor_users WHERE username = %s AND is_active = TRUE",
                (username,)
            )
            if result:
                user_data = result[0]
                return User(
                    username=user_data['username'],
                    user_id=user_data['id'],
                    email=user_data['email'],
                    full_name=user_data['full_name'],
                    is_admin=user_data['is_admin']
                )
        except:
            # Fallback to config-based auth if table doesn't exist
            pass
        
        # Fallback: check environment variable
        if username == Config.ADMIN_USERNAME:
            return User(username, is_admin=True)
        return None
    
    @staticmethod
    def verify_password(username, password):
        """Verify username and password against database"""
        from app.database import db
        try:
            # Try database authentication first
            result = db.execute_query(
                "SELECT password_hash, is_active FROM rpa_data.bai_monitor_users WHERE username = %s",
                (username,)
            )
            if result:
                user_data = result[0]
                if not user_data['is_active']:
                    return False
                password_hash = user_data['password_hash']
                return bcrypt.checkpw(password.encode('utf-8'), password_hash.encode('utf-8'))
        except:
            # Fallback to config-based auth if table doesn't exist
            pass
        
        # Fallback to environment variable authentication
        if username == Config.ADMIN_USERNAME and password == Config.ADMIN_PASSWORD:
            return True
        return False
    
    @staticmethod
    def create_user(username, password, email=None, full_name=None, is_admin=False):
        """Create a new user in the database"""
        from app.database import db
        password_hash = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        
        query = """
            INSERT INTO rpa_data.bai_monitor_users (username, password_hash, email, full_name, is_admin)
            VALUES (%s, %s, %s, %s, %s)
            RETURNING id
        """
        result = db.execute_query(query, (username, password_hash, email, full_name, is_admin))
        return result[0]['id'] if result else None
    
    @staticmethod
    def update_user(user_id, email=None, full_name=None, is_admin=None, is_active=None):
        """Update user details"""
        from app.database import db
        
        updates = []
        params = []
        
        if email is not None:
            updates.append("email = %s")
            params.append(email)
        if full_name is not None:
            updates.append("full_name = %s")
            params.append(full_name)
        if is_admin is not None:
            updates.append("is_admin = %s")
            params.append(is_admin)
        if is_active is not None:
            updates.append("is_active = %s")
            params.append(is_active)
        
        if not updates:
            return False
        
        updates.append("updated_at = CURRENT_TIMESTAMP")
        params.append(user_id)
        
        query = f"UPDATE rpa_data.bai_monitor_users SET {', '.join(updates)} WHERE id = %s"
        db.execute_query(query, tuple(params))
        return True
    
    @staticmethod
    def change_password(user_id, new_password):
        """Change user password"""
        from app.database import db
        password_hash = bcrypt.hashpw(new_password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        
        query = "UPDATE rpa_data.bai_monitor_users SET password_hash = %s, updated_at = CURRENT_TIMESTAMP WHERE id = %s"
        db.execute_query(query, (password_hash, user_id))
        return True
    
    @staticmethod
    def get_all_users():
        """Get all users from database"""
        from app.database import db
        try:
            result = db.execute_query("""
                SELECT id, username, email, full_name, is_admin, is_active, created_at, last_login
                FROM rpa_data.bai_monitor_users
                ORDER BY created_at DESC
            """)
            return result if result else []
        except:
            return []
    
    @staticmethod
    def delete_user(user_id):
        """Delete a user (soft delete by setting is_active to false)"""
        from app.database import db
        query = "UPDATE rpa_data.bai_monitor_users SET is_active = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = %s"
        db.execute_query(query, (user_id,))
        return True
    
    @staticmethod
    def update_last_login(username):
        """Update last login timestamp"""
        from app.database import db
        try:
            query = "UPDATE rpa_data.bai_monitor_users SET last_login = CURRENT_TIMESTAMP WHERE username = %s"
            db.execute_query(query, (username,))
        except:
            pass
