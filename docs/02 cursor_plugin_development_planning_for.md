# Plugin development planning for AGS
_Exported on 6/2/2026 at 14:54:20 GMT+2 from Cursor (3.6.31)_

---

**User**

@cursor_plugin_development_for_ags_with.md 
@EditorPlugins.md 
im in planning phase for this project. separating concerns and issues and coming up with an high level overview of the process of this

note: in the first document it is  mentioned to use local llms, at first were not going to do that. we want to test this with cursor agent first, before think about localizing the llm

also before we start working on the concrete plugin, we should get knowledgable on how to setup a development environment for ags plugins in general. and also we need to decide which AGS version we're going to primarily write for. If there are new features availble in AGSv4 that would be incredibly helpful for our agent integration, then we should pick v4. if new v4 features are not necessary for agent integration, we should stick to 3.6 because it has a large userbase, will be around for a long time even after 4 fully releases, and 4 will be mostly backwards compatible with 3.6

---

**Cursor**

I’ll review both referenced docs first, then I’ll synthesize a practical high-level plan that separates concerns (environment setup, AGS version decision, and agent integration rollout without local LLMs).

I’ve read both docs; next I’ll quickly scan this repo for any AGS v4-specific notes so the version recommendation is grounded in your project context, not just generic assumptions.

Your direction makes sense: treat this as an **editor-integration product** first, and an **LLM-hosting problem** second.

## High-Level Plan (Separated Concerns)

- **Track A — AGS Plugin Foundation:** get a reliable AGS editor plugin skeleton running (menus/components, safe file IO, patch preview/apply).
- **Track B — Agent Connector:** integrate with Cursor Agent flow first (no local model infra initially).
- **Track C — Version Strategy:** design once, target AGS 3.6 first, keep a compatibility layer to evaluate AGS 4 with low rewrite cost.
- **Track D — Product UX:** focus on command-driven actions (explain/refactor/generate) and review-first patching, not inline autocomplete.
- **Track E — Safety/Trust:** never auto-apply edits; always show diff + accept/reject.

This aligns with AGS plugin mechanics in your docs (`IAGSEditorPlugin`, `IEditorComponent`, `RequiredAGSVersion`):

```15:17:EditorPlugins.md
You must have one class in your plugin that implements the `AGS.Types.IAGSEditorPlugin` interface. It must have a constructor that takes one argument, an `AGS.Types.IAGSEditor` object. It must also have a `RequiredAGSVersion` attribute on the class, set to the minimum version of AGS that the plugin requires.
* The editor will find your class and construct it, passing in the *IAGSEditor* object that you can use to access editor methods.
* You work with AGS using *Components*. A component represents one area of functionality...
```

## Version Decision: AGS 3.6 vs 4

### Recommendation
- **Primary target now: AGS 3.6**
- **Prepare for AGS 4 later via compatibility boundary**

### Why
- Your current plugin goal (menu/command/component + patch workflow) does **not** appear to require AGS 4-only capabilities.
- AGS 4 is still in upgrade-heavy evolution and can introduce migration/plugin maintenance overhead.
- AGS 3.6 has stronger installed-base stability and should be enough for command-driven AI assistance MVP.
- Your first document already frames “assistant plugin first, deep inline later” — that favors 3.6-first execution risk-wise.

### Decision Rule (simple and objective)
Move primary target to AGS 4 only if one of these becomes true:
- you need a **specific v4 editor API** that materially improves agent UX, and
- that gain outweighs reduced audience + migration/testing overhead.

## Practical Process (Planning → Execution)

### Phase 0: Discovery (short, 2-4 days)
- Confirm AGS plugin API touchpoints you’ll use in MVP.
- Define “agent contract” (input context, expected diff/JSON edit output, error schema).
- Freeze non-goals: no inline completion interception in v1, no local LLM in v1.

### Phase 1: AGS Plugin Dev Environment (first concrete step)
- Set up clean AGS plugin project (`AGS.Plugin.*` naming, `AGS.Types.dll` reference).
- Build minimal plugin with:
  - one menu command,
  - one component,
  - one dock/panel or modal command UI,
  - file read + diff preview + manual apply.
- Create a repeatable test harness project in AGS 3.6 for regression checks.

### Phase 2: Cursor Agent Integration (no local LLM yet)
- Implement connector layer to call agent backend/provider abstraction.
- Start with 3 commands:
  - Explain selection
  - Refactor selection
  - Generate function from intent
- Force structured output (diff/JSON edits), then render in preview.

### Phase 3: Hardening
- Add cancellation, retries, timeout handling, and non-blocking UI.
- Add project-context packer (current file + related module snippets).
- Add telemetry/logging (latency, token-ish size, failure type) for product decisions.

### Phase 4: Compatibility Track
- Keep AGS API interactions behind an adapter (`IAgsEditorAdapter` style boundary).
- Smoke test against AGS 4 periodically.
- Promote AGS 4 support when adapter delta is small and user demand justifies.

## Key Risks and Mitigations

- **Editor hook limitations:** keep UX command-driven; treat inline Copilot-like UX as separate R&D track.
- **Wrong edits / hallucinations:** strict schema + patch preview + explicit apply.
- **Version drift:** isolate AGS API usage to small adapter surfaces.
- **Responsiveness:** async calls, cancellable operations, avoid blocking editor thread.

## Suggested Project Structure (Conceptual)

- `PluginCore` (AGS integration, components, menus, apply patch)
- `AgentBridge` (Cursor agent request/response contract)
- `ContextBuilder` (selection, file/module context)
- `PatchEngine` (diff parse, conflict check, apply)
- `Compatibility` (`AGS36Adapter`, future `AGS4Adapter`)

## Next Planning Deliverables (what to write next)

- 1-page **Architecture Decision Record**:
  - “Primary target AGS 3.6, AGS 4 secondary”
  - “Cursor Agent first, local LLM deferred”
- MVP **feature spec** with exact command inputs/outputs
- **Test matrix**:
  - AGS 3.6 required path
  - AGS 4 experimental path
- **Exit criteria for v1**:
  - three commands stable
  - safe preview/apply
  - acceptable latency and failure handling

If you want, I can draft those three artifacts next in your preferred format (ADR + MVP spec + test matrix) so you can start implementation immediately.

