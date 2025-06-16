# OptimalyAI - Nastavení databáze

## Přepínání mezi vývojovou a produkční databází

OptimalyAI podporuje dva režimy databáze:

1. **In-Memory databáze** (výchozí) - pro vývoj
2. **PostgreSQL databáze** - pro produkci

### Konfigurace

V souboru `appsettings.json` nastavte:

```json
{
  "UseProductionDatabase": false,  // false = In-Memory, true = PostgreSQL
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

### První spuštění s PostgreSQL

1. Spusťte PostgreSQL:
   ```bash
   ./Tools/database/docker-db-start.sh
   ```

2. Nastavte v `appsettings.json`:
   ```json
   "UseProductionDatabase": true
   ```

3. Vytvořte migrace (pokud ještě neexistují):
   ```bash
   dotnet ef migrations add InitialCreate
   ```

4. Aplikujte migrace:
   ```bash
   dotnet ef database update
   ```

5. Spusťte aplikaci:
   ```bash
   python run-dev.py
   ```

## Správa migrací

### Vytvoření nové migrace
```bash
dotnet ef migrations add NazevMigrace
```

### Aplikování migrací
```bash
dotnet ef database update
```

### Vrácení migrace
```bash
dotnet ef database update PredchoziMigrace
```

### Odstranění poslední migrace
```bash
dotnet ef migrations remove
```

## Rozdíly mezi In-Memory a PostgreSQL

### In-Memory databáze
- ✅ Rychlé spuštění
- ✅ Žádná konfigurace
- ✅ Ideální pro vývoj a testování
- ❌ Data se ztratí po restartu
- ❌ Nepodporuje všechny SQL funkce

### PostgreSQL
- ✅ Plná podpora SQL
- ✅ Trvalé uložení dat
- ✅ Podpora transakcí
- ✅ Výkonné pro produkci
- ❌ Vyžaduje Docker nebo instalaci
- ❌ Pomalejší start

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