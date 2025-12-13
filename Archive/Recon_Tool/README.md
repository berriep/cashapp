# Reconciliation Tool

Web-based reconciliation tool voor Worldline betalingsdata met multi-source reconciliatie.

## Features

- **Worldline Payments Management**
  - Import van CSV bestanden (180K+ records per maand)
  - PostgreSQL partitioning per maand voor optimale performance
  - Automatische duplicate detection
  - Filter en zoek functionaliteit

- **Multi-Source Reconciliation**
  - Voorbereiding voor Bank, Reservering, en SAP data
  - Configureerbare matching rules
  - Exception tracking en workflow
  - Match confidence scoring

- **Dashboard & Reporting**
  - Real-time statistics
  - Daily volume charts
  - Brand/merchant/country breakdown
  - Import audit trail

- **Data Management**
  - 18-24 maanden data retentie
  - Automatische archivering
  - Partition management

## Tech Stack

- **Backend**: Python 3.11 + Flask
- **Database**: PostgreSQL 12+ (Azure)
- **Frontend**: Bootstrap 5 + Chart.js
- **Deployment**: Docker + Docker Compose

## Installation

### 1. Database Setup

```bash
# Run database schema scripts
cd database
psql -h your-server.postgres.database.azure.com -U your-user -d reconciliation -f 01_create_schema.sql
psql -h your-server.postgres.database.azure.com -U your-user -d reconciliation -f 02_create_users_table.sql
```

Zie `database/README.md` voor gedetailleerde instructies.

### 2. Application Setup

```bash
# Clone repository
cd app

# Create virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1  # Windows PowerShell

# Install dependencies
pip install -r requirements.txt

# Configure environment
cp .env.example .env
# Edit .env met je Azure PostgreSQL credentials
```

### 3. Create Admin User

```bash
python create_admin_user.py
```

### 4. Test Database Connection

```bash
python test_db.py
```

### 5. Run Application

**Development:**
```bash
python main.py
```

**Production (Docker):**
```bash
docker-compose up -d
```

Application: http://localhost:5000

## Configuration

Edit `.env` file:

```env
# Azure PostgreSQL
DB_HOST=your-server.postgres.database.azure.com
DB_PORT=5432
DB_NAME=reconciliation
DB_USER=your-user@your-server
DB_PASSWORD=your-password
DB_SCHEMA=reconciliation

# Application
SECRET_KEY=your-random-secret-key
ADMIN_USERNAME=admin
ADMIN_PASSWORD=your-admin-password

# Flask
FLASK_ENV=development
DEBUG=False
```

## Usage

### Import Worldline Data

1. Navigate to **Import** page
2. Select CSV file (max 500 MB)
3. Click **Upload and Import**
4. Monitor progress in Import History

### Query Payments

1. Navigate to **Payments** page
2. Use filters: date range, brand, merchant, search
3. View results with pagination

### Reconciliation

1. Navigate to **Reconciliation** page
2. View matched/unmatched transactions
3. Manage exceptions
4. Track reconciliation progress

## Data Volume

- **Per maand**: ~180,000 records (~80 MB CSV)
- **18 maanden**: ~3,240,000 records (~1.44 GB)
- **Partitioning**: Maandelijks voor optimale performance

## Azure Deployment

### App Service Deployment

1. Create Azure App Service (Python 3.11)
2. Configure Application Settings (from .env)
3. Deploy via:
   - Azure DevOps Pipeline
   - GitHub Actions
   - Direct deployment: `az webapp up`

### Container Deployment

```bash
# Build and push to Azure Container Registry
docker build -t your-registry.azurecr.io/reconciliation-tool:latest .
docker push your-registry.azurecr.io/reconciliation-tool:latest

# Deploy to Azure Container Instances or App Service
```

## Project Structure

```
app/
├── main.py              # Flask application
├── auth.py              # Authentication
├── database.py          # Database queries
├── data_import.py       # CSV import logic
├── config/
│   └── config.py        # Configuration
├── templates/           # HTML templates
├── static/
│   └── css/
│       └── style.css
├── Dockerfile
├── docker-compose.yml
└── requirements.txt

database/
├── 01_create_schema.sql
├── 02_create_users_table.sql
└── README.md
```

## Maintenance

### Archive Old Partitions

```sql
-- Archive partitions older than 18 months
SELECT * FROM reconciliation.archive_old_partitions(18);
```

### Create New Partitions

```sql
-- Create partition for future month
SELECT reconciliation.create_worldline_partition('2026-01-01'::DATE);
```

### Monitor Database Size

```sql
SELECT 
    tablename,
    pg_size_pretty(pg_total_relation_size('reconciliation.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'reconciliation'
ORDER BY pg_total_relation_size('reconciliation.'||tablename) DESC;
```

## Troubleshooting

### Connection Issues

```bash
# Test database connection
python test_db.py
```

### Import Failures

- Check CSV format (semicolon delimiter, European decimals)
- Verify PAYDATE column format (DD/MM/YYYY)
- Check Import History for error messages

### Performance

- Ensure queries use `paydate` filter for partition pruning
- Verify indexes exist: `\d reconciliation.worldline_payments`
- Monitor Azure PostgreSQL metrics

## Support

For issues or questions, contact the development team.

## License

Internal use only - Client project
