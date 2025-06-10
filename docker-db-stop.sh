#!/bin/bash

# Barvy pro výstup
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🐳 OptimalyAI Docker PostgreSQL Manager${NC}"
echo "========================================"

# Zkontrolovat, jestli Docker běží
if ! docker info &> /dev/null; then
    echo -e "${RED}❌ Docker není spuštěný.${NC}"
    exit 1
fi

# Zastavit PostgreSQL
echo -e "${YELLOW}🛑 Zastavuji PostgreSQL databázi...${NC}"
docker-compose stop postgres

echo -e "${GREEN}✅ PostgreSQL databáze byla zastavena${NC}"
echo ""
echo "💡 Pro opětovné spuštění použijte: ./docker-db-start.sh"