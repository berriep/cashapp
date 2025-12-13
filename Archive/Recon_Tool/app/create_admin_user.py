"""
Create initial admin user in the database
"""
import bcrypt
from config.config import Config
from database import db

def create_admin_user(username, password, email=None, full_name=None):
    """Create admin user in the database"""
    password_hash = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
    
    query = f"""
        INSERT INTO {Config.DB_SCHEMA}.recon_monitor_users (username, password_hash, email, full_name, is_admin)
        VALUES (%s, %s, %s, %s, TRUE)
        ON CONFLICT (username) DO UPDATE
        SET password_hash = EXCLUDED.password_hash,
            email = EXCLUDED.email,
            full_name = EXCLUDED.full_name,
            updated_at = CURRENT_TIMESTAMP
        RETURNING id
    """
    
    conn = db.connect()
    try:
        with conn.cursor() as cur:
            cur.execute(query, (username, password_hash, email, full_name))
            result = cur.fetchone()
            conn.commit()
            return result['id'] if result else None
    except Exception as e:
        conn.rollback()
        raise e
    finally:
        db.close()

if __name__ == '__main__':
    print("Create Admin User")
    print("-" * 50)
    
    username = input("Username [admin]: ").strip() or "admin"
    password = input("Password: ").strip()
    email = input("Email (optional): ").strip() or None
    full_name = input("Full Name (optional): ").strip() or None
    
    if not password:
        print("Error: Password is required!")
        exit(1)
    
    try:
        user_id = create_admin_user(username, password, email, full_name)
        print(f"\n✓ Admin user '{username}' created successfully (ID: {user_id})")
    except Exception as e:
        print(f"\n✗ Error creating user: {e}")
        exit(1)
