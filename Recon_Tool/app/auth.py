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
        from database import db
        try:
            result = db.execute_query(
                f"SELECT id, username, email, full_name, is_admin FROM {Config.DB_SCHEMA}.recon_monitor_users WHERE username = %s AND is_active = TRUE",
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
        from database import db
        try:
            # Try database authentication first
            result = db.execute_query(
                f"SELECT password_hash, is_active FROM {Config.DB_SCHEMA}.recon_monitor_users WHERE username = %s",
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
        from database import db
        password_hash = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        
        query = f"""
            INSERT INTO {Config.DB_SCHEMA}.recon_monitor_users (username, password_hash, email, full_name, is_admin)
            VALUES (%s, %s, %s, %s, %s)
            RETURNING id
        """
        conn = db.connect()
        try:
            with conn.cursor() as cur:
                cur.execute(query, (username, password_hash, email, full_name, is_admin))
                result = cur.fetchone()
                conn.commit()
                return result['id'] if result else None
        except Exception as e:
            conn.rollback()
            raise e
    
    @staticmethod
    def update_last_login(username):
        """Update last login timestamp"""
        from database import db
        try:
            query = f"UPDATE {Config.DB_SCHEMA}.recon_monitor_users SET last_login = CURRENT_TIMESTAMP WHERE username = %s"
            conn = db.connect()
            with conn.cursor() as cur:
                cur.execute(query, (username,))
                conn.commit()
        except:
            pass
