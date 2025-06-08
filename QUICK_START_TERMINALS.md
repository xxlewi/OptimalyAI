# Quick Start - AI Tools Development

## ✅ Terminály byly otevřeny!

Měly by se vám otevřít 4 nové terminály. V každém postupujte podle instrukcí níže:

---

## 🏗️ Terminal 1: Core Infrastructure (ZAČNĚTE TADY!)

**Již by měl být na branch:** `feature/ai-tools-infrastructure`

Spusťte:
```bash
code .
```

Pak řekněte Claude:
```
I am working on Terminal 1 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md
Please implement all items starting with Phase 1: Core Interfaces.
This is critical infrastructure that other terminals depend on.
```

---

## 🛠️ Terminal 2: Basic Tools (ČEKEJTE na T1)

**Počkejte až Terminal 1 dokončí Phase 1 & 2**, pak spusťte:
```bash
git checkout -b feature/ai-tools-basic
git pull origin feature/ai-tools-infrastructure
code .
```

Pak řekněte Claude:
```
I am working on Terminal 2 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md
Terminal 1 has created the base interfaces and entities.
Please implement basic tools starting with FileOperationsTool.
```

---

## 🚀 Terminal 3: Advanced Tools (ČEKEJTE na T1)

**Počkejte až Terminal 1 dokončí Phase 1, 2 & 3**, pak spusťte:
```bash
git checkout -b feature/ai-tools-advanced
git pull origin feature/ai-tools-infrastructure
code .
```

Pak řekněte Claude:
```
I am working on Terminal 3 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md
Please implement advanced tools, starting with CodeGenerationTool.
Use the infrastructure from Terminal 1.
```

---

## 🎨 Terminal 4: API & UI (ČEKEJTE na T1)

**Počkejte až Terminal 1 dokončí Phase 1-3**, pak spusťte:
```bash
git checkout -b feature/ai-tools-api-ui
git pull origin feature/ai-tools-infrastructure
code .
```

Pak řekněte Claude:
```
I am working on Terminal 4 from AI_TOOLS_PARALLEL_IMPLEMENTATION.md
Please create API controllers and UI for tools.
Coordinate with Terminal 2 for testing with real tools.
```

---

## 📋 Důležité poznámky:

1. **Terminal 1 musí začít první** - ostatní na něm závisí
2. **Sledujte AI_TOOLS_PARALLEL_IMPLEMENTATION.md** pro detailní checklist
3. **Commitujte často** - minimálně každou hodinu
4. **Při konfliktech** kontaktujte ostatní terminály

## 🔄 Synchronizace:

```bash
# Pro pull změn z jiného terminálu:
git fetch origin
git merge origin/feature/ai-tools-infrastructure --no-ff
```

---

Hodně štěstí s vývojem! 🚀