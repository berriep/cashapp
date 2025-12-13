# Reconciliation Tool - Database Setup Guide

## Overview
Database schema voor de Reconciliation Tool met PostgreSQL partitioning voor Worldline betalingsdata.

## Prerequisites
- PostgreSQL 12+ (Azure PostgreSQL)
- Toegang tot de database met CREATE SCHEMA rechten

## Data Volume
- **Per maand**: ~180,000 records (~80 MB)
- **18 maanden**: ~3,240,000 records (~1.44 GB)
- **Partitioning**: Maandelijks, automatisch beheer

## Installation Steps

### 1. Connect to Azure PostgreSQL
```bash
psql "host=your-server.postgres.database.azure.com port=5432 dbname=your-database user=your-user@your-server sslmode=require"
```

### 2. Run Schema Scripts (in volgorde)
```bash
# Create main schema and tables
psql -h your-server.postgres.database.azure.com -U your-user -d your-database -f 01_create_schema.sql

# Create users table
psql -h your-server.postgres.database.azure.com -U your-user -d your-database -f 02_create_users_table.sql
```

Of via pgAdmin / Azure Portal Query Editor

### 3. Verify Installation
```sql
-- Check schema
SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'reconciliation';

-- Check tables
SELECT table_name FROM information_schema.tables WHERE table_schema = 'reconciliation' ORDER BY table_name;

-- Check partitions
SELECT tablename FROM pg_tables WHERE schemaname = 'reconciliation' AND tablename LIKE 'worldline_payments_%' ORDER BY tablename;
```

## Database Structure

### Main Tables

#### 1. `worldline_payments` (Partitioned)
- **Partitioning**: Range op `paydate` (maandelijks)
- **Records**: 45 kolommen van Worldline CSV
- **Indexes**: id, paydate, ref, order, batchref, brand, merchref, total, status
- **Automatische partities**: 24 maanden terug + 6 maanden vooruit

#### 2. `worldline_payments_archive`
- Archief tabel voor data ouder dan retention period
- Zelfde structuur als hoofdtabel

#### 3. `data_sources`
- Registry van alle databronnen (Worldline, Bank, Reservering, SAP)
- Initiële data wordt automatisch aangemaakt

#### 4. `reconciliation_rules`
- Configureerbare matching regels tussen databronnen
- Matching criteria opgeslagen als JSONB
- Default regel: Worldline-Bank matching op REF, PAYDATE, BRAND, MERCHREF

#### 5. `reconciliation_matches`
- Gematched transacties tussen databronnen
- Match confidence score
- Tracking van matched fields

#### 6. `reconciliation_exceptions`
- Unmatched transacties en discrepancies
- Status tracking (OPEN, INVESTIGATING, RESOLVED, ACCEPTED)
- Assignment workflow

#### 7. `file_import_log`
- Audit trail voor CSV imports
- Success/failure tracking
- Record counts (totaal, imported, failed, duplicates)

#### 8. `monitor_users`
- User authentication (matching BAI_Tool)
- Bcrypt password hashing
- Admin/user roles

## Utility Functions

### Create Partition
```sql
-- Create partition voor specifieke maand
SELECT reconciliation.create_worldline_partition('2025-01-01'::DATE);
```

### Archive Old Partitions
```sql
-- Archiveer partities ouder dan 18 maanden
SELECT * FROM reconciliation.archive_old_partitions(18);
```

## Maintenance

### Automatic Partition Creation
Partities worden automatisch aangemaakt voor:
- 24 maanden geleden tot nu
- 6 maanden in de toekomst

Voor nieuwe maanden in de toekomst, run:
```sql
SELECT reconciliation.create_worldline_partition(CURRENT_DATE + INTERVAL '7 months');
```

### Check Partition Distribution
```sql
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'reconciliation' 
  AND tablename LIKE 'worldline_payments_%'
ORDER BY tablename DESC;
```

### Archive Strategy
Oudere data (>18-24 maanden) wordt gearchiveerd naar `worldline_payments_archive`:
```sql
-- Handmatig archiveren
SELECT * FROM reconciliation.archive_old_partitions(18);
```

## Performance Tips

1. **Queries altijd met paydate filter** voor partition pruning:
   ```sql
   SELECT * FROM reconciliation.worldline_payments 
   WHERE paydate BETWEEN '2024-01-01' AND '2024-12-31';
   ```

2. **Indexes gebruiken** op REF, MERCHREF, BRAND voor reconciliation queries

3. **Bulk imports** gebruiken (COPY) voor snelle CSV import

## Reconciliation Matching

### Default Matching Criteria
- `REF` (payment reference)
- `PAYDATE` (payment date, ±1 dag tolerance)
- `BRAND` (MasterCard, Visa, etc.)
- `MERCHREF` (merchant reference)

### Custom Rules toevoegen
```sql
INSERT INTO reconciliation.reconciliation_rules (
    rule_name, 
    source_a_id, 
    source_b_id, 
    matching_criteria, 
    tolerance_amount
) VALUES (
    'Custom SAP Match',
    1,  -- Worldline
    4,  -- SAP
    '{"fields": ["ref", "total"], "match_type": "fuzzy", "tolerance_days": 2}'::jsonb,
    0.10
);
```

## Next Steps
1. Run de schema scripts op Azure PostgreSQL
2. Create eerste admin user (via Python script)
3. Import eerste CSV batch
4. Setup web interface voor querying

## Azure Specific Notes

### Connection String Format
```
host=your-server.postgres.database.azure.com port=5432 dbname=reconciliation user=your-user@your-server password=xxx sslmode=require
```

### Firewall Rules
Zorg dat je lokale IP toegang heeft tot Azure PostgreSQL:
- Azure Portal → PostgreSQL → Networking → Firewall Rules
- Add your client IP

### Performance Tier
Voor 3M+ records:
- Minimum: General Purpose, 2 vCores, 10 GB storage
- Recommended: General Purpose, 4 vCores, 50 GB storage (met auto-grow)
