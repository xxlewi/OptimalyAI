# OptimalyAI - Nastavení databáze

## Databázová konfigurace

OptimalyAI používá výhradně **PostgreSQL databázi** pro všechna prostředí.

### Konfigurace

V souboru `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=optimalyai_db;Username=optimaly;Password=OptimalyAI2024!"
  }
}
```

## PostgreSQL s Dockerem

### Požadavky
- Docker Desktop nainstalovaný a spuštěný
- Docker Compose

### Spuštění databáze

```bash
# Spustit PostgreSQL
./Tools/database/docker-db-start.sh

# Zastavit PostgreSQL
./Tools/database/docker-db-stop.sh

# Zobrazit logy
docker-compose logs postgres

# Správa databáze přes pgAdmin
# URL: http://localhost:5050
# Email: admin@optimaly.ai
# Password: admin123
```

### První spuštění

1. Spusťte PostgreSQL:
   ```bash
   ./Tools/database/docker-db-start.sh
   ```

2. Aplikujte migrace:
   ```bash
   dotnet ef database update -p OAI.DataLayer
   ```

3. Spusťte aplikaci:
   ```bash
   python run-dev.py
   ```

## Správa migrací

### Vytvoření nové migrace
```bash
dotnet ef migrations add NazevMigrace -p OAI.DataLayer
```

### Aplikování migrací
```bash
dotnet ef database update -p OAI.DataLayer
```

### Vrácení migrace
```bash
dotnet ef database update PredchoziMigrace -p OAI.DataLayer
```

### Odstranění poslední migrace
```bash
dotnet ef migrations remove -p OAI.DataLayer
```

## Výhody PostgreSQL

- ✅ Plná podpora SQL funkcí
- ✅ Trvalé uložení dat mezi restarty
- ✅ Podpora transakcí a ACID vlastností
- ✅ Výkonné pro produkční nasazení
- ✅ Podpora JSON/JSONB datových typů
- ✅ Full-text search capabilities
- ✅ Snadné zálohování a obnovení

## Řešení problémů

### PostgreSQL se nespustí
```bash
# Zkontrolovat, jestli port 5432 není obsazený
lsof -i :5432

# Smazat staré kontejnery
docker-compose down
docker-compose up -d postgres
```

### Chyby při migraci
```bash
# Smazat databázi a vytvořit znovu
docker-compose down -v
docker-compose up -d postgres
dotnet ef database update
```

### Připojení k databázi přímo
```bash
# Přes psql
docker exec -it optimalyai-postgres psql -U optimaly -d optimalyai_db

# Základní SQL příkazy
\l          # Seznam databází
\dt         # Seznam tabulek
\d tabulka  # Struktura tabulky
\q          # Ukončit
```