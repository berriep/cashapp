# Reconciliation Tool - Implementatie Compleet! 🎉

## Wat is er gebouwd?

Een complete reconciliation tool voor Worldline betalingsdata met:

### ✅ Database Layer
- **PostgreSQL schema** met table partitioning per maand
- **45 kolommen** voor Worldline payment data
- **Multi-source reconciliation** tabellen (Bank, Reservering, SAP)
- **Automatische partitioning** voor 24 maanden history + 6 maanden toekomst
- **Archive functionaliteit** voor oude data
- **User authentication** tabel

### ✅ Backend (Python + Flask)
- **CSV Import module** met:
  - European decimal format support (comma → dot)
  - Date parsing (DD/MM/YYYY)
  - Duplicate detection op Id
  - Batch import (1000 records/batch)
  - Automatic partition creation
  - Import logging & audit trail

- **Database query module** met:
  - Worldline payment queries (filter, search, pagination)
  - Summary statistics & aggregaties
  - Daily volume tracking
  - Brand/merchant/country breakdowns
  - Reconciliation queries (matched/unmatched)
  - Exception management

- **Authentication** (hergebruik van BAI_Tool):
  - Bcrypt password hashing
  - Database-based user management
  - Session management
  - Admin/user roles

### ✅ Frontend (Flask + Bootstrap)
- **Dashboard** - KPIs, charts, brand breakdown
- **Payments** - Zoeken, filteren, pagination (50 per page)
- **Reconciliation** - Matched/unmatched, exceptions
- **Reports** - Top merchants, country/brand breakdown
- **Import** - File upload, import history
- **Settings** - Data sources, rules, partitions

### ✅ Deployment
- **Docker** support (Dockerfile + docker-compose.yml)
- **Local development** setup
- **Azure deployment** ready
- **Environment configuration** (.env files)

## Project Structuur

```
Reconciliation/
├── README.md                    # Hoofddocumentatie
├── database/
│   ├── README.md               # Database setup guide
│   ├── 01_create_schema.sql   # Schema + partitioning
│   └── 02_create_users_table.sql
├── app/
│   ├── main.py                 # Flask application
│   ├── auth.py                 # Authentication
│   ├── database.py             # Database queries
│   ├── data_import.py          # CSV import logic
│   ├── requirements.txt
│   ├── Dockerfile
│   ├── docker-compose.yml
│   ├── .env.example
│   ├── .gitignore
│   ├── config/
│   │   └── config.py
│   ├── templates/
│   │   ├── base.html
│   │   ├── login.html
│   │   ├── dashboard.html
│   │   ├── payments.html
│   │   ├── reconciliation.html
│   │   ├── reports.html
│   │   ├── import.html
│   │   ├── import_history.html
│   │   ├── settings.html
│   │   ├── 404.html
│   │   └── 500.html
│   ├── static/
│   │   └── css/
│   │       └── style.css
│   ├── uploads/
│   │   └── .gitkeep
│   └── utility scripts:
│       ├── create_admin_user.py
│       ├── test_db.py
│       └── generate_hash.py
└── Documentation/
    ├── Worldline_Payments.csv (sample)
    └── Monitor/ (BAI_Tool reference)
```

## Volgende Stappen

### 1. Database Setup (Azure PostgreSQL)

```bash
cd database

# Connect to Azure PostgreSQL
psql "host=your-server.postgres.database.azure.com port=5432 dbname=reconciliation user=your-user@your-server sslmode=require"

# Run schema scripts
\i 01_create_schema.sql
\i 02_create_users_table.sql

# Verify
\dn  # Check schemas
\dt reconciliation.*  # Check tables
```

### 2. Configureer Applicatie

```bash
cd app

# Create .env file
cp .env.example .env

# Edit .env met je Azure credentials:
# - DB_HOST=your-server.postgres.database.azure.com
# - DB_USER=your-user@your-server
# - DB_PASSWORD=your-password
# - SECRET_KEY=random-secret-key
```

### 3. Setup Python Environment

```powershell
# Create virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt
```

### 4. Test Database Connection

```bash
python test_db.py
```

### 5. Create Admin User

```bash
python create_admin_user.py
# Username: admin
# Password: [kies een sterk wachtwoord]
```

### 6. Run Application

**Development:**
```bash
python main.py
# Open: http://localhost:5000
```

**Production (Docker):**
```bash
docker-compose up -d
# Open: http://localhost:5000
```

### 7. Import Eerste Data

1. Login met admin credentials
2. Navigate to **Import** page
3. Upload `Worldline_Payments_2025.csv` (of andere file)
4. Monitor import progress
5. Check **Dashboard** voor statistieken

## Data Volume & Performance

### Huidige Situatie
- **1 maand**: ~180,000 records (~80 MB CSV)
- **18 maanden**: ~3,240,000 records (~1.44 GB)
- **Partitioning**: Maandelijks voor optimale query performance

### Database Capaciteit
PostgreSQL kan dit makkelijk aan met:
- **Minimum**: General Purpose, 2 vCores, 10 GB storage
- **Aanbevolen**: General Purpose, 4 vCores, 50 GB storage (auto-grow enabled)

### Performance Tips
1. Gebruik altijd `paydate` filter in queries → partition pruning
2. Indexes bestaan op: id, paydate, ref, order, batchref, brand, merchref
3. Bulk imports via COPY zijn supersnel (180K records in <1 minuut)

## Reconciliation Matching

### Default Matching Criteria
Configureerbaar via `reconciliation_rules` tabel:
- **REF** (payment reference)
- **PAYDATE** (±1 dag tolerance)
- **BRAND** (MasterCard, Visa, etc.)
- **MERCHREF** (merchant reference)

### Toekomstige Databronnen
Schema is voorbereid voor:
- **Bank Statements** - Bank reconciliatie
- **Reservation System** - Booking data matching
- **SAP** - ERP financial data

Nieuwe tabellen aanmaken naar hetzelfde patroon als `worldline_payments`.

## Azure Deployment (Later)

### Option 1: Azure App Service
```bash
# Deploy via Azure CLI
az webapp up --name reconciliation-tool --resource-group your-rg --runtime "PYTHON:3.11"
```

### Option 2: Azure Container Instances
```bash
# Build en push naar Azure Container Registry
docker build -t yourregistry.azurecr.io/reconciliation-tool:latest .
docker push yourregistry.azurecr.io/reconciliation-tool:latest

# Deploy
az container create --resource-group your-rg \
  --name reconciliation-tool \
  --image yourregistry.azurecr.io/reconciliation-tool:latest \
  --dns-name-label reconciliation-tool \
  --ports 5000
```

## Monitoring & Maintenance

### Database Maintenance

**Check partition sizes:**
```sql
SELECT 
    tablename,
    pg_size_pretty(pg_total_relation_size('reconciliation.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'reconciliation' 
  AND tablename LIKE 'worldline_payments_%'
ORDER BY tablename DESC;
```

**Archive old partitions (>18 maanden):**
```sql
SELECT * FROM reconciliation.archive_old_partitions(18);
```

**Create new partitions:**
```sql
SELECT reconciliation.create_worldline_partition('2026-06-01'::DATE);
```

### Application Logs
- Flask logging naar console/file
- Import logs in `file_import_log` tabel
- Azure Application Insights (optioneel)

## Features Overzicht

| Feature | Status | Notities |
|---------|--------|----------|
| Database Schema | ✅ | Partitioned, indexed |
| CSV Import | ✅ | Duplicate detection, logging |
| User Authentication | ✅ | Bcrypt, database-based |
| Payment Queries | ✅ | Filter, search, pagination |
| Dashboard | ✅ | Stats, charts, KPIs |
| Reports | ✅ | Merchants, brands, countries |
| Reconciliation Base | ✅ | Schema ready, basic UI |
| Multi-source Matching | 🔄 | Schema ready, matching logic TODO |
| Bank Data Import | 📋 | Future phase |
| SAP Integration | 📋 | Future phase |
| Reservation System | 📋 | Future phase |
| Automated Reconciliation | 📋 | Rule engine TODO |
| Exception Workflow | 🔄 | Basic UI, workflow TODO |
| Azure Deployment | 📋 | Config ready, deploy TODO |

## Technische Specificaties

- **Python**: 3.11
- **Flask**: 3.0.0
- **PostgreSQL**: 12+ (tested with 15)
- **Bootstrap**: 5.3.0
- **Chart.js**: 4.4.0
- **Authentication**: Flask-Login + Bcrypt
- **Database Driver**: psycopg2-binary

## Support & Contact

Voor vragen of issues:
1. Check `README.md` in root folder
2. Check `database/README.md` voor database specifics
3. Run `python test_db.py` voor connection issues
4. Check import logs in database

## Notes

- **European Decimal Format**: Automatisch geconverteerd (1.234,56 → 1234.56)
- **Date Formats**: DD/MM/YYYY en DD/MM/YYYY HH:MM:SS supported
- **File Size Limit**: 500 MB (configureerbaar in config.py)
- **Session Timeout**: 1 uur (configureerbaar)
- **Batch Size**: 1000 records per batch (optimaal voor performance)

---

## Ready to Go! 🚀

De complete basis-implementatie is klaar. Je kunt nu:

1. ✅ Database schema uitrollen op Azure PostgreSQL
2. ✅ Applicatie configureren en testen
3. ✅ Eerste admin user aanmaken
4. ✅ Worldline data importeren
5. ✅ Dashboard en reports bekijken
6. 📋 Later: Bank/SAP/Reservering data integratie
7. 📋 Later: Automatische matching implementeren
8. 📋 Later: Azure deployment

**Veel succes!** 🎉
