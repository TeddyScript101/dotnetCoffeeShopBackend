---
name: add-migration
description: Add an EF Core migration and prompt the user to verify the generated files
disable-model-invocation: true
---

Run in the CoffeeShopApi project directory:

```bash
cd /Users/teddy/Documents/programming/claude-ai-agents/coffee-shop/CoffeeShopApi
dotnet ef migrations add {{args}}
```

After running, verify:
1. Open `Migrations/<timestamp>_{{args}}.cs` and confirm the Up/Down methods are correct.
2. Do not manually edit `*Designer.cs` or `CoffeeShopDbContextModelSnapshot.cs`.
3. Apply the migration: `dotnet ef database update` (or just deploy — Render runs it automatically on startup).
