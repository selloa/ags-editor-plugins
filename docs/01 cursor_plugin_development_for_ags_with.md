# Plugin development for AGS with AI integration
_Exported on 6/2/2026 at 14:05:06 GMT+2 from Cursor (3.6.31)_

---

**User**

explore the possiblity of writing a plugin for adventure game studio 3.6 that makes it possible to integrate modern AI agents into the AGS code editor

---

**Cursor**

I’ll quickly check AGS 3.6 editor/plugin capabilities and then map what’s feasible for integrating modern AI agents into the editor workflow.

Yes — this is **feasible**, with some important limits.

## What AGS 3.6 gives you

From the AGS docs, AGS 3.x supports **.NET editor plugins** (C#/VB.NET class library) for extending the Editor UI, via `IAGSEditorPlugin` + `IEditorComponent`, with access through `IAGSEditor` ([Editor Plugins](https://adventuregamestudio.github.io/ags-manual/EditorPlugins.html)).

That means you can realistically build:

- menu commands like “Ask AI”, “Refactor selection”, “Generate dialog tree”
- a docked/popup tool window for chat, prompt presets, and apply/preview diffs
- project-aware helpers (read script/module files, include headers, symbols)
- editor-side automation workflows (generate script stubs, translation helpers, etc.)

## Hard constraints to expect

The biggest unknown is **deep script editor embedding** (inline ghost text, token-level autocomplete interception, live cursor suggestions). AGS plugin APIs are documented mostly around component/menu/tree integration, and not clearly around low-level text editor hooks.

So likely:

- **High confidence**: command-driven AI actions (selection -> prompt -> patch preview -> apply)
- **Medium confidence**: custom side panel integrated into editor workflow
- **Low confidence**: full Copilot-style inline completions inside native AGS script editor, unless internal editor APIs expose text events/caret APIs not covered in public docs

## Practical architecture that should work

Use a 2-part design:

1. **AGS Editor Plugin (.NET)**
   - UI, command routing, project context gathering
   - reads/writes script files safely
   - shows patch preview + accept/reject per hunk

2. **AI backend connector**
   - local service (`localhost`) or direct API calls (OpenAI/Anthropic/local model)
   - handles prompt templates, rate limits, streaming, retries, model selection
   - optional RAG over game scripts/modules for context

This keeps AGS-specific code stable while AI provider logic remains replaceable.

## MVP scope (recommended)

Start with features that do not require deep editor internals:

- “Explain selected script”
- “Generate function from comment”
- “Refactor selected block”
- “Create dialogue boilerplate from plain English”
- “Find likely bugs in this room/global script”
- “Generate translation extraction helper snippets”

Workflow:
- user selects text (or chooses file/module)
- plugin sends context to AI
- plugin returns **diff-style proposed change**
- user reviews and applies

## Risk checklist

- **API surface risk**: script editor internals may not expose enough hooks for true inline AI UX
- **Security/privacy**: game scripts may contain proprietary story/IP; need local-mode option
- **Stability**: async calls must not block AGS UI thread
- **Versioning**: tie `RequiredAGSVersion` to tested 3.6 patch levels
- **Trust/safety**: always preview patches; never silent auto-apply

## Recommendation

Build it as an **Editor assistant plugin** first (chat/prompt + patch workflow).  
Treat “native inline completions in AGS code editor” as a **Phase 2 research item** after validating whether AGS exposes sufficient text-editor hooks in 3.6 internals.

If you want, I can draft a concrete technical spec next: plugin project layout, interfaces, prompt contract, and a week-by-week implementation plan for an MVP.

---

**User**

are there local models that would work for command driven AI actions from within AGS?

---

**Cursor**

Yes — several **local models are good fits** for command-driven actions inside an AGS plugin.

Best options (practical today) are:

- **Qwen2.5-Coder (7B/14B/32B)**: very solid instruction following + code transforms.
- **DeepSeek-Coder v2 Lite / mid-size**: good at refactors and patch-style outputs.
- **Codestral (22B)**: strong code generation/editing if you have enough VRAM.
- **StarCoder2 (15B)**: decent fallback, especially for code completion/edit tasks.
- **Phi-3 / Phi-4 small code-capable variants**: lighter-weight, faster on modest hardware.

For AGS specifically (custom C-like scripting), these work well if you frame prompts as:
- “Given this AGS script, produce unified diff”
- “Return JSON with `edits[]`”
- “Do not invent APIs; stay within AGS 3.6 scripting patterns”

## Local runtime choices for plugin integration

- **Ollama**: easiest local HTTP API (`/api/generate`, `/api/chat`) for a .NET plugin to call.
- **llama.cpp server**: excellent for GGUF quantized models and low-memory setups.
- **LM Studio local server**: easy model management + OpenAI-compatible endpoints.
- **vLLM**: great throughput, but heavier ops footprint.

## What will feel responsive in-editor

- **7B–14B quantized** models are usually the sweet spot for “command-driven” actions (explain/refactor/generate snippet).
- **32B+** gives better quality but may feel slow unless you have strong GPU.
- For UX, keep actions short and structured (JSON/diff), and stream partial output.

## Recommendation for AGS MVP

Start with:
1. **Qwen2.5-Coder 14B (quantized)** via Ollama or llama.cpp  
2. Strict output schema (JSON edits or diff only)  
3. “Preview + apply” flow in AGS plugin

If you want, I can propose a concrete model matrix by your hardware (CPU/GPU/RAM/VRAM) and expected latency per action.

