---
name: agripeweb-mongo-setup
description: |
  Applies MongoDB schema changes to the AgripeWeb project: adds new collections, entity classes,
  fields, and indexes to agpDBContext. This project uses MongoDB — there is no EF Core and no
  dotnet ef migrations.
  Use this skill whenever the user says "add a MongoDB collection", "add a new collection",
  "create an entity", "add an index", "add a field to an entity", or when a feature plan
  has a non-empty MongoDB Changes section in the AgripeWeb project.
  Never use dotnet ef — always use this skill for AgripeWeb schema changes.
---

# AgripeWeb — MongoDB Setup

Apply MongoDB schema changes by modifying `agpDBContext` and entity classes. **Never run `dotnet ef` — this project uses MongoDB with MongoDB.Driver, not EF Core.**

## Step 1: Read the plan

Read `docs/features/{name}/plan.md` (or ask the user what changes are needed). Extract the **MongoDB Changes** section:
- New collections (need new entity class + agpDBContext property)
- New fields on existing entities
- New indexes

## Step 2: Read existing files for patterns

Always read these before making changes:
- `AgripeWebAPI/Models/agpDBContext.cs` — understand the constructor and collection initialization pattern
- The most relevant existing entity in `AgripeWebAPI/Models/Entities/` — understand field conventions

## Step 3: Apply changes

### 3a. New entity class (if needed)

Create in `AgripeWebAPI/Models/Entities/NewEntity.cs`:

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace AgripeWebAPI.Models.Entities;

public class NewEntity : Entity   // Entity provides [BsonId] int Id
{
    public int UserId { get; set; }          // Always for user-owned entities
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // ... other fields
}
```

Rules:
- Inherit from `Entity` (provides `[BsonId] int Id`)
- Include `UserId` for any user-owned entity
- Use `string.Empty` defaults for strings — no null
- Dates: `DateTime` (UTC)

### 3b. Add collection to agpDBContext

In `AgripeWebAPI/Models/agpDBContext.cs`, add:

**1 — Public property:**
```csharp
public IMongoCollection<NewEntity> NewEntities { get; }
```

**2 — Initialize in constructor** (match the exact existing pattern):
```csharp
NewEntities = database.GetCollection<NewEntity>("new_entities");
```

**3 — Create indexes immediately after** (always index UserId on user-owned collections):
```csharp
// Single-field index
NewEntities.Indexes.CreateOne(
    new CreateIndexModel<NewEntity>(
        Builders<NewEntity>.IndexKeys.Ascending(e => e.UserId)));

// Compound index (equality fields first, range fields last)
NewEntities.Indexes.CreateOne(
    new CreateIndexModel<NewEntity>(
        Builders<NewEntity>.IndexKeys
            .Ascending(e => e.UserId)
            .Ascending(e => e.PivotId)));

// Unique index
NewEntities.Indexes.CreateOne(
    new CreateIndexModel<NewEntity>(
        Builders<NewEntity>.IndexKeys.Ascending(e => e.Email),
        new CreateIndexOptions { Unique = true }));
```

### 3c. New fields on existing entities

Add properties to the existing entity class — MongoDB is schema-less, so no migration SQL is needed. Ensure backward compatibility with sensible defaults:

```csharp
// Backward-compatible
public string? Description { get; set; }    // nullable — existing docs have no value
public int Threshold { get; set; } = 0;    // default — existing docs get 0
```

### 3d. New indexes on existing collections

Add index creation in `agpDBContext` constructor after the existing collection's initialization:
```csharp
Sensors.Indexes.CreateOne(
    new CreateIndexModel<Sensor>(
        Builders<Sensor>.IndexKeys.Ascending(s => s.PivotId)));
```

## Step 4: Build verification

Run: `dotnet build AgripeWebAPI/AgripeWebAPI.csproj`

Fix any compilation errors before reporting done.

## Index design rules

- **Always index `UserId`** on every new user-owned collection
- **Compound indexes**: equality fields first (`UserId`), range fields last (`ReadAt`, `CreatedAt`)
- **Unique indexes**: only for truly unique business keys (`Email` on `users`)
- New query filtering by a field without an index — flag as WARNING

## Output

Report: files created or modified, exact lines added to `agpDBContext`, build result.
