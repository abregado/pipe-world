# Writing Docs

Rules for anything under `Assets/Docs/`.

---

## Describe the current state only

A doc is a description of the system as it is right now. It is not a changelog, a design-decision log, or a migration guide.

- Do not describe previous implementations, old field names, or prior architectures.
- Do not explain *why* something changed, what it used to do, or what bug used to exist.
- Do not use phrases like "used to be", "previously", "originally", "the old version", "this replaces X", "this was renamed from Y", "this fixes a bug where...".
- Write every section as if the current design is the only design that has ever existed.

Git history already records what changed and why (commit messages, PR descriptions). Docs are not the place to duplicate that — they go stale the moment the next change lands, since nobody reliably updates prose about the past.

## Present tense, declarative

State facts about the code as it exists: "`X` does `Y`", not "`X` was changed to do `Y`" or "`X` will do `Y`".

## Keep docs in sync with the code they describe

When a change touches something a doc describes, update the doc in the same change — rewritten to describe the new state, not annotated with the old one. A doc describing removed/renamed code is worse than no doc.

## Cite real locations

Reference actual files/line numbers (`[Foo.cs:42](path/Foo.cs)`) so claims are checkable and greppable. Prefer this to prose paraphrase of what code does.

## Cross-link related docs

Use relative markdown links between docs in `Assets/Docs/` so a reader can navigate the set. A doc that mentions a system covered elsewhere should link to it rather than re-explaining it.

Point at a specific section, not just the file, when only part of the other doc is relevant: `[adding-a-prop.md](adding-a-prop.md) §1` or `[prop-tooltips.md](prop-tooltips.md) §4 and §7`. Within a single doc, link to another of its own sections with a standard markdown anchor: `[SocketStation](#socketstation)`.

## Rationale for current behavior is welcome

"Do not explain why something changed" (above) means don't narrate history — it does not mean omit reasoning. When a mechanism's current behavior is non-obvious, explain *why the design has to work this way* as a fact about the present system: what would break, or what edge case is being guarded against, if it worked differently. "The `None` slot in `Inventory._count` is not guaranteed to actually read `0` ... so gating on it is unsafe" is a statement about how the current code must be treated, not a history lesson — write these freely.

## Two kinds of docs

Docs in this set are one of two shapes, and each has its own voice:

- **Reference docs** (e.g. `netcode-guide-piperspace.md`, `single-recipe-machines.md`, `prop-pooling.md`) describe how an existing system works. Pure declarative present tense throughout — "`X` does `Y`" — no numbered steps, no checklist.
- **How-to docs** (e.g. `adding-a-prop.md`, `adding-an-input-action.md`) walk through adding something new to an existing system. Use imperative, numbered `##` steps ("Add a new entry to...", "Create `MyNewPropBehaviour.cs`"), and end with a `## Checklist` of GitHub-style `- [ ]` items that recap every step and file touched — this is the doc's own definition of "done," so it should be checkable without re-reading the prose above it.

Don't mix the voices within one doc: a reference doc doesn't need a checklist, and a how-to doc shouldn't drift into passive description of the system for its own sake — link to the reference doc for that instead.

## Structure

- Title: `# Title Case Heading`.
- A one-paragraph intro right after the title, no heading of its own, giving the one-sentence "what is this and why would you be reading it."
- Separate major sections with a `---` horizontal rule — between the intro and the first section, and between top-level sections that represent a real shift in topic. Don't put `---` between every subsection; reserve it for boundaries a reader would want to jump between.
- In a how-to doc, prefix a step's opening line with a bold label naming where the change happens, before any prose: `**File:** \`Assets/Systems/PropPrototypes.cs\`` or `**Directory:** \`Assets/Scripts/PropBehaviour/\``.

## Linking to code

Cite the way the rest of the file's own citations are formatted: link text is the bare filename, optionally with a line or range (`Foo.cs:42`, `Foo.cs:16-31`); the link target is the real repo-relative path (`[Foo.cs:42](Assets/Scripts/Foo.cs)`). Never link text that's just the path repeated, and never a bare unlinked filename where a real citation is possible.

## Tables for enumerable options

When a doc is listing a fixed, enumerable set — override points on a base class, builder methods, field-type choices, struct members — use a two-column (or wider) markdown table rather than a bullet list of the same shape. Reserve prose bullets for steps or non-parallel points; reach for a table the moment every line has the same "name — what it does" shape.

## Emphasis for critical constraints

Bold a sentence inline (not just as a label) when it states a constraint that, if missed, breaks something non-obviously or for everyone rather than just the reader's own feature — e.g. "**Prefabs are always created by hand in the Unity Editor — never generated or hand-written as raw `.prefab` YAML.**" Reserve this for genuine gotchas, not routine instructions.
