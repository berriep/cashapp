# CashApp - Unified Finance Platform

Integrated platform combining BAI Monitor and Reconciliation tools for the Finance team. Built with Flask Blueprints architecture for modular, scalable development.

## Features

### BAI Monitor Module (`/bai`)
- Real-time transaction monitoring dashboard
- Balance tracking and reconciliation
- Data quality tracking and reporting
- Missing data detection
- Bank statements and PDF export
- Transaction detail views with filtering

### Reconciliation Module (`/recon`)
- Automated reconciliation workflows (placeholder)
- Scheduled background jobs
- Manual reconciliation interface
- Reconciliation reporting

### User Management (`/admin`)
- Centralized user administration
- Module-based access control (BAI, Recon)
- Role-based permissions (Admin, User)
- User creation and management

## Project Structure

```
CashApp/
├── app/
│   ├── bai/              # BAI Monitor module (routes, templates, PDF generator)
│   ├── recon/            # Reconciliation module (placeholder)
│   ├── shared/           # Shared components
│   │   ├── auth.py       # Authentication & user management
│   │   ├── database.py   # Multi-database connection management
│   │   ├── decorators.py # Access control decorators
│   │   ├── templates/    # Shared templates (login, dashboard, admin)
│   │   └── static/       # CSS, images, JavaScript
│   └── main.py           # Main Flask application
├── config/
│   └── config.py         # Environment-based configuration
├── database/             # Migration scripts & documentation
│   ├── migrate_fase1_direct.py
│   ├── migrate_fase2.py
│   ├── IMPLEMENTATION_SUMMARY.md
│   └── README.md
├── requirements.txt      # Python dependencies
├── .env                  # Environment variables (not in git)
├── MIGRATION_STATUS.md   # Complete migration history
└── README.md             # This file
```

## Setup

1. **Create virtual environment:**
   ```powershell
   python -m venv venv
   .\venv\Scripts\Activate.ps1
   ```

2. **Install dependencies:**
   ```powershell
   pip install -r requirements.txt
   ```

3. **Configure environment:**
   ```powershell
   cp .env.example .env
   # Edit .env with your database credentials
   ```

4. **Run application:**
   ```powershell
   # Set PYTHONPATH and run the application
   $env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"
   python -m app.main
   
   # Or in one command:
   cd C:\Users\bpeijmen\Documents\Code\CashApp; $env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"; py -m app.main
   ```

5. **Access application:**
   - Main Dashboard: http://localhost:5000
   - BAI Monitor: http://localhost:5000/bai/dashboard
   - Reconciliation: http://localhost:5000/recon/dashboard
   - User Management: http://localhost:5000/admin/users

## Database Configuration

CashApp uses a **multi-database architecture** with environment separation:

- **Shared DB** (`shared_db`): User authentication and platform-wide data
  - Table: `rpa_data.cashapp_users`
  - Connection: Azure PostgreSQL (psql-prd-weu-rpa-02.postgres.database.azure.com)
  - Environment: **PRODUCTION**

- **BAI DB** (`bai_db`): BAI Monitor transaction data
  - Tables: `rpa_data.bai_rabobank_transactions`, `bai_rabobank_balances`, etc.
  - Connection: Same Azure PostgreSQL, database `p1pgrpacp`
  - Environment: **PRODUCTION** (read-only recommended)

- **Recon DB** (`recon_db`): Reconciliation module Worldline data
  - Tables: `rpa_data.recon_worldline_payments`, `recon_reconciliation_matches`, etc.
  - Connection: **ACCEPT DATABASE** (safe testing environment)
  - Environment: **ACCEPT/TEST** (NOT production!)
  
### Why Separate Databases?

- ✅ **BAI** monitors real-time production transactions (read-only)
- ✅ **Recon** uses accept database for safe Worldline reconciliation testing
- ✅ No risk of production data corruption during Recon development
- ✅ Both modules can operate independently

### Database Migrations

See `/database/README.md` for migration scripts and rollback procedures.

**Completed migrations:**
- Phase 1: Renamed `bai_monitor_users` → `cashapp_users`
- Phase 2: Added module permissions (`has_bai_access`, `has_recon_access`)

## User Management & Permissions

### Access Levels
- **Admin**: Full platform access, can manage users and permissions
- **BAI Access**: Can view and use BAI Monitor module
- **Recon Access**: Can view and use Reconciliation module (when implemented)

### Managing Users
1. Login as admin user
2. Navigate to Admin → User Management (`/admin/users`)
3. Create/edit users and assign module permissions

### Default Users (after migration)
- `admin`: Full access to all modules
- `barry`: Full access to all modules

## Development

### Running locally
```powershell
$env:FLASK_ENV="development"
.\venv\Scripts\Activate.ps1
python -m app.main
```

### Running with Docker
```powershell
docker-compose up
```

## Migration Notes

This project consolidates:
- `BAI_Tool/Monitor` → `app/bai`
- `Recon_Tool` → `app/recon`

Original projects kept in root for reference during migration.
