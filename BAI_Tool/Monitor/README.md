# BAI Monitor Dashboard

Monitoring dashboard voor BAI transacties, balances en exports.

## Features

- ✅ Beveiligde login met wachtwoord authenticatie
- ✅ Dashboard met real-time transactie overzicht
- ✅ Balance reconciliatie monitoring
- ✅ Transactie type analyse
- ✅ Missing data detectie
- ✅ Grafische visualisaties met Chart.js

## Quick Start

### 1. Configuratie

Kopieer `.env.example` naar `.env` en pas aan:

```bash
cp .env.example .env
```

Edit `.env`:
```
DB_HOST=localhost
DB_PORT=5432
DB_NAME=rabobank
DB_USER=postgres
DB_PASSWORD=your_password

ADMIN_USERNAME=admin
ADMIN_PASSWORD=your_secure_password
SECRET_KEY=generate_random_secret_key
```

### 2. Start met Docker

```bash
docker-compose up --build
```

### 3. Toegang

Open browser: http://localhost:5000

Login met credentials uit `.env` file.

## Development (zonder Docker)

```bash
# Installeer dependencies
pip install -r requirements.txt

# Run development server
python -m app.main
```

## Project Structuur

```
Monitor/
├── app/
│   ├── main.py              # Flask application & routes
│   ├── auth.py              # User authentication
│   ├── database.py          # Database queries
│   ├── templates/           # HTML templates
│   │   ├── base.html
│   │   ├── login.html
│   │   ├── dashboard.html
│   │   ├── transactions.html
│   │   ├── balances.html
│   │   └── reports.html
│   └── static/              # CSS, JS, images
│       ├── css/
│       └── js/
├── config/
│   └── config.py            # Configuration
├── Dockerfile
├── docker-compose.yml
├── requirements.txt
└── .env
```

## Database Schema

Verbindt met PostgreSQL tabellen:
- `dt_camt053_tx` - Transacties
- `dt_camt053_balance` - Balances

## Security

- Session-based authenticatie
- Secure cookie settings
- HTTPS ready (configureer certificate voor productie)
- Password-protected admin access
- Environment variable based credentials

## TODO

- [ ] Export rapporten naar PDF
- [ ] Email notificaties voor mismatches
- [ ] Multi-user support met rollen
- [ ] Scheduled automated reports
- [ ] API endpoints voor externe integratie
