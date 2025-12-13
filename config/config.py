"""
Configuration settings for CashApp
"""
import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

class Config:
    """Flask configuration"""
    
    # Flask settings
    SECRET_KEY = os.getenv('SECRET_KEY', 'dev-secret-key-change-in-production')
    DEBUG = os.getenv('FLASK_DEBUG', 'False').lower() == 'true'

    # Shared Database settings (Users, Authentication, Audit Logs)
    # This database contains resources used by all modules
    SHARED_DB_HOST = os.getenv('SHARED_DB_HOST', 'localhost')
    SHARED_DB_PORT = os.getenv('SHARED_DB_PORT', '5432')
    SHARED_DB_NAME = os.getenv('SHARED_DB_NAME', 'rpa_data')
    SHARED_DB_USER = os.getenv('SHARED_DB_USER', 'postgres')
    SHARED_DB_PASSWORD = os.getenv('SHARED_DB_PASSWORD', '')

    # BAI Database settings (Production - Read Only)
    BAI_DB_HOST = os.getenv('BAI_DB_HOST', 'localhost')
    BAI_DB_PORT = os.getenv('BAI_DB_PORT', '5432')
    BAI_DB_NAME = os.getenv('BAI_DB_NAME', 'rpa_data')
    BAI_DB_USER = os.getenv('BAI_DB_USER', 'postgres')
    BAI_DB_PASSWORD = os.getenv('BAI_DB_PASSWORD', '')

    # Recon Database settings (Accept - Development)
    RECON_DB_HOST = os.getenv('RECON_DB_HOST', 'localhost')
    RECON_DB_PORT = os.getenv('RECON_DB_PORT', '5432')
    RECON_DB_NAME = os.getenv('RECON_DB_NAME', 'recon_accept')
    RECON_DB_USER = os.getenv('RECON_DB_USER', 'postgres')
    RECON_DB_PASSWORD = os.getenv('RECON_DB_PASSWORD', '')

    # Session settings
    SESSION_COOKIE_SECURE = os.getenv('SESSION_COOKIE_SECURE', 'False').lower() == 'true'
    SESSION_COOKIE_HTTPONLY = True
    SESSION_COOKIE_SAMESITE = 'Lax'
    PERMANENT_SESSION_LIFETIME = 3600  # 1 hour
    
    # Application settings
    ITEMS_PER_PAGE = int(os.getenv('ITEMS_PER_PAGE', '50'))
    MAX_TRANSACTION_DISPLAY = int(os.getenv('MAX_TRANSACTION_DISPLAY', '10000'))
    
    @staticmethod
    def get_db_connection_string(db_type='bai'):
        """Generate PostgreSQL connection string for specified database
        
        Args:
            db_type (str): 'shared' (users/auth), 'bai' (production data), or 'recon' (accept data)
            
        Returns:
            str: PostgreSQL connection string
        """
        if db_type == 'shared':
            return (
                f"host={Config.SHARED_DB_HOST} "
                f"port={Config.SHARED_DB_PORT} "
                f"dbname={Config.SHARED_DB_NAME} "
                f"user={Config.SHARED_DB_USER} "
                f"password={Config.SHARED_DB_PASSWORD}"
            )
        elif db_type == 'bai':
            return (
                f"host={Config.BAI_DB_HOST} "
                f"port={Config.BAI_DB_PORT} "
                f"dbname={Config.BAI_DB_NAME} "
                f"user={Config.BAI_DB_USER} "
                f"password={Config.BAI_DB_PASSWORD}"
            )
        elif db_type == 'recon':
            return (
                f"host={Config.RECON_DB_HOST} "
                f"port={Config.RECON_DB_PORT} "
                f"dbname={Config.RECON_DB_NAME} "
                f"user={Config.RECON_DB_USER} "
                f"password={Config.RECON_DB_PASSWORD}"
            )
        else:
            raise ValueError(f"Unknown database type: {db_type}. Must be 'shared', 'bai', or 'recon'")
