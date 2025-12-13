import bcrypt
import sys

def generate_password_hash(password):
    """Generate bcrypt password hash"""
    return bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')

if __name__ == '__main__':
    if len(sys.argv) > 1:
        password = sys.argv[1]
    else:
        password = input("Enter password: ")
    
    hash_value = generate_password_hash(password)
    print(f"\nPassword hash:\n{hash_value}\n")
    print("Use this hash when creating a user in the database:")
    print(f"INSERT INTO reconciliation.monitor_users (username, password_hash, email, full_name, is_admin)")
    print(f"VALUES ('username', '{hash_value}', 'user@example.com', 'Full Name', FALSE);")
