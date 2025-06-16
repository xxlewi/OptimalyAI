# OptimalyAI Development Tools

Tato složka obsahuje pomocné nástroje pro vývoj a testování aplikace OptimalyAI.

## Struktura

### browser-testing/
Nástroje pro testování a debugování UI v prohlížeči:

- **browser-inspector.js** - JavaScript knihovna pro zachytávání stavu prohlížeče (console logs, network, DOM)
- **browser-test.sh** - Kompletní testovací framework s automatickým reportováním
- **browser-inspect.sh** - Injektuje browser-inspector do libovolné stránky
- **browser-automation.sh** - Automatizace akcí v prohlížeči (DevTools, formuláře)
- **analyze-browser-state.py** - Analyzuje exportovaná data z browser-inspector
- **open-with-devtools.sh** - Rychlé otevření stránky s Developer Tools
- **test_browser.sh** - Jednoduchý příklad použití pro screenshoty

#### Použití:
```bash
# Kompletní test stránky
./Tools/browser-testing/browser-test.sh https://localhost:5005/Projects/Create

# Rychlá inspekce
./Tools/browser-testing/browser-inspect.sh https://localhost:5005

# Otevření s DevTools
./Tools/browser-testing/open-with-devtools.sh https://localhost:5005
```

### database/
Nástroje pro správu PostgreSQL databáze v Dockeru:

- **docker-db-start.sh** - Spustí PostgreSQL kontejner
- **docker-db-stop.sh** - Zastaví PostgreSQL kontejner
- **docker-db-manager.sh** - Interaktivní správce databáze (backup, restore, psql)

#### Použití:
```bash
# Spuštění databáze
./Tools/database/docker-db-start.sh

# Správa databáze (interaktivní menu)
./Tools/database/docker-db-manager.sh

# Zastavení databáze
./Tools/database/docker-db-stop.sh
```

## Poznámky

- Browser testing nástroje vyžadují Google Chrome
- Databázové nástroje vyžadují Docker
- Všechny skripty jsou určeny pro macOS/Linux prostředí