# Code Comment Style

Rules for comments in first-party `.cs` files (not vendored code under `Assets/Plugins/`, `Assets/com.rlabrecque.steamworks.net/`, `Assets/Scripts/Topten.JsonKit/`, or similar third-party packages).

---

## A comment states what the code can't say for itself

A comment exists for one reason: to give the reader technical information that reading the code will not. A hidden invariant, a constraint that isn't visible at the call site, a Unity- or engine-specific quirk the code has to work around. If deleting the comment would leave a competent reader with no less understanding than they had with it, delete it.

Do not write a comment to:

- **Restate what the code does.** Well-named types, methods, and fields already say this. `// increments the counter` above `count++` is noise.
- **Explain why something was built this way, or what it used to do.** History belongs in commit messages, not in the code — see [writing-docs.md](writing-docs.md)'s "Describe the current state only". Don't write "changed this to fix...", "used to be...", "originally this was...".
- **Say where a member is used or called from.** That's what "Find All References" is for. A comment listing callers goes stale the moment a new caller is added or an old one is removed, and nobody reliably updates it.
- **Give usage instructions.** "To add a new X, do Y" belongs in a how-to doc under `Assets/Docs/`, not inline in the code it's instructing about. Link to the doc if a pointer is genuinely useful; don't duplicate its content.
- **Describe what a data class means in-game.** A `Prop` subclass, a `ScriptableObject`, or a prototype definition doesn't need a comment explaining what the thing represents to the player — the type name, field names, and any design-facing content (e.g. a `description` string field) already carry that.

**Rationale is fine when it's a fact about the current system, not a history lesson.** If a piece of code has to be written a specific, non-obvious way to avoid breaking something, say so as a present-tense fact: "`nextMakeTick` is set to `ulong.MaxValue` here, not `0` — `0` is a valid tick and would make the machine try to craft immediately" is a real technical constraint worth one line. "We set it to `MaxValue` because the old scheduler treated `0` as unset" is not — the old scheduler is gone, and the comment is now a history lesson.

## Keep it short

A comment should be the shortest sentence that lands the point — usually well under one line, rarely more than one. If a comment needs multiple sentences or a paragraph to explain a constraint, that's a sign the constraint belongs in a doc under `Assets/Docs/`, with a short pointer left in the code (`// see docs/prop-pooling.md`), not a paragraph inline.

## XML doc comments follow the same rule

`///` summaries are comments, not exempt from the rules above because they're formatted as a doc block. Don't add a `<summary>` that just restates the member's name and signature (`/// <summary>Gets the door prop's sector.</summary>` above `SectorId GetSector()`). Only write one when the member has behavior a caller could not predict from its signature and name alone.

## Example

```csharp
// Bad — restates the code, explains history, and describes a caller that isn't there anymore.
// This used to run every FixedUpdate but was moved here after profiling showed it was too slow,
// so now PropSyncer.sync() calls it once per tick instead. Loops over all live objects and
// checks if they're still valid, removing any that aren't.
foreach (var entry in liveObjects) { ... }

// Good — states a constraint the code has to satisfy that isn't visible at the call site.
// LiveKey excludes the prop's slab generation, so a stale key can still match a reused slot — recheck prototypeId.
foreach (var entry in liveObjects) { ... }
```

## Dead code is not a comment

Commented-out code (`// Button resetAir = new Button() { ... }`) isn't a comment under any of the rules above — it's dead code. Delete it; git history has it if it's ever needed again.
