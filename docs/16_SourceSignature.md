# Source Signature Change Tracking

The `SourceSignature` property allows you to detect when a source entity's structure changes, helping you catch unintended breaking changes to your DTOs.

## Overview

When you set `SourceSignature` on a `[Facet]` attribute, the analyzer computes a hash of the source type's properties and compares it to the stored signature. If the source entity changes (properties added, removed, or types changed), you'll get a compile-time warning with the new signature.

## Usage

```csharp
[Facet(typeof(User), SourceSignature = "a1b2c3d4")]
public partial class UserDto;
```

## How It Works

1. **Hash Computation**: The signature is an 8-character SHA-256 hash computed from:
   - Property names and their types
   - Respects `Include`/`Exclude` filters
   - Respects `IncludeFields` setting

2. **Compile-Time Check**: The analyzer compares the stored signature against the current computed signature

3. **Warning on Mismatch**: If they differ, you get diagnostic `FAC022` with the new signature

4. **Code Fix**: Use the provided code fix to automatically update the signature

## Example Workflow

### Initial Setup

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// First, create your facet without a signature
[Facet(typeof(User))]
public partial class UserDto;

// Then add SourceSignature to track changes (get initial value from analyzer)
[Facet(typeof(User), SourceSignature = "8f3a2b1c")]
public partial class UserDto;
```

### When Source Changes

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }  // New property added
}
```

You'll see a warning:

```
FAC022: Source entity 'User' structure has changed. Update SourceSignature to 'd4e5f6a7' to acknowledge this change.
```

### Acknowledging Changes

Use the provided code fix (lightbulb/quick action) to automatically update the signature, or manually update it:

```csharp
[Facet(typeof(User), SourceSignature = "d4e5f6a7")]
public partial class UserDto;
```

## Benefits

- **Intentional Changes**: Forces you to explicitly acknowledge when source entities change
- **Code Review**: Makes structural changes visible in diffs
- **Team Communication**: Alerts team members when shared entities are modified
- **API Stability**: Helps maintain stable DTO contracts

## With Include/Exclude

The signature only considers the properties that will actually be in your facet:

```csharp
// Only tracks Id, Name, Email (excludes Password)
[Facet(typeof(User), "Password", SourceSignature = "1a2b3c4d")]
public partial class UserDto;

// Only tracks FirstName and LastName
[Facet(typeof(User), Include = new[] { "FirstName", "LastName" }, SourceSignature = "5e6f7a8b")]
public partial class UserNameDto;
```

## When to Use

**Recommended for:**
- Public API DTOs where breaking changes affect consumers
- Shared models between services
- DTOs used in serialization/deserialization contracts
- Any facet where stability is critical

**Optional for:**
- Internal-only DTOs
- Rapidly evolving models during development
- Simple/temporary projections

## Diagnostic Reference

| Code | Severity | Description |
|------|----------|-------------|
| FAC022 | Warning | Source entity structure has changed - signature mismatch |

## Tips

1. **Start Without Signature**: Create your facet first, then add the signature once the model stabilizes

2. **Review Changes**: When you see FAC022, review what changed in the source entity before accepting the new signature

3. **Git Blame**: The signature update in your commit history shows when structural changes occurred

4. **Multiple Facets**: Each facet can have its own signature tracking the specific properties it uses
