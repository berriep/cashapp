import bcrypt

password = b'N33lk374!'
hash_val = bcrypt.hashpw(password, bcrypt.gensalt())
print(f"Bcrypt hash: {hash_val.decode('utf-8')}")
